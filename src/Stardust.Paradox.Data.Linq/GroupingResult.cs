using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Implementation of IGrouping for GroupBy results
    /// </summary>
    internal class GroupingResult<TKey, TElement> : IGrouping<TKey, TElement>
    {
      private readonly TKey _key;
        private readonly IEnumerable<TElement> _elements;

        public GroupingResult(TKey key, IEnumerable<TElement> elements)
        {
     _key = key;
    _elements = elements ?? Enumerable.Empty<TElement>();
        }

 public TKey Key => _key;

        public IEnumerator<TElement> GetEnumerator()
        {
            return _elements.GetEnumerator();
        }

  IEnumerator IEnumerable.GetEnumerator()
 {
            return GetEnumerator();
        }
    }
}
