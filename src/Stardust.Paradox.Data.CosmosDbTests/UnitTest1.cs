using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Stardust.Nucleus;
using Stardust.Nucleus.TypeResolver;
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.Providers.Gremlin;
using Stardust.Paradox.Data.Traversals;
using Stardust.Paradox.Data.Traversals.Helpers;
using Stardust.Particles;
using Stardust.Particles.Collection.Arrays;
using Xunit;
using Xunit.Abstractions;
using static Stardust.Paradox.Data.Traversals.GremlinFactory;

namespace Stardust.Paradox.CosmosDbTest
{

    public class GremlinTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private IKernelContext _scope;

        public GremlinTests(ITestOutputHelper output)
        {
            _output = output;

        }

        static GremlinTests()
        {
            ConfigurationManagerHelper.SetManager(new NullManager());

            Resolver.LoadModuleConfiguration<TestBp>();
        }


        [Fact]
        public async Task InsertItem()
        {

            await G.V().Drop().ExecuteAsync();
            var tc = TestContext();
            var jonas = CreateItem(tc, "Jonas", "Techincal Solution Architect");
            jonas.ProgramingLanguages.AddRange(new[] { "C#", "C++", "JavaScript", "Gremlin" });
            var tor = CreateItem(tc, "Tor", "Farmer");
            var rita = CreateItem(tc, "Rita", "Farmer");
            var kine = CreateItem(tc, "Kine");
            var sanne = CreateItem(tc, "Sanne");
            var marena = CreateItem(tc, "Marena");
            var mathilde = CreateItem(tc, "Mathilde");
            var herman = CreateItem(tc, "Herman");
            var dnvgl = CreateCompany(tc, "DNVGL", "dnvgl.com", "kema.com");
            var gss = CreateCompany(tc, "GSS");
            var gssIt = CreateCompany(tc, "GSSIT");
            //await tc.SaveChangesAsync();
            dnvgl.Divisions.Add(gss);
            gss.Divisions.Add(gssIt);
            await tor.Spouce.SetVertexAsync(rita);

            jonas.Parents.Add(tor);
            jonas.Parents.Add(rita);
            jonas.Employers.Add(gssIt);
            //await jonas.Spouce.SetVertexAsync(kine);
            sanne.Parents.Add(jonas);
            sanne.Parents.Add(kine);
            herman.Parents.Add(jonas);
            herman.Parents.Add(kine);
            marena.Parents.Add(jonas);
            //mathilde.Parents.Add(kine);

            kine.Children.Add(mathilde);
            await tc.SaveChangesAsync();

        }

