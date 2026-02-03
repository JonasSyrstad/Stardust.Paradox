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
using Stardust.Paradox.Data.Internals;
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
				var alexis = CreateItem(tc, "Alexis", "Technical Solution Architect", true);
				alexis.ProgramingLanguages.AddRange(new[] { "C#", "C++", "JavaScript", "Gremlin" });
				var bryce = CreateItem(tc, "Bryce", "Farmer", true);
				var casey = CreateItem(tc, "Casey", "Farmer", true);
				var devon = CreateItem(tc, "Devon", isAdult: true);
				var ember = CreateItem(tc, "Ember");
				var finley = CreateItem(tc, "Finley");
				var gray = CreateItem(tc, "Gray");
				var harper = CreateItem(tc, "Harper");
				var zephyrcorp = CreateCompany(tc, "ZephyrCorp", "zephyrcorp.com", "windtech.com");
				var nexus = CreateCompany(tc, "Nexus");
				var nexustech = CreateCompany(tc, "NexusTech");
				await tc.SaveChangesAsync();
				zephyrcorp.Divisions.Add(nexus);
				await tc.SaveChangesAsync();
				nexus.Divisions.Add(nexustech);
				await bryce.Spouce.SetVertexAsync(casey);

				alexis.Parents.Add(bryce);

				alexis.Parents.Add(casey);
				alexis.Employers.Add(nexustech);
				ember.Parents.Add(alexis, new Dictionary<string, object> { { "birthPlace", "Millbrook" }, { "created", DateTime.Now } });
				ember.Parents.Add(devon, new Dictionary<string, object> { { "birthPlace", "Millbrook" }, { "created", DateTime.Now } });
				harper.Parents.Add(alexis, new Dictionary<string, object> { { "birthPlace", "Millbrook" }, { "created", DateTime.Now } });
				harper.Parents.Add(devon, new Dictionary<string, object> { { "birthPlace", "Millbrook" }, { "created", DateTime.Now } });
				finley.Parents.Add(alexis);
				devon.Children.Add(gray);
				await devon.Spouce.SetVertexAsync(alexis);
				await tc.SaveChangesAsync();
			}

		}

		[Fact]
		public async Task GetEdges()
		{
			using (var tc = TestContext())
			{
				var s = await tc.Profiles.GetAsync("Ember".ToTuple());
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
			var tc = new TestContext(new GremlinNetLanguageConnector($"jonas-playground.gremlin.cosmos.azure.com", "graphTest", "graphTest", ConfigurationManagerHelper.GetValueOnKey("cosmosDbKey")));
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
                t.Pk = "pk";
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
                t.Pk = "pk";
				await tc.SaveChangesAsync();
				_output.WriteLine(JsonConvert.SerializeObject(t));
				tc.Delete(t);
				await tc.SaveChangesAsync();
			}


		}

		//[Fact]
		//public async Task GetOrCreateTests()
		//{
		//	IProfile newItem;
		//	using (var tc = new TestContext(new Class1()))
		//	{
		//		var alexis = await tc.GetOrCreate<IProfile>("Alexis","pk");
		//		Assert.Equal("Alexis", alexis.Name);
		//		newItem = await tc.GetOrCreate<IProfile>("getOrCreateTest");
		//		Assert.Null(newItem.Name);
		//	}


		//}

		//[Fact]
		//public async Task GetPerfTest()
		//{
		//	using (var tc = TestContext())
		//	{
		//		var j = await tc.Profiles.GetAsync("Alexis", "Alexis");
		//	}
		//	using (var tc = TestContext())
		//	{
		//		var t = Stopwatch.StartNew();
		//		var j = await tc.Profiles.GetAsync("Alexis", "Alexis");
		//		t.Stop();
		//		_output.WriteLine($"Gremlin.Net: {t.ElapsedMilliseconds}ms");
		//	}
		//	using (var tc = new TestContext(new Class1()))
		//	{

		//		var j = await tc.Profiles.GetAsync("Alexis", "Alexis");

		//	}
		//	using (var tc = new TestContext(new Class1()))
		//	{
		//		var t = Stopwatch.StartNew();
		//		var j = await tc.Profiles.GetAsync("Alexis", "Alexis");
		//		t.Stop();
		//		_output.WriteLine($"Document client: {t.ElapsedMilliseconds}ms");
		//	}
		//}

		[Fact]
		public async Task DataContextReadTestAsync()
		{
			IProfile alexis;
			using (var tc = TestContext())
			{
				alexis = await tc.VAsync<IProfile>("Alexis", "Alexis");

				_output.WriteLine("Me");
				_output.WriteLine(JsonConvert.SerializeObject(alexis));
				Assert.NotNull(alexis);

				var parents = await alexis.Parents.ToVerticesAsync();
				_output.WriteLine("Parents");
				_output.WriteLine(JsonConvert.SerializeObject(parents));

				var children = await alexis.Children.ToVerticesAsync();
				_output.WriteLine("Children");
				//_output.WriteLine(JsonConvert.SerializeObject(alexis.Children));

				_output.WriteLine("First child's parents");
				_output.WriteLine(JsonConvert.SerializeObject(await children.First().Parents.ToVerticesAsync()));
				_output.WriteLine("Childs siblings (fluent)");
				var firstChild = children.First(c => c.Name == "Ember");
				var siblings2 = await firstChild.AllSiblings.ToVerticesAsync();
				_output.WriteLine(JsonConvert.SerializeObject(siblings2));
				Assert.Equal(4, siblings2.Count());
				_output.WriteLine("Child's siblings (attribute)");
				var siblings = await firstChild.Siblings.ToVerticesAsync();
				Assert.Equal(3, siblings.Count());
				_output.WriteLine(JsonConvert.SerializeObject(siblings));

				_output.WriteLine("Spouce");
				_output.WriteLine(JsonConvert.SerializeObject(await alexis.Spouce.ToVertexAsync()));

				var nexustech = (await alexis.Employers.ToVerticesAsync()).First();
				var zephyrcorp = await nexustech.Group.ToVertexAsync();
				_output.WriteLine("Group from zephyrcorp");
				_output.WriteLine(JsonConvert.SerializeObject(zephyrcorp));

				var nexus = await nexustech.Parent.ToVertexAsync();
				_output.WriteLine(JsonConvert.SerializeObject(await nexus.Group.ToVertexAsync()));

				_output.WriteLine(
					JsonConvert.SerializeObject(await (await nexus.Group.ToVertexAsync()).AllEmployees.ToVerticesAsync()));
			}
		}

        [Fact]
        public async Task DataContextToVerticesFilteredAsync()
        {
            IProfile alexis;
            using (var tc = TestContext())
            {
                alexis = await tc.VAsync<IProfile>("Alexis", "Alexis");

                _output.WriteLine("Me");
                _output.WriteLine(JsonConvert.SerializeObject(alexis));
                Assert.NotNull(alexis);
                var filterd =await alexis.Children.ToVerticesAsync(profile => profile.Name, "Finley");
                Assert.Equal(1,filterd.Count());
                _output.WriteLine(JsonConvert.SerializeObject(filterd.First()));
            }
        }

        [Fact]
		public async Task GetTreeTest()
		{
			IVertexTreeRoot<IProfile> alexis;
			using (var tc = TestContext())
			{
				var tree = await tc.GetTreeAsync<IProfile>("Bryce", "parent");
				var f = tree.First().First().Key;
				_output.WriteLine("F:");
				_output.WriteLine(JsonConvert.SerializeObject(f));
				_output.WriteLine("Tree:");
				_output.WriteLine(JsonConvert.SerializeObject(tree));
				_output.WriteLine("Alexis:");

				alexis = await tc.GetTreeAsync<IProfile>("Alexis", t => t.Parents, true);
				_output.WriteLine(JsonConvert.SerializeObject(alexis));
			}
		}

        [Fact]
        public async Task DataContextReadWriteTestAsync()
        {
            var time = DateTime.UtcNow;
            using (var tc = TestContext())
            {
                var alexis = await tc.VAsync<IProfile>("Alexis".ToTuple());
                Assert.NotNull(alexis);
                alexis.LastUpdated = DateTime.Now;
                alexis.SomeEnum = GenderTypes.Other;
                alexis.LastUpdatedEpoch = (EpochDateTime) DateTime.Now;
                alexis.FirstName = "Alexis";
                alexis.LastName = null;
                alexis.Email = "alexis.taylor@zephyrcorp.com";
                alexis.SetProperty("someRandomProp",$"test+:{DateTime.UtcNow.Ticks}");
                alexis.SomeProperty = new MyProp {TimeStamp = time};
                Assert.Null(alexis.LastName);
                await tc.SaveChangesAsync();
            }

            using (var tc = TestContext())
            {
                var alexis = await tc.VAsync<IProfile>("Alexis".ToTuple());
                Assert.NotNull(alexis.GetProperty("someRandomProp"));
                Assert.NotNull(alexis);
                Assert.Null(alexis.LastName);
                Assert.NotNull(alexis.SomeProperty);
                Assert.Equal(time,alexis.SomeProperty.TimeStamp);
                alexis.SomeProperty.TimeStamp=DateTime.UtcNow;
                var g = alexis as GraphDataEntity;
                Assert.True(g.IsDirty);
                alexis.LastName = "Taylor";
                alexis.SomeEnum = GenderTypes.Male;
                Assert.NotEmpty(alexis.DynamicPropertyNames);
                await tc.SaveChangesAsync();
            }
        }

        [Fact]
		public async Task GraphSetTests()
		{
			IEnumerable<IProfile> alexis2;
			using (var tc = TestContext())
			{
				var alexis = await tc.Profiles.GetAsync("Alexis");
				Assert.NotNull(alexis);
				var page1 = await tc.Profiles.GetAsync(0, 3);
				var page2 = await tc.Profiles.GetAsync(1, 3);
				var page3 = await tc.Profiles.GetAsync(2, 3);
				Assert.Equal(2, page3.Count());
				Assert.Equal(3, page2.Count());
				Assert.Equal(3, page1.Count());

				var page4 = await tc.Profiles.GetAsync(g => g.V().HasLabel("person"), 0, 6);
				Assert.Equal(6, page4.Count());

				alexis2 = await tc.Profiles.FilterAsync(p => p.Name, "Alexis");
				Assert.Single(alexis2);
				//await alexis.Children.LoadAsync();
				var c = alexis.Children.Count<IProfile>();
				var nochildren = await alexis.Children.FirstOrDefault<IProfile>().Children.ToVerticesAsync();
				Assert.NotNull(nochildren);
				Assert.Empty(nochildren);
				foreach (var profile in nochildren)
				{
					_output.WriteLine("wtf?!?");
				}
				_output.WriteLine(JsonConvert.SerializeObject(await alexis2.SingleOrDefault().GetTreeAsync<IProfile>(p => p.Parents)));
			}
		}

		[Fact]
		public async Task DataContextCreateReadDeleteTestAsync()
		{
			IProfile test3;
			using (var tc = TestContext())
			{
				var test = tc.CreateEntity<IProfile>("test.item");
				test.Email = "test.user@example.com";
				test.Pk = "test.item";
				test.FirstName = "test";
				test.LastName = "test";
				test.LastUpdated = DateTime.Now;
				test.Name = "test";
				test.Number = 1;
				await tc.SaveChangesAsync().ConfigureAwait(false);
				tc.Clear();
				var test2 = await tc.VAsync<IProfile>("test.item", "test.item");
                test2.Email = "test2.user@example.com";
                await tc.SaveChangesAsync();
                var test4= await tc.VAsync<IProfile>("test.item", "test.item");
                Assert.Equal(test2.Email,test4.Email);
                Assert.Equal(test2,test4);
                var j = await tc.VAsync<IProfile>("Alexis");
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
            var y = g.V().Or(x=>x.Has("name", "Ember"),x=>x.Has("name", "Harper"));
            var query = y.CompileQuery();
			var parameters=y.Parameters;
            ;	
			var q = g.V().Has("name", "Ember").As("s") //find start
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
            var staringWith = G.V().Has("name", p => p.StartingWith("em"));
            var notStaringWith = G.V().Has("name", p => p.NotStartingWith("em"));
            var testQuery = G.V().Where(s => s.Out().HasLabel("test").And().Out().HasLabel("test2"));
			Assert.Equal("g.V().where(out().hasLabel(__p0).and().out().hasLabel(__p1))", testQuery.CompileQuery());
			var rangeQuery = G.V().Range(1, 1);
			var v = G.V();
			var val= DateTime.Now.AddDays(-100).ToEpoch();
			var z = v.HasId("Alexis").Has("name","Alexis").Has("lastUpdatedEpoch",p=>p.Gte(val));//.Has("lastUpdated", p => p.Gte((decimal)50.3));
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
			////var bryce = JsonConvert.DeserializeObject("{\"name\": [\"bryce\"],\"ocupation\": [\"\"]}", typeof(ServiceDefinition));
			var q1 = G.V()
				.As("a").Out("parent").As("b")
				.Where("a", p => p.P.Not(q => q.Gt("b")))
				.Values("name").Select("a", "b");
			await PrintResult(q1, false);
			var q2 = G.V("Alexis").Out("parent");
			await PrintResult(q2);
			var q3 = G.V("Alexis");
			await PrintResult(q3);
			var q4 = G.V();
			q4 = q4.Where(p => p.HasLabel("person"));
			await PrintResult(q4, false);
			var q5 = G.V(1).Until(p => p.Has("name", "Alexis")).Repeat(p => p.Out()).Path().By("name");
			await PrintResult(q5, false, false);
			var q6 = G.V().Optional(p => p.Out("parent")).Dedup().Properties("name");//list all parents
			await PrintResult(q6);

			//var q7 = G.V().Coalesce(p => p.HasLabel("person").Values("name"),
			//	p => p.Constant("inhuman"));

			//await PrintResult(q7);

			//var q8 = G.V().Choose(p => p.HasLabel("person"), p => p.Values("name"),
			//	p => p.Constant("inhuman"));

			//await PrintResult(q8);

			var q9 = G.V().Out().Dedup().Values("name").Inject("test");

			await PrintResult(q9);

			var siblings = G.V().Has("name", "Ember").As("s")//find start
				.In("parent").Out("parent")//navigate to siblings
				.Where(p => p.Without("s")).Dedup()//filter self and deduplicated result set
				.Values("name");//project the names
								//g.V().has('name','ember').as('s').out('parent').in('parent').where(without('s')).dedup().values('name')
			Assert.Equal("g.V().has(__p0,__p1).as(__p2).in(__p3).out(__p4).where(without(__p6)).dedup().values(__p5)", siblings.ToString());
			await PrintResult(siblings);
			await PrintResult(siblings.Count());
            
            //var matchTest = G.V().Match(p => p.__().As("a").Out("parent").As("b"),
            //    p=>p.__().As("b").Has("name","Alexis")
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
        private DateTime _timeStamp;

        public DateTime TimeStamp
        {
            get => _timeStamp;
            set
            {
                if (value.Equals(_timeStamp)) return;
                _timeStamp = value;
                OnPropertyChanged();
            }
        }
    }
}
