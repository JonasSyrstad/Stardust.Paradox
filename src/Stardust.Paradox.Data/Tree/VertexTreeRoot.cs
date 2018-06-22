using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stardust.Paradox.Data.Tree
{
    public class VertexTreeRoot<T> : IVertexTreeRoot<T> where T : IVertex
    {
        protected readonly IGraphContext _context;

        public VertexTreeRoot(IEnumerable<VertexTree<T>> items)
        {
            _children = items.ToList();
        }

        internal VertexTreeRoot(IEnumerable<dynamic> items,IGraphContext context)
        {
            _context = context;
            foreach (var item in items)
            {
                foreach (JProperty i in item)
                {
                    _children.Add(new VertexTree<T>(i.Value,_context)); 
                }
            }
        }
        protected List<VertexTree<T>> _children = new List<VertexTree<T>>();

        public VertexTreeRoot()
        {
        }

        protected VertexTreeRoot(IGraphContext context)
        {
            _context = context;
        }
        
        public VertexTree<T> this[int index] => _children[index];
        public IEnumerator<VertexTree<T>> GetEnumerator()
        {
            return _children.GetEnumerator();
        }

        [JsonProperty("values",DefaultValueHandling = DefaultValueHandling.Include)]
        internal IEnumerable<VertexTree<T>> Values => _children;

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}