using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Internals
{
	internal class TypedEdgeCollection<TIn, TOut> : IEdgeCollection<TIn, TOut> where TOut : IVertex where TIn : IVertex
	{
		public int Count => throw new NotImplementedException();

		public bool IsReadOnly => throw new NotImplementedException();

		public void Add(IEdge<TIn, TOut> item)
		{
			throw new NotImplementedException();
		}

		public Task<IEnumerable<IEdge<TIn, TOut>>> AsEnumerableAsync()
		{
			throw new NotImplementedException();
		}

		public void Clear()
		{
			throw new NotImplementedException();
		}

		public bool Contains(IEdge<TIn, TOut> item)
		{
			throw new NotImplementedException();
		}

		public void CopyTo(IEdge<TIn, TOut>[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		public IEnumerator<IEdge<TIn, TOut>> GetEnumerator()
		{
			throw new NotImplementedException();
		}

		public Task<IEnumerable<TIn>> InVerticesAsync()
		{
			throw new NotImplementedException();
		}

		public Task LoadAsync()
		{
			throw new NotImplementedException();
		}

		public Task<IEnumerable<TOut>> OutVerticesAsync()
		{
			throw new NotImplementedException();
		}

		public bool Remove(IEdge<TIn, TOut> item)
		{
			throw new NotImplementedException();
		}

		public Task SaveChangesAsync()
		{
			throw new NotImplementedException();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException();
		}
	}
}