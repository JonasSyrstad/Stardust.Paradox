using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// Extension methods for LINQ operations not available in .NET Standard 2.0
    /// </summary>
    internal static class LinqExtensions
    {
        /// <summary>
        /// Takes the last N elements from a sequence (not available in .NET Standard 2.0)
        /// </summary>
        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int count)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (count <= 0)
                return Enumerable.Empty<T>();

            var list = source.ToList();
            var startIndex = Math.Max(0, list.Count - count);
            return list.Skip(startIndex);
        }

        /// <summary>
        /// Creates a HashSet from an enumerable (not available in .NET Standard 2.0)
        /// </summary>
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return new HashSet<T>(source);
        }

        /// <summary>
        /// Gets a value from dictionary or returns default if key doesn't exist
        /// </summary>
        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            if (dictionary == null)
                throw new ArgumentNullException(nameof(dictionary));

            return dictionary.TryGetValue(key, out TValue value) ? value : defaultValue;
        }

        /// <summary>
        /// Safe Count method for dynamic enumerable
        /// </summary>
        public static int SafeCount(this IEnumerable<dynamic> source)
        {
            if (source == null)
                return 0;

            var count = 0;
            foreach (var item in source)
            {
                count++;
            }
            return count;
        }

        /// <summary>
        /// Safe First method for dynamic enumerable
        /// </summary>
        public static dynamic SafeFirst(this IEnumerable<dynamic> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            foreach (var item in source)
            {
                return item;
            }
            throw new InvalidOperationException("Sequence contains no elements");
        }

        /// <summary>
        /// Safe FirstOrDefault method for dynamic enumerable
        /// </summary>
        public static dynamic SafeFirstOrDefault(this IEnumerable<dynamic> source)
        {
            if (source == null)
                return null;

            foreach (var item in source)
            {
                return item;
            }
            return null;
        }

        public static T CastAs<T>(this object value)
        {
            return (T)value;
        }
    }
    
    
}