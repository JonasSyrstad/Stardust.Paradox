# Stardust.Paradox
Entity framework'ish tool for developing .net applications using gremlin graph query language with CosmosDb
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=JonasSyrstad_Stardust.Paradox&metric=alert_status)](https://sonarcloud.io/dashboard?id=JonasSyrstad_Stardust.Paradox)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=JonasSyrstad_Stardust.Paradox&metric=security_rating)](https://sonarcloud.io/dashboard?id=JonasSyrstad_Stardust.Paradox)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=JonasSyrstad_Stardust.Paradox&metric=sqale_rating)](https://sonarcloud.io/dashboard?id=JonasSyrstad_Stardust.Paradox)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=JonasSyrstad_Stardust.Paradox&metric=reliability_rating)](https://sonarcloud.io/dashboard?id=JonasSyrstad_Stardust.Paradox)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=JonasSyrstad_Stardust.Paradox&metric=ncloc)](https://sonarcloud.io/dashboard?id=JonasSyrstad_Stardust.Paradox)
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2FJonasSyrstad%2FStardust.Paradox.svg?type=shield)](https://app.fossa.io/projects/git%2Bgithub.com%2FJonasSyrstad%2FStardust.Paradox?ref=badge_shield)

# Usage (asp.net core)

## Startup.cs
### ConfigureServices
Add the generated entity implementations to the IOC container (I will provide an extention method to make this easier)
```cs
 services.AddEntityBinding((entityType, entityImplementation) => services.AddTransient(entityType, entityImplementation))
        .AddScoped<MyEntityContext,MyEntityContext>()
        .AddScoped<IGremlinLanguageConnector>(s => new CosmosDbLanguageConnector(DbAccountName, AccessKey, "databaseName","collectionName"));
```

## Defining the model

```cs
[VertexLabel("person")]
public interface IPerson : IVertex
{
    string Id {get;}

    string FirstName { get; set; }

    string LastName { get; set; }
    
    string Email { get; set; }

    EpochDateTime Birthday {get;set;}//Wrapper type for DateTime that serializes into unix epoch. Can be used in predicate steps directly.

    IEdgeCollection<IPerson> Parents { get; }

    IEdgeCollection<IPerson> Children { get; }

    IEdgeCollection<IPerson> Siblings { get; }

    [InLabel("city")] //pointing to the Residents property in ICity
    IEdgeReference<ICity> HomeCity { get; }//use IEdgeReference to enable task-async operations

    IEdgeCollection<ICompany> Employers { get; }
}

[VertexLabel("city")]
public interface ICity : IVertex
{
    string Id { get; }

    string Name { get; set; }

    string ZipCode { get; set; }
    

   [OutLabel("city")] //pointing to the HomCity property in IPerson
    IEdgeCollection<Iperson> Residents { get; } //use IEdgeCollection to enable task-async operations on the collection

    IEdgeReference<ICountry> Country { get; }
}

[VertexLabel("company")]
public interface ICompany : IVertex
{
    string Id { get; }

    string Name { get; set; }

    IEdgeCollection<ICompany> Employees { get; }
}

[VertexLabel("country")]
public interface ICountry : IVertex
{
    string Id { get; }

    string Name { get; set; }

    string CountryCode { get; set; }

    string PhoneNoPrefix { get; set; }

    IEdgeCollection<ICity> Cities { get; }
}

 [EdgeLabel("employer")]
    public interface IEmployment : IEdge<IProfile, ICompany>
    {
        string Id { get; }

        EpochDateTime HiredDate { get; set; }

        string Manager { get; set; }
    }

```

## Defining the entity context and generating the entity implementations

```cs
public class MyEntityContext : Stardust.Paradox.Data.GraphContextBase
{

    static MyEntityContext()
    {
        PartitionKeyName = "pk";//used with partitioned cosmosDb to make sure that ToVerticesAsync uses partitionKey when executing
    }
    public IGraphSet<IPerson> Persons => GraphSet<IPerson>();

    public IGraphSet<ICity> Cities => GraphSet<ICity>();

    public IGraphSet<ICountry> Countries => GraphSet<ICountry>();

    public IGraphSet<ICompany> Companies => GraphSet<ICompany>();

    public IGraphSet<IEmployment> Employments => EdgeGraphSet<IEmployment>();

    public MyEntityContext(IGremlinLanguageConnector connector, IServiceProvider resolver) : base(connector, resolver)
    {
    }

    protected override bool InitializeModel(IGraphConfiguration configuration)
    {
        //Added some fluent configuration of the edges
        configuration.ConfigureCollection<IPerson>()
                .In(t => t.Parents, "parent").Out(t => t.Children)
            .ConfigureCollection<ICity>()
            .ConfigureCollection<ICountry>()
                .AddEdge(t=>t.Cities).Reverse<ICountry>(t=>t.Country)
            .ConfigureCollection<ICompany>()
                .Out(t=>t.Employees, "employer").In(t=>t.Employers)
                .ConfigureCollection<IEmployment>();;
        return true;
    }
}

```
## Using the datacontext

```CS

public class DemoController:Controller
{
    private MyEntityContext _dataContext;
    public DemoController(MyEntityContext dataContext)
    {
        _dataContext=dataContext;
    }

    public Task<IActionResult> GetDataAsync(string personId)
    {
        var person=await _dataContext.Persons.GetAsync(persionId);
        return new User
        {
            Id=person.Id,
            FirstName=person.FistName,
            LastName=person.LastName,
            Email=person.Email
        }
    }

    public Task<IActionResult> AddEmploymentAsync(string userId, string companyId,DateTime hiredDate, string managerName) //new in V2
    {
        var user=await await _dataContext.Persons.GetAsync(persionId);
        var company=await _dataContext.Companies.GetAsync(companyId);
        var e= _dataContext.Employments.Create(user,company);
        e.HiredDate=hiredDate.ToEpoch();
        e.ManagerName=managerName;
        await _dataContext.SaveChangesAsync();
        //alternative
        
    }

    public Task<IActionResult> AddEmploymentAlternativeAsync(string userId, string companyId,DateTime hiredDate, string managerName) //edge property handling in V1
    {
        var user=await await _dataContext.Persons.GetAsync(persionId);
        var company=await _dataContext.Companies.GetAsync(companyId);
        user.Employers.Add(company,new Dictionary<string,object>{
            {"hiredDate",hiredDate.ToEpoch()},
            {"managerName","managerName"}
        })
        await _dataContext.SaveChangesAsync();
        //alternative
        
    }
}

```
## Dynamic graph entities

In many cases we cannot model our graph as strong typed entities. Paradox supports hybrid entities by adding IDynamicGraphEntity to your edge or vertex definition you can model the well known peroperties on your entities, but at the same time assign and manipulate arbitary properties. These properties enjoy the same treatment as the typed properties with regards to parameterization and change tracking.


```CS
[VertexLabel("person")]
    public interface IProfile : IVertex, IDynamicGraphEntity
    {
        string Id { get; }

        string FirstName { get; set; }

        string LastName { get; set; }

        string Email { get; set; }

        bool VerifiedEmail { get; set; }

        string Name { get; set; }

        string Ocupation { get; set; }

        DateTime LastUpdated { get; set; }

        //[EdgeLabel("parent")]
        IEdgeCollection<IProfile> Parents { get; }

        //[ReverseEdgeLabel("parent")]
        IEdgeCollection<IProfile> Children { get; }

        [ToWayEdgeLabel("spouce")]
        IEdgeReference<IProfile> Spouce { get; }

        [Eager]
        [EdgeLabel("employer")]
        ICollection<ICompany> Employers { get; }


        [GremlinQuery("g.V('{id}').as('s').in('parent').out('parent').where(without('s')).dedup()")]
        IEdgeCollection<IProfile> Siblings { get; }

        [InlineSerialization(SerializationType.ClearText)]
        ICollection<string> ProgramingLanguages { get; }

        IEdgeCollection<IProfile> AllSiblings { get; set; }

        bool Adult { get; set; }
        
        string Description { get; set; }
        
        int Number { get; set; }
	    
        string Pk { get; set; }
	    
        EpochDateTime LastUpdatedEpoch { get; set; }
    }
```

### usage

```CS
 var profile = await tc.VAsync<IProfile>("myId");
 profile.SetProperty("someRandomProp",$"test+:{DateTime.UtcNow.Ticks}");
 Console.WriteLine(profile.GetProperty("someRandomProp"));
 Console.WriteLine(string.Join(",",profile.DynamicPropertyNames))
```

## Complex properties

Version 2.3.3 introduces the cocept of complex properties. Gremlin in it self does not support this. Complex properties are serialized to json and needs to be implemented using the IComplexProperty abstract class. For the framework to pick up on changes to the object all properties needs to raise a notification about the change (resharper will help with the implementation of these)

```CS
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
```


## License
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2FJonasSyrstad%2FStardust.Paradox.svg?type=large)](https://app.fossa.io/projects/git%2Bgithub.com%2FJonasSyrstad%2FStardust.Paradox?ref=badge_large)