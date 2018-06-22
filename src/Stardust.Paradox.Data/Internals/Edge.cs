using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data.Internals
{
    internal class Edge<T> : IEdge<T> where T : IVertex
    {

        private string _id;
        private GraphDataEntity _parent;
        private IGraphContext _context;

        public Edge(string id, T vertex, GraphDataEntity parent, IGraphContext context)
        {
            Vertex = vertex;
            _id = id;
            _parent = parent;
            _context = context;
        }

        public T Vertex { get; set; }
        public string EdgeType { get; internal set; }

        public async Task AddToVertexAsync(string label)
        {
            IEnumerable<dynamic> v;
            if (AddReverse)
                v=await _context.ExecuteAsync<T>(g => g.V(ToId).As("t").V(FromId).AddE(ReverseLabel ?? label).To("t"));
            else v = await _context.ExecuteAsync<T>(g => CreateAddEdgeExpression(label, g));
            _id = v.First().id;
        }

        private GremlinQuery CreateAddEdgeExpression(string label, GremlinContext g)
        {
            var expression = g.V(FromId).As("t").V(ToId).AddE(label).To("t");
            //if (AddReverse) return expression.V().V(ToId).As("y").V(FromId).AddE(ReverseLabel??label).To("y");
            return expression;
        }

        private string FromId => _parent._entityKey;

        private string ToId => (Vertex as GraphDataEntity)._entityKey;
        internal bool AddReverse { get; set; }
        internal string ReverseLabel { get; set; }

        public async Task DropEdgeAsync()
        {
            await _context.ExecuteAsync<T>(g => g.V(_parent._entityKey).OutE().HasId(_id).Drop());
        }
    }

    
}