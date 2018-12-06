using System;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Traversals;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            Properties = new Dictionary<string, object>();
        }

        public Edge(string id, GraphDataEntity parent, IGraphContext context)
        {
            _id = id;
            _parent = parent;
            _context = context;
            Properties = new Dictionary<string, object>();
        }

        public T Vertex { get; set; }
        public string EdgeType { get; internal set; }
        public IDictionary<string, object> Properties { get; }

        public async Task AddToVertexAsync(string label)
        {
            IEnumerable<dynamic> v;
            if (AddReverse)
                v = await _context.ExecuteAsync<T>(g => CreateReverseExpression(label, g, Properties)).ConfigureAwait(false);
            else v = await _context.ExecuteAsync<T>(g => CreateAddEdgeExpression(label, g, Properties)).ConfigureAwait(false);
            _id = v.FirstOrDefault()?.id;
        }

        private GremlinQuery CreateReverseExpression(string label, GremlinContext g, IDictionary<string, object> properties)
        {
            try
            {
                var expression = g.V(ToId).As("s").V(FromId).As("t").AddE(label).Property("id", $"{Guid.NewGuid().ToString()}").To("s").From("t");
                foreach (var property in properties)
                {
                    expression = expression.Property(property.Key, property.Value);
                }

                return expression;
            }
            catch (System.Exception ex)
            {

                throw;
            }
        }

        private GremlinQuery CreateAddEdgeExpression(string label, GremlinContext g, IDictionary<string, object> properties)
        {
            try
            {
                var expression = g.V(FromId).As("t").V(ToId).As("s").AddE(label).Property("id", $"{Guid.NewGuid().ToString()}").From("s").To("t");
                foreach (var property in properties)
                {
                    expression = expression.Property(property.Key, property.Value);
                }
                return expression;
            }
            catch (System.Exception ex)
            {

                throw;
            }
        }

        private string FromId => _parent._entityKey;

        private string ToId => (Vertex as GraphDataEntity)._entityKey;
        internal bool AddReverse { get; set; }
        internal string ReverseLabel { get; set; }

        public async Task DropEdgeAsync(string label)
        {
            await _context.ExecuteAsync<T>(g => g.V(FromId).BothE().HasLabel(label).Where(p => p.OtherV().HasId(ToId)).Drop()).ConfigureAwait(false);
        }

        public string Label
        {
            get { return Label ?? ReverseLabel; }
        }

        public event PropertyChangedHandler PropertyChanged;
        public event PropertyChangingHandler PropertyChanging;
    }


}