using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Traversals;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Stardust.Paradox.Data
{
	public interface IGraphSet<T> : IGraphSetBase<T> where T : IGraphEntity
    {
	    Task<T> GetAsync(string id, string partitionKey);

	    Task<T> GetAsync((string, string) idAndPartitionKey);

		Task DeleteAsync(string id, string partitionKey);
	    Task DeleteAsync((string, string) idAndPartitionKey);

		/// <summary>
		/// Creates a new item with a Guid as id
		/// </summary>
		/// <returns></returns>
		T Create();

        T Create(string id);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="initializer"></param>
        /// <returns></returns>
        T Create(string id, Func<T, T> initializer);

        /// <summary>
        /// Creates a new item with a Guid as id
        /// </summary>
        /// <returns></returns>
        T Create(Func<T, T> initializer);
    }
    public interface IGraphSetBase<T> where T : IGraphEntity
    {
        Task DeleteAsync(string id);
        Task<IEnumerable<T>> GetAsync(int page, int pageSize = 20);
        Task<IEnumerable<T>> GetAsync(Func<GremlinContext, GremlinQuery> query, int page, int pageSize = 20);
        Task<IEnumerable<T>> GetAsync(Func<GremlinContext, GremlinQuery> query);
        Task<T> GetAsync(string id);

	    

		Task<IEnumerable<T>> FilterAsync(Expression<Func<T, object>> byProperty, string hasValue);

        Task<IEnumerable<T>> FilterAsync(Expression<Func<T, object>> byProperty, string hasValue, int page, int pageSize = 20);


        Task<IEnumerable<T>> AllAsync();

        Task<IEnumerable<T>> AllAsync(int page, int pageSize = 20);
       

        void Attach(T item);
    }
}