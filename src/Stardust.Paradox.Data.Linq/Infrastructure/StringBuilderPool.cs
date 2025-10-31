using System;
using System.Text;

namespace Stardust.Paradox.Data.Linq.Infrastructure
{
    /// <summary>
    /// Simple StringBuilder pool for reducing allocations in query building
    /// Compatible with .NET Standard 2.0
    /// </summary>
    internal static class StringBuilderPool
    {
        private const int MaxPoolSize = 10;
     private const int MaxBuilderCapacity = 2048;
        
     [ThreadStatic]
        private static StringBuilder[] _pool;
     
    [ThreadStatic]
      private static int _poolIndex;

    /// <summary>
 /// Gets a StringBuilder from the pool or creates a new one
        /// </summary>
        public static StringBuilder Get()
       {
   if (_pool == null)
    {
   _pool = new StringBuilder[MaxPoolSize];
  _poolIndex = 0;
         }

   if (_poolIndex > 0)
            {
         var sb = _pool[--_poolIndex];
       _pool[_poolIndex] = null;
       return sb;
    }

        return new StringBuilder(256);
  }

  /// <summary>
     /// Returns a StringBuilder to the pool after clearing it
  /// </summary>
        public static void Return(StringBuilder sb)
      {
  if (sb == null)
    return;

          // Don't pool if capacity is too large
            if (sb.Capacity > MaxBuilderCapacity)
   return;

     sb.Clear();

     if (_pool == null)
       {
    _pool = new StringBuilder[MaxPoolSize];
      _poolIndex = 0;
        }

  if (_poolIndex < MaxPoolSize)
      {
      _pool[_poolIndex++] = sb;
            }
        }
}
}
