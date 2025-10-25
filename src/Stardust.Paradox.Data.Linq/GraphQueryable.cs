using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// IQueryable implementation for graph entities
 /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    internal class GraphQueryable<T> : IOrderedQueryable<T>
    {
      public GraphQueryable(GremlinQueryProvider provider)
        {
       Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Expression = Expression.Constant(this);
      }

 public GraphQueryable(GremlinQueryProvider provider, Expression expression)
        {
      Provider = provider ?? throw new ArgumentNullException(nameof(provider));
  Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

      public Type ElementType => typeof(T);

 public Expression Expression { get; }

        public IQueryProvider Provider { get; }

        public IEnumerator<T> GetEnumerator()
        {
   var result = Provider.Execute<IEnumerable<T>>(Expression);
            return result.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
    return GetEnumerator();
     }
    }
}