        private TestContext TestContext()
        {
            JsonConvert.DefaultSettings = () =>
            {
                return new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    Error = (sender, args) =>
                    {
                        Logging.DebugMessage("");
                    },

                }.RegisterGraphSerializer();
            };
            //var tc = new TestContext(new Class1());
            var tc = new TestContext(new GremlinNetLanguageConnector("jonas-graphtest.gremlin.cosmosdb.azure.com", "graphTest", "services", "1TKgMc0u6F0MOBQi4jExGm1uAfOMHcXxylcvL55qV7FiCKx5LhTIW0FVXvJ68zdzFnFaS58yPtlxmBLmbDka1A=="));
            tc.SavingChanges += (sender, args) => { _output.WriteLine($"saving changes with {args.TrackedItems.Count()} tracked items"); };
            tc.ChangesSaved += (sender, args) => { _output.WriteLine($"saved changes with {args.TrackedItems.Count()} tracked items"); };
            tc.SaveChangesError += (sender, args) => { _output.WriteLine($"saving changes failed with message: {args.Error.Message}"); };
            return tc;
        }

        private static IProfile CreateItem(TestContext tc, string id, string ocupation = null)
        {
            var item = tc.CreateEntity<IProfile>(id);
            item.Name = id;
            item.FirstName = id;
            item.Ocupation = ocupation;
            return item;
        }

        private static ICompany CreateCompany(TestContext tc, string id, params string[] domains)
        {
            var item = tc.CreateEntity<ICompany>(id);
            item.Name = id;
            if (domains.ContainsElements())
                item.EmailDomains.AddRange(domains);
            return item;
        }

        [Fact]
        public void DataContextCreateTest()
        {
            var tc = TestContext();
            var t = tc.CreateEntity<IProfile>("jonas.syrstad");
            Assert.NotNull(t);
            t.Email = "jonas.syrstad@dnvgl.com";
            t.FirstName = "Jonas";
            t.LastName = "Syrstad";
            t.VerifiedEmail = true;
            _output.WriteLine(JsonConvert.SerializeObject(t));
        }

        [Fact]
        public async Task GetOrCreateTests()
        {
            var tc = new TestContext(new Class1());
            var jonas = await tc.GetOrCreate<IProfile>("Jonas");
            Assert.Equal("Jonas", jonas.Name);
            var newItem = await tc.GetOrCreate<IProfile>("getOrCreateTest");
            Assert.Null(newItem.Name);
        }

        [Fact]
        public async Task DataContextReadTestAsync()
        {
            var tc = TestContext();
            var jonas = await tc.VAsync<IProfile>("Jonas");
            _output.WriteLine("Me");
            _output.WriteLine(JsonConvert.SerializeObject(jonas));
            Assert.NotNull(jonas);

            var parents = jonas.Parents;
            _output.WriteLine("Parents");
            _output.WriteLine(JsonConvert.SerializeObject(parents));

            var children = await jonas.Children.ToVerticesAsync();
            _output.WriteLine("Children");
            //_output.WriteLine(JsonConvert.SerializeObject(jonas.Children));

            _output.WriteLine("First child's parents");
            _output.WriteLine(JsonConvert.SerializeObject(await children.First().Parents.ToVerticesAsync()));
            _output.WriteLine("Childs siblings (fluent)");
            var firstChild = children.First(c=>c.Name=="Sanne");
            var siblings2 = await firstChild.AllSiblings.ToVerticesAsync();
            _output.WriteLine(JsonConvert.SerializeObject(siblings2));
            Assert.Equal(4, siblings2.Count());
            _output.WriteLine("Child's siblings (attribute)");
            var siblings = await firstChild.Siblings.ToVerticesAsync();
            Assert.Equal(3, siblings.Count());
            _output.WriteLine(JsonConvert.SerializeObject(siblings));

            _output.WriteLine("Spouce");
            _output.WriteLine(JsonConvert.SerializeObject(jonas.Spouce));
            var serializedJ = JsonConvert.SerializeObject(jonas);
            _output.WriteLine(serializedJ);
            //var deserializedJ = JsonConvert.DeserializeObject<IProfile>(serializedJ);
            //Assert.NotNull(deserializedJ);
            //_output.WriteLine("Serialization");
            //_output.WriteLine(JsonConvert.SerializeObject(deserializedJ));

            var gssit = (await jonas.Employers.ToVerticesAsync()).First();
            var dnvgl = await gssit.Group.ToVertexAsync();
            _output.WriteLine("Group from dnvgl");
            _output.WriteLine(JsonConvert.SerializeObject(dnvgl));

            var gss = await gssit.Parent.ToVertexAsync();
            _output.WriteLine(JsonConvert.SerializeObject(await gss.Group.ToVertexAsync()));

            _output.WriteLine(JsonConvert.SerializeObject(await (await gss.Group.ToVertexAsync()).AllEmployees.ToVerticesAsync()));
        }

        [Fact]
        public async Task GetTreeTest()
        {
            var tc = TestContext();
            var tree = await tc.GetTreeAsync<IProfile>("Tor", "parent");
            var f = tree.First().First().Key;
            _output.WriteLine("F:");
            _output.WriteLine(JsonConvert.SerializeObject(f));
            _output.WriteLine("Tree:");
            _output.WriteLine(JsonConvert.SerializeObject(tree));
            _output.WriteLine("Jonas:");
            var jonas = await tc.GetTreeAsync<IProfile>("Jonas", p => p.Parents, true);
            _output.WriteLine(JsonConvert.SerializeObject(jonas));
        }

        [Fact]
        public async Task DataContextReadWriteTestAsync()
        {
            var tc = TestContext();
            var jonas = await tc.VAsync<IProfile>("Jonas");
            Assert.NotNull(jonas);
            jonas.LastUpdated = DateTime.Now;
            jonas.FirstName = "Jonas";
            jonas.LastName = "Syrstad";
            jonas.Email = "jonas.syrstad@dnvgl.com";
            await tc.SaveChangesAsync();
        }

        [Fact]
        public async Task GraphSetTests()
        {
            var tc = TestContext();
            var jonas = await tc.Profiles.GetAsync("Jonas");
            Assert.NotNull(jonas);
            var page1 = await tc.Profiles.GetAsync(0, 3);
            var page2 = await tc.Profiles.GetAsync(1, 3);
            var page3 = await tc.Profiles.GetAsync(2, 3);
            Assert.Equal(2, page3.Count());
            Assert.Equal(3, page2.Count());
            Assert.Equal(3, page1.Count());

            var page4 = await tc.Profiles.GetAsync(g => g.V().HasLabel("person"), 0, 6);
            Assert.Equal(6, page4.Count());

            var jonas2 = await tc.Profiles.FilterAsync(p => p.Name, "Jonas");
            Assert.Single(jonas2);
            _output.WriteLine(JsonConvert.SerializeObject(await jonas2.SingleOrDefault().GetTreeAsync<IProfile>(p => p.Parents)));
        }

        [Fact]
        public async Task DataContextCreateReadDeleteTestAsync()
        {
            var tc = TestContext();
            var test = tc.CreateEntity<IProfile>("test.item");
            test.Email = "test.@dnvgl.com";
            test.FirstName = "test";
            test.LastName = "test";
            test.LastUpdated = DateTime.Now;
            test.Name = "test";
            await tc.SaveChangesAsync();
            tc = TestContext();
            var test2 = await tc.VAsync<IProfile>("test.item");
            var j = await tc.VAsync<IProfile>("Jonas");
            test2.Parents.Add(j);
            await tc.SaveChangesAsync();
            Assert.NotNull(test2);
            tc.Delete(test2);
            await tc.SaveChangesAsync();
            var test3 = await tc.VAsync<IProfile>("test.item");
            Assert.Null(test3);
        }

        [Fact]
        public async Task ExecuteAdvancedQueryTest()
        {
            IGraphContext tc = TestContext();
            var result = await tc.VAsync<IProfile>(SiblingQuery);

            Assert.Equal(3, result.Count());
            _output.WriteLine(JsonConvert.SerializeObject(result));
        }

        public static GremlinQuery SiblingQuery(GremlinContext g)
        {
            var q = g.V().Has("name", "Sanne").As("s") //find start
                .In("parent").Out("parent") //navigate to siblings
                .Where(p => p.Without("s")).Dedup();
            return q;
        }

        [Fact]
        public async Task QueryBuilderTest()
        {
            ////var tor = JsonConvert.DeserializeObject("{\"name\": [\"tor\"],\"ocupation\": [\"\"]}", typeof(ServiceDefinition));
            var q1 = G.V()
                .As("a").Out("parent").As("b")
                .Where("a", p => p.P.Not(q => q.Gt("b")))
                .Values("name").Select("a", "b");
            await PrintResult(q1, false);
            var q2 = G.V("Jonas").Out("parent");
            await PrintResult(q2);
            var q3 = G.V("Jonas");
            await PrintResult(q3);
            var q4 = G.V().Where(p => p.HasLabel("person"));
            await PrintResult(q4, false);
            var q5 = G.V(1).Until(p => p.Has("name", "Jonas")).Repeat(p => p.Out()).Path().By("name");
            await PrintResult(q5, false, false);
            var q6 = G.V().Optional(p => p.Out("parent")).Dedup().Properties("name");//list all parents
            await PrintResult(q6);

            var q7 = G.V().Coalesce(p => p.HasLabel("person").Values("name"),
                p => p.Constant("inhuman"));

            await PrintResult(q7);

            var q8 = G.V().Choose(p => p.HasLabel("person"), p => p.Values("name"),
                p => p.Constant("inhuman"));

            await PrintResult(q8);

            var q9 = G.V().Out().Dedup().Values("name").Inject("test");

            await PrintResult(q9);

            var siblings = G.V().Has("name", "Sanne").As("s")//find start
                .In("parent").Out("parent")//navigate to siblings
                .Where(p => p.Without("s")).Dedup()//filter self and deduplicated result set
                .Values("name");//project the names
            //g.V().has('name','sanne').as('s').out('parent').in('parent').where(without('s')).dedup().values('name')
            Assert.Equal("g.V().has('name','Sanne').as('s').in('parent').out('parent').where(without('s')).dedup().values('name')", siblings.ToString());
            await PrintResult(siblings);
            await PrintResult(siblings.Count());
            //var matchTest = G.V().Match(p => p.__().As("a").Out("parent").As("b"),
            //    p=>p.__().As("b").Has("name","Jonas")
            //    );
            //await PrintResult(matchTest);

        }

        private async Task PrintResult(GremlinQuery q1, bool outputResult = true, bool execute = true)
        {
            _output.WriteLine($"query: {q1}");
            if (execute)
            {
                var t = Stopwatch.StartNew();
                var result = await q1.ExecuteAsync();
                t.Stop();
                if (outputResult)
                {
                    _output.WriteLine($"result accuired in {t.ElapsedMilliseconds}ms:");
                    _output.WriteLine(JsonConvert.SerializeObject(result));
                }
            }
            _output.WriteLine("");
        }

        public void Dispose()
        {
            //_scope.TryDispose();
        }
    }

    public class TestContext : GraphContextBase
    {
        public TestContext(IGremlinLanguageConnector connector) : base(connector, Resolver.CreateScopedResolver())
        {
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            ServiceProvider.TryDispose();
        }

        protected override bool InitializeModel(IGraphConfiguration configuration)
        {
            configuration.ConfigureCollection<IProfile>()
                .AddEdge(t => t.Parents, "parent").Reverse<IProfile>(t => t.Children)
                .AddQuery(t => t.AllSiblings, g => g.V("{id}").As("s").In("parent").Out("parent").Dedup())
            .ConfigureCollection<ICompany>();

            return true;
        }

        public IGraphSet<IProfile> Profiles => GraphSet<IProfile>();

        public IGraphSet<ICompany> Companies => GraphSet<ICompany>();
    }
}
