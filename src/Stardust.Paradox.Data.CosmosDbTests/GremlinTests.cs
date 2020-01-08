using Newtonsoft.Json;
using Stardust.Nucleus;
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Providers.Gremlin;
using Stardust.Paradox.Data.Traversals;
using Stardust.Paradox.Data.Traversals.Helpers;
using Stardust.Particles;
using Stardust.Particles.Collection.Arrays;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations.DataTypes;
using Xunit;
using Xunit.Abstractions;
using static Stardust.Paradox.Data.Traversals.GremlinFactory;

namespace Stardust.Paradox.CosmosDbTest
{

	public class GremlinTests : IDisposable
	{
		private readonly ITestOutputHelper _output;
		private IDependencyResolver _scope;

		public GremlinTests(ITestOutputHelper output)
		{
			_output = output;
			_scope = Resolver.CreateScopedResolver();
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
			using (var tc = TestContext())
			{
				var jonas = CreateItem(tc, "Jonas", "Techincal Solution Architect", true);
				jonas.ProgramingLanguages.AddRange(new[] { "C#", "C++", "JavaScript", "Gremlin" });
				var tor = CreateItem(tc, "Tor", "Farmer", true);
				var rita = CreateItem(tc, "Rita", "Farmer", true);
				var kine = CreateItem(tc, "Kine", isAdult: true);
				var sanne = CreateItem(tc, "Sanne");
				var marena = CreateItem(tc, "Marena");
				var mathilde = CreateItem(tc, "Mathilde");
				var herman = CreateItem(tc, "Herman");
				var dnvgl = CreateCompany(tc, "DNVGL", "dnvgl.com", "kema.com");
				var gss = CreateCompany(tc, "GSS");
				var gssIt = CreateCompany(tc, "GSSIT");
				await tc.SaveChangesAsync();
				dnvgl.Divisions.Add(gss);
				await tc.SaveChangesAsync();
				gss.Divisions.Add(gssIt);
				await tor.Spouce.SetVertexAsync(rita);

				jonas.Parents.Add(tor);

				jonas.Parents.Add(rita);
				jonas.Employers.Add(gssIt);
				sanne.Parents.Add(jonas, new Dictionary<string, object> { { "birthPlace", "Kristiansand" }, { "created", DateTime.Now } });
				sanne.Parents.Add(kine, new Dictionary<string, object> { { "birthPlace", "Kristiansand" }, { "created", DateTime.Now } });
				herman.Parents.Add(jonas, new Dictionary<string, object> { { "birthPlace", "Kristiansand" }, { "created", DateTime.Now } });
				herman.Parents.Add(kine, new Dictionary<string, object> { { "birthPlace", "Kristiansand" }, { "created", DateTime.Now } });
				marena.Parents.Add(jonas);
				kine.Children.Add(mathilde);
				await kine.Spouce.SetVertexAsync(jonas);
				await tc.SaveChangesAsync();
			}

		}

		[Fact]
		public async Task GetEdges()
		{
			using (var tc = TestContext())
			{
				var s = await tc.Profiles.GetAsync("Sanne".ToTuple());
				var parents = await s.Parents.ToEdgesAsync();
				Assert.NotNull(parents.FirstOrDefault()?.Properties.FirstOrDefault());
			}
		}

		[Fact]
		public async Task GetTypedEdges()
		{
			using (var tc = TestContext())
			{
				var j = await tc.Profiles.AllAsync(0, 1);
				var e = await tc.Employments.AllAsync(0, 1);
				var inV = await e.First().InVAsync();
				var outV = await e.First().OutVAsync();
				Assert.NotNull(inV);
				Assert.NotNull(outV);
				var edge = await tc.Employments.GetAsync(e.First().InVertexId, e.First().OutVertextId);
				Assert.NotNull(edge);
				e.First().HiredDate = DateTime.Now;
				e.First().Manager = "Test Manager";
				await tc.SaveChangesAsync();
				var createdE = tc.Employments.Create(j.First(), await e.First().OutVAsync());
				Assert.NotNull(createdE);
				var inEdges = await tc.Employments.GetByInIdAsync(e.First().InVertexId);
				var outEdges = await tc.Employments.GetByOutIdAsync(e.First().OutVertextId);
				Assert.NotEmpty(inEdges);
				Assert.NotEmpty(outEdges);
				var newEdge = tc.Employments.Create(await e.First().InVAsync(), await e.First().OutVAsync());
				Assert.NotNull(newEdge);
				///Assert.True(((EdgeGraphEntity)newEdge).);
				newEdge.HiredDate = DateTime.Now;
				newEdge.Manager = "test";
				await tc.SaveChangesAsync();
				tc.Delete(newEdge);
				await tc.SaveChangesAsync();
			}
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
			var tc = new TestContext(new GremlinNetLanguageConnector($"{ConfigurationManagerHelper.GetValueOnKey("cosmosDbAccount")}.gremlin.cosmosdb.azure.com", "graphTest", "services", ConfigurationManagerHelper.GetValueOnKey("cosmosDbKey")));
			tc.OnDisposing = c =>
			{
				c.SaveChangesError -= OnTcOnSaveChangesError;
				c.SavingChanges -= OnTcOnSavingChanges;
				c.ChangesSaved -= OnTcOnChangesSaved;
			};
            tc.Disposing += Tc_Disposing;
			tc.SavingChanges += OnTcOnSavingChanges;
			tc.ChangesSaved += OnTcOnChangesSaved;
			tc.SaveChangesError += OnTcOnSaveChangesError;
			return tc;
		}

