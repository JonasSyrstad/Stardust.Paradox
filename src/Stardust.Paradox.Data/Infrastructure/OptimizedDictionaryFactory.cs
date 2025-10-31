using System;
using System.Collections.Concurrent;

namespace Stardust.Paradox.Data.Infrastructure
{
    /// <summary>
    /// Factory for creating optimized ConcurrentDictionary instances with proper concurrency levels and capacity.
    /// </summary>
internal static class OptimizedDictionaryFactory
    {
        private static readonly int DefaultConcurrencyLevel = Environment.ProcessorCount * 2;

        /// <summary>
        /// Creates a small-capacity ConcurrentDictionary optimized for metadata caching (32-128 items).
    /// </summary>
        public static ConcurrentDictionary<TKey, TValue> CreateSmallCache<TKey, TValue>()
        {
       return new ConcurrentDictionary<TKey, TValue>(
             concurrencyLevel: DefaultConcurrencyLevel,
           capacity: 64);
        }

        /// <summary>
        /// Creates a medium-capacity ConcurrentDictionary optimized for property caching (128-512 items).
        /// </summary>
     public static ConcurrentDictionary<TKey, TValue> CreateMediumCache<TKey, TValue>()
      {
            return new ConcurrentDictionary<TKey, TValue>(
         concurrencyLevel: DefaultConcurrencyLevel,
        capacity: 256);
        }

        /// <summary>
      /// Creates a large-capacity ConcurrentDictionary optimized for entity tracking (512+ items).
        /// </summary>
        public static ConcurrentDictionary<TKey, TValue> CreateLargeCache<TKey, TValue>()
     {
         return new ConcurrentDictionary<TKey, TValue>(
concurrencyLevel: DefaultConcurrencyLevel,
       capacity: 1024);
        }

        /// <summary>
        /// Creates a ConcurrentDictionary with custom concurrency level and capacity.
        /// </summary>
        public static ConcurrentDictionary<TKey, TValue> CreateCustom<TKey, TValue>(
  int estimatedCapacity,
        int? concurrencyLevel = null)
        {
        return new ConcurrentDictionary<TKey, TValue>(
     concurrencyLevel: concurrencyLevel ?? DefaultConcurrencyLevel,
     capacity: estimatedCapacity);
    }

        /// <summary>
      /// Gets the recommended concurrency level for the current system.
        /// </summary>
        public static int GetRecommendedConcurrencyLevel()
        {
            return DefaultConcurrencyLevel;
        }

        /// <summary>
        /// Calculates the next power of 2 for optimal hashtable sizing.
  /// </summary>
  public static int GetOptimalCapacity(int estimatedSize)
        {
            if (estimatedSize <= 32) return 32;
            if (estimatedSize <= 64) return 64;
            if (estimatedSize <= 128) return 128;
    if (estimatedSize <= 256) return 256;
     if (estimatedSize <= 512) return 512;
            if (estimatedSize <= 1024) return 1024;
 if (estimatedSize <= 2048) return 2048;
            
 // For larger sizes, round up to next power of 2
   int power = 1;
            while (power < estimatedSize)
    {
   power <<= 1;
       }
            return power;
        }
    }
}
