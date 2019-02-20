using System;
using System.Collections.Generic;
using Stardust.Paradox.Data;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Internals;

namespace Stardust.Paradox.CosmosDbTest
{
    [VertexLabel("company")]
    public interface ICompany : IVertex
    {
        string Id { get; }

        string Name { get; set; }

        [ReverseEdgeLabel("division")]
        IEdgeReference<ICompany> Parent { get; }

        [EdgeLabel("division")]
        IEdgeCollection<ICompany> Divisions { get; }

        [ReverseEdgeLabel("employer")]
        IEdgeCollection<IProfile> Employees { get; }

        [GremlinQuery("g.V('{id}').out('division').tail(1)")]
        IEdgeReference<ICompany> Group { get; }

        [GremlinQuery("g.V('{id}').emit().repeat(inE('division').outV()).out('employer')")]
        IEdgeCollection<IProfile> AllEmployees { get; }

        [InlineSerialization(SerializationType.Base64)]
        IInlineCollection<string> EmailDomains { get; }
	    string Pk { get; set; }
	}

    [VertexLabel("person")]
    public interface IProfile : IVertex
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
    }


}