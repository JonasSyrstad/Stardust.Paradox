using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Traversals;
using Stardust.Particles;

#pragma warning disable 168
#pragma warning disable 693
#pragma warning disable 67

namespace Stardust.Paradox.Data.Internals
{
    internal class Edge<T> : IEdge<T> where T : IVertex
    {
        private readonly IGraphContext _context;

        private string _id;

        private string _label;
        private readonly GraphDataEntity _parent;

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

        private string FromId => _parent._entityKey;
        private string FromPk => _parent._partitionKey;

        private string ToId => (Vertex as GraphDataEntity)._entityKey;
        private string ToPk => (Vertex as GraphDataEntity)._partitionKey;

        internal bool AddReverse { get; set; }
        internal string ReverseLabel { get; set; }

        public T Vertex { get; set; }
        public string EdgeType { get; internal set; }
        public IDictionary<string, object> Properties { get; }

        public string Label
        {
            get => _label ?? ReverseLabel;
            internal set => _label = value;
        }

        public event PropertyChangedHandler PropertyChanged;
        public event PropertyChangingHandler PropertyChanging;

        public async Task AddToVertexAsync(string label)
        {
            IEnumerable<dynamic> v;
            if (AddReverse)
                v = await _context.ExecuteAsync<T>(g => CreateReverseExpression(label, g, Properties))
                    .ConfigureAwait(false);
            else
                v = await _context.ExecuteAsync<T>(g => CreateAddEdgeExpression(label, g, Properties))
                    .ConfigureAwait(false);
            _id = v.FirstOrDefault()?.id;
        }

        private GremlinQuery CreateReverseExpression(string label, GremlinContext g,
            IDictionary<string, object> properties)
        {
            GremlinQuery expression;

            if (ToPk.ContainsCharacters())
                expression = g.V(ToId, ToPk).As("s");
            else
                expression = g.V(ToId).As("s");

            if (FromPk.ContainsCharacters())
                expression = expression.V(FromId, FromPk).As("t");
            else
                expression = expression.V(FromId).As("t");

            expression = expression.AddE(label).Property("id", $"{Guid.NewGuid().ToString()}").To("s").From("t");

            foreach (var property in properties) expression = expression.Property(property.Key, property.Value);

            return expression;
        }

        private GremlinQuery CreateAddEdgeExpression(string label, GremlinContext g,
            IDictionary<string, object> properties)
        {
            GremlinQuery expression;

            if (FromPk.ContainsCharacters())
                expression = g.V(FromId, FromPk).As("t");
            else
                expression = g.V(FromId).As("t");

            if (ToPk.ContainsCharacters())
                expression = expression.V(ToId, ToPk).As("s");
            else
                expression = expression.V(ToId).As("s");

            expression = expression.AddE(label).Property("id", $"{Guid.NewGuid().ToString()}").From("s").To("t");

            foreach (var property in properties) expression = expression.Property(property.Key, property.Value);
            return expression;
        }

        public async Task DropEdgeAsync(string label)
        {
            await _context
                .ExecuteAsync<T>(g => g.V(FromId).BothE().HasLabel(label).Where(p => p.OtherV().HasId(ToId)).Drop())
                .ConfigureAwait(false);
        }
    }
}