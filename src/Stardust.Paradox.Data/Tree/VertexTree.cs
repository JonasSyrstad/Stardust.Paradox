using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Annotations.DataTypes;

namespace Stardust.Paradox.Data.Tree
{
    [JsonObject]
    public class VertexTree<T> : VertexTreeRoot<T>, IVertexTree<T>, IVertexTree where T : IVertex
    {
        private GraphContextBase _graphContext;

        public VertexTree(IEnumerable<VertexTree<T>> items) : base(items)
        {
        }

        public VertexTree()
        {
        }


        internal VertexTree(JObject items, IGraphContext context) : base(items, context)
        {
            _graphContext = context as GraphContextBase;
        }

        //public VertexTree(JObject items) : base()
        //{

        //}

        public VertexTree(KeyValuePair<string, JToken> items)
        {
        }

        public VertexTree(JToken iValue, IGraphContext context) : base(context)
        {
            _graphContext = context as GraphContextBase;
            var key = iValue.First.First.ToObject<object>();
            Key = _graphContext.MakeInstance<T>(key as dynamic);
            foreach (var item in iValue.Last)
            foreach (JProperty i in item)
                _children.Add(new VertexTree<T>(i.Value, _context));
        }

        [JsonProperty("key", DefaultValueHandling = DefaultValueHandling.Include)]
        public T Key { get; }

        List<IVertexTree> IVertexTree.ToList()
        {
            return _children.Select(i => i as IVertexTree).ToList();
        }

        object IVertexTree.GetKey()
        {
            return Key;
        }
    }
}