		private void Tc_Disposing(GraphContextBase sender)
		{
			_output.WriteLine($"Total RU Consumption {sender.ConsumedRU}");
		}

		private void OnTcOnSavingChanges(object sender, SaveEventArgs args)
		{
			_output.WriteLine($"saving changes with {args.TrackedItems.Count()} tracked items");
		}

		private void OnTcOnChangesSaved(object sender, SaveEventArgs args)
		{
			_output.WriteLine($"saved changes with {args.TrackedItems.Count()} tracked items");
		}

		private void OnTcOnSaveChangesError(object sender, SaveEventArgs args)
		{
			_output.WriteLine($"failed statement: {args.FailedUpdateStatement}");
			_output.WriteLine($"saving changes failed with message: {args.Error.Message}");
			_output.WriteLine(args.Error.StackTrace);
		}

		private static IProfile CreateItem(TestContext tc, string id, string ocupation = null, bool isAdult = false)
		{
			var item = tc.CreateEntity<IProfile>(id);
			item.Name = id;
			item.Pk = id;
			item.FirstName = id;
			item.Ocupation = ocupation;
			item.Adult = isAdult;
			return item;
		}

		private static ICompany CreateCompany(TestContext tc, string id, params string[] domains)
		{
			var item = tc.CreateEntity<ICompany>(id);
			item.Name = id;
			item.Pk = id;
			if (domains.ContainsElements())
				item.EmailDomains.AddRange(domains);
			return item;
		}

		[Fact]
		public void DataContextCreateTest()
		{
			IProfile t;
			using (var tc = TestContext())
			{
				t = tc.CreateEntity<IProfile>("jonas.syrstad");
				Assert.NotNull(t);
				t.Email = "jonas.syrstad@dnvgl.com";
				t.FirstName = "Jonas";
				t.LastName = "Syrstad";
				t.VerifiedEmail = true;
				_output.WriteLine(JsonConvert.SerializeObject(t));
			}


		}

		[Fact]
		public async Task EscapeCharacterTest()
		{
			IProfile t;
			using (var tc = TestContext())
			{
				t = tc.CreateEntity<IProfile>("jonas.syrstad");
				Assert.NotNull(t);
				t.Email = "jonas.syrstad@dnvgl.com";
				t.FirstName = "Jonas";
				t.LastName = "Syrstad";
				t.Description = "jonas's little test";
				t.VerifiedEmail = true;
				await tc.SaveChangesAsync();
				_output.WriteLine(JsonConvert.SerializeObject(t));
				tc.Delete(t);
				await tc.SaveChangesAsync();
			}


		}

		[Fact]
		public async Task GetOrCreateTests()
		{
			IProfile newItem;
			using (var tc = new TestContext(new Class1()))
			{
				var jonas = await tc.GetOrCreate<IProfile>("Jonas");
				Assert.Equal("Jonas", jonas.Name);
				newItem = await tc.GetOrCreate<IProfile>("getOrCreateTest");
				Assert.Null(newItem.Name);
			}


		}

		[Fact]
		public async Task GetPerfTest()
		{
			using (var tc = TestContext())
			{
				var j = await tc.Profiles.GetAsync("Jonas", "Jonas");
			}
			using (var tc = TestContext())
			{
				var t = Stopwatch.StartNew();
				var j = await tc.Profiles.GetAsync("Jonas", "Jonas");
				t.Stop();
				_output.WriteLine($"Gremlin.Net: {t.ElapsedMilliseconds}ms");
			}
			using (var tc = new TestContext(new Class1()))
			{

				var j = await tc.Profiles.GetAsync("Jonas", "Jonas");

			}
			using (var tc = new TestContext(new Class1()))
			{
				var t = Stopwatch.StartNew();
				var j = await tc.Profiles.GetAsync("Jonas", "Jonas");
				t.Stop();
				_output.WriteLine($"Document client: {t.ElapsedMilliseconds}ms");
			}
		}

		[Fact]
		public async Task DataContextReadTestAsync()
		{
			IProfile jonas;
			using (var tc = TestContext())
			{
				jonas = await tc.VAsync<IProfile>("Jonas", "Jonas");

				_output.WriteLine("Me");
				_output.WriteLine(JsonConvert.SerializeObject(jonas));
				Assert.NotNull(jonas);

				var parents = await jonas.Parents.ToVerticesAsync();
				_output.WriteLine("Parents");
				_output.WriteLine(JsonConvert.SerializeObject(parents));

				var children = await jonas.Children.ToVerticesAsync();
				_output.WriteLine("Children");
				//_output.WriteLine(JsonConvert.SerializeObject(jonas.Children));

				_output.WriteLine("First child's parents");
				_output.WriteLine(JsonConvert.SerializeObject(await children.First().Parents.ToVerticesAsync()));
				_output.WriteLine("Childs siblings (fluent)");
				var firstChild = children.First(c => c.Name == "Sanne");
				var siblings2 = await firstChild.AllSiblings.ToVerticesAsync();
				_output.WriteLine(JsonConvert.SerializeObject(siblings2));
				Assert.Equal(4, siblings2.Count());
				_output.WriteLine("Child's siblings (attribute)");
				var siblings = await firstChild.Siblings.ToVerticesAsync();
				Assert.Equal(3, siblings.Count());
				_output.WriteLine(JsonConvert.SerializeObject(siblings));

				_output.WriteLine("Spouce");
				_output.WriteLine(JsonConvert.SerializeObject(await jonas.Spouce.ToVertexAsync()));

				var gssit = (await jonas.Employers.ToVerticesAsync()).First();
				var dnvgl = await gssit.Group.ToVertexAsync();
				_output.WriteLine("Group from dnvgl");
				_output.WriteLine(JsonConvert.SerializeObject(dnvgl));

				var gss = await gssit.Parent.ToVertexAsync();
				_output.WriteLine(JsonConvert.SerializeObject(await gss.Group.ToVertexAsync()));

				_output.WriteLine(
					JsonConvert.SerializeObject(await (await gss.Group.ToVertexAsync()).AllEmployees.ToVerticesAsync()));
			}
		}

		[Fact]
		public async Task GetTreeTest()
		{
			IVertexTreeRoot<IProfile> jonas;
			using (var tc = TestContext())
			{
				var tree = await tc.GetTreeAsync<IProfile>("Tor", "parent");
				var f = tree.First().First().Key;
				_output.WriteLine("F:");
				_output.WriteLine(JsonConvert.SerializeObject(f));
				_output.WriteLine("Tree:");
				_output.WriteLine(JsonConvert.SerializeObject(tree));
				_output.WriteLine("Jonas:");

				jonas = await tc.GetTreeAsync<IProfile>("Jonas", t => t.Parents, true);
				_output.WriteLine(JsonConvert.SerializeObject(jonas));
			}
		}

        [Fact]
        public async Task DataContextReadWriteTestAsync()
        {
            var time = DateTime.UtcNow;
            using (var tc = TestContext())
            {
                var jonas = await tc.VAsync<IProfile>("Jonas".ToTuple());
                Assert.NotNull(jonas);
                jonas.LastUpdated = DateTime.Now;
                jonas.SomeEnum = GenderTypes.Other;
                jonas.LastUpdatedEpoch = (EpochDateTime) DateTime.Now;
                jonas.FirstName = "Jonas";
                jonas.LastName = null;
                jonas.Email = "jonas.syrstad@dnvgl.com";
                jonas.SetProperty("someRandomProp",$"test+:{DateTime.UtcNow.Ticks}");
                jonas.SomeProperty = new MyProp {TimeStamp = time};
                Assert.Null(jonas.LastName);
                await tc.SaveChangesAsync();
            }

            using (var tc = TestContext())
            {
                var jonas = await tc.VAsync<IProfile>("Jonas".ToTuple());
                Assert.NotNull(jonas.GetProperty("someRandomProp"));
                Assert.NotNull(jonas);
                jonas.SomeEnum = GenderTypes.Male;
                Assert.Null(jonas.LastName);
                jonas.LastName = "Syrstad";
                Assert.NotNull(jonas.SomeProperty);
                Assert.Equal(time,jonas.SomeProperty.TimeStamp);
                Assert.NotEmpty(jonas.DynamicPropertyNames);
                await tc.SaveChangesAsync();
            }
        }

        [Fact]
		public async Task GraphSetTests()
		{
			IEnumerable<IProfile> jonas2;
			using (var tc = TestContext())
			{
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

				jonas2 = await tc.Profiles.FilterAsync(p => p.Name, "Jonas");
				Assert.Single(jonas2);
				//await jonas.Children.LoadAsync();
				var c = jonas.Children.Count<IProfile>();
				var nochildren = await jonas.Children.FirstOrDefault<IProfile>().Children.ToVerticesAsync();
				Assert.NotNull(nochildren);
				Assert.Empty(nochildren);
				foreach (var profile in nochildren)
				{
					_output.WriteLine("wtf?!?");
				}
				_output.WriteLine(JsonConvert.SerializeObject(await jonas2.SingleOrDefault().GetTreeAsync<IProfile>(p => p.Parents)));
			}
		}

		[Fact]
		public async Task DataContextCreateReadDeleteTestAsync()
		{
			IProfile test3;
			using (var tc = TestContext())
			{
				var test = tc.CreateEntity<IProfile>("test.item");
				test.Email = "test.@dnvgl.com";
				test.Pk = "test.item";
				test.FirstName = "test";
				test.LastName = "test";
				test.LastUpdated = DateTime.Now;
				test.Name = "test";
				test.Number = 1;
				await tc.SaveChangesAsync().ConfigureAwait(false);
				tc.Clear();
				var test2 = await tc.VAsync<IProfile>("test.item", "test.item");
                test2.Email = "test2.@dnvgl.com";
                await tc.SaveChangesAsync();
                var test4= await tc.VAsync<IProfile>("test.item", "test.item");
                Assert.Equal(test2.Email,test4.Email);
                Assert.Equal(test2,test4);
                var j = await tc.VAsync<IProfile>("Jonas");
				test2.Parents.Add(j);
				await tc.SaveChangesAsync();
				Assert.NotNull(test2);
				tc.Delete(test2);
				await tc.SaveChangesAsync();
				tc.Clear();

				test3 = await tc.VAsync<IProfile>("test.item", "test.item");
				Assert.Null(test3);
			}
		}

		[Fact]
		public async Task ExecuteAdvancedQueryTest()
		{
			IEnumerable<IProfile> result;
			using (var tc = TestContext())
			{
				result = await tc.VAsync<IProfile>(SiblingQuery);
				Assert.Equal(3, result.Count());
				_output.WriteLine(JsonConvert.SerializeObject(result));
			}
		}

		public static GremlinQuery SiblingQuery(GremlinContext g)
		{
			var q = g.V().Has("name", "Sanne").As("s") //find start
				.In("parent").Out("parent") //navigate to siblings
				.Where(p => p.Without("s")).Dedup();
			return q;
		}

		[Fact]
		public async Task CreateGetDeleteItemWithoutEdges()
		{
			using (var c = TestContext())
			{
				var i = c.Profiles.Create("string");
				i.Name = "string";
				i.Email = "string";
				i.Pk = "string";
				i.FirstName = "string";
				i.LastName = "string";
				await c.SaveChangesAsync();
			}

			using (var c = TestContext())
			{
				var i = await c.Profiles.GetAsync("string", "string");
				Assert.NotNull(i);
			}
			using (var c = TestContext())
			{
				await c.Profiles.DeleteAsync("string", "string");
				await c.SaveChangesAsync();
			}
		}

		[Fact]
		public async Task CreateGetDeleteItemWithEdges()
		{
			using (var c = TestContext())
			{
				var i = c.Profiles.Create("string");
				i.Name = "string";
				i.Pk = "string";
				i.Email = "string";
				i.FirstName = "string";
				i.LastName = "string";
				var i2 = c.Profiles.Create("string2");
				i2.Pk = "string2";
				i2.Name = "string";
				i2.Email = "string";
				i2.FirstName = "string";
				i2.LastName = "string";
				await c.SaveChangesAsync();
			}

			using (var c = TestContext())
			{
				var i = await c.Profiles.GetAsync("string", "string");
				var t = await c.Profiles.GetAsync("string2", "string2");
				i.Parents.Add(t);
				await c.SaveChangesAsync();
				Assert.NotNull(i);
			}

			using (var c = TestContext())
			{
				var i = await c.Profiles.GetAsync("string", "string");
				var t = await c.Profiles.GetAsync("string2", "string2");
				await i.Parents.LoadAsync();
				i.Parents.Remove(t);
				await c.SaveChangesAsync();
				Assert.NotNull(i);
			}

			using (var c = TestContext())
			{
				var i = await c.Profiles.GetAsync("string", "string");
				var t = await c.Profiles.GetAsync("string2", "string2");
				Assert.Empty(i.Parents);
				Assert.Empty(t.Children);
				Assert.NotNull(i);
			}
			using (var c = TestContext())
			{
				await c.Profiles.DeleteAsync("string", "string");
				await c.Profiles.DeleteAsync("string2");
				await c.SaveChangesAsync();
			}
		}

		[Fact]
		public async Task CreateGetDeleteItemWithEdgesParallel()
		{
			try
			{
				GremlinContext.ParallelSaveExecution = true;
				using (var c = TestContext())
				{
					var i = c.Profiles.Create("string");
					i.Name = "string";
					i.Email = "string";
					i.Pk = "string";
					i.FirstName = "string";
					i.LastName = "string";
					var i2 = c.Profiles.Create("string2");
					i2.Name = "string";
					i2.Email = "string";
					i2.Pk = "string2";
					i2.FirstName = "string";
					i2.LastName = "string";
					await c.SaveChangesAsync();
				}

				using (var c = TestContext())
				{
					var i = await c.Profiles.GetAsync("string", "string");
					var t = await c.Profiles.GetAsync("string2", "string2");
					i.Parents.Add(t);
					await c.SaveChangesAsync();
					Assert.NotNull(i);
				}
				using (var c = TestContext())
				{
					try
					{
						var i = await c.Profiles.GetAsync("string", "string");
						var t = await c.Profiles.GetAsync("string2", "string2");
						i.Parents.Add(t);
						await c.SaveChangesAsync();
						Assert.NotNull(i);
					}
					catch (Exception ex)
					{
						_output.WriteLine(ex.Message);
					}
				}

				using (var c = TestContext())
				{
					var i = await c.Profiles.GetAsync("string", "string");
					var t = await c.Profiles.GetAsync("string2", "string2");
					await i.Parents.LoadAsync();
					i.Parents.Remove(t);
					await c.SaveChangesAsync();
					Assert.NotNull(i);
				}

				using (var c = TestContext())
				{
					var i = await c.Profiles.GetAsync("string", "string");
					var t = await c.Profiles.GetAsync("string2", "string2");
					Assert.Empty(i.Parents);
					Assert.Empty(t.Children);
					Assert.NotNull(i);
				}

				using (var c = TestContext())
				{
					await c.Profiles.DeleteAsync("string");
					await c.Profiles.DeleteAsync("string2");
					await c.SaveChangesAsync();
				}
			}
			finally
			{
				GremlinContext.ParallelSaveExecution = false;
			}
		}

		public enum VertextType
		{
			company
		}

		[Fact]
		public async Task QueryBuilderTest()
		{
            var staringWith = G.V().Has("name", p => p.StartingWith("sa"));
            var notStaringWith = G.V().Has("name", p => p.NotStartingWith("sa"));
            var testQuery = G.V().Where(s => s.Out().HasLabel("test").And().Out().HasLabel("test2"));
			Assert.Equal("g.V().where(out().hasLabel(__p0).and().out().hasLabel(__p1))", testQuery.CompileQuery());
			var rangeQuery = G.V().Range(1, 1);
			var v = G.V();
			var val= DateTime.Now.AddDays(-100).ToEpoch();
			var z = v.HasId("Jonas").Has("name","Jonas").Has("lastUpdatedEpoch",p=>p.Gte(val));//.Has("lastUpdated", p => p.Gte((decimal)50.3));
			var J = await z.ExecuteAsync();
			var t = v.HasLabel(VertextType.company.ToString());
			var c = await t.Count().ExecuteAsync();
			var y = await t.ExecuteAsync();
			await PrintResult(rangeQuery, false);
			var range = await rangeQuery.ExecuteAsync();
			Assert.Empty(range);
			rangeQuery = G.V().Range(1, 2);
			await PrintResult(rangeQuery, false);
			range = await rangeQuery.ExecuteAsync();
			Assert.NotEmpty(range);
			Assert.Single(range);
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
			var q4 = G.V();
			q4 = q4.Where(p => p.HasLabel("person"));
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
			Assert.Equal("g.V().has(__p0,__p1).as(__p2).in(__p3).out(__p4).where(without(__p6)).dedup().values(__p5)", siblings.ToString());
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
			_scope.TryDispose();
		}
	}

    public enum GenderTypes
    {
        Male,
        Female,
        Other
    }

    public class MyProp:IComplexProperty
    {
        public DateTime TimeStamp { get; set; }
        public override string ToString()
        {
            return TimeStamp.ToString(CultureInfo.InvariantCulture);
        }
    }
}
