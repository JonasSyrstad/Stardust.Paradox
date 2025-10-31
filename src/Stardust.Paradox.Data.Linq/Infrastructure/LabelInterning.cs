using System;
using System.Collections.Concurrent;

namespace Stardust.Paradox.Data.Linq.Infrastructure
{
    /// <summary>
    /// Provides string interning for frequently-used graph labels to reduce memory overhead
    /// </summary>
    internal static class LabelInterning
    {
      private static readonly ConcurrentDictionary<string, string> _internedLabels =
    new ConcurrentDictionary<string, string>(
        concurrencyLevel: Environment.ProcessorCount,
       capacity: 64);

        /// <summary>
     /// Interns a label string. Frequently used labels will share the same string instance.
        /// </summary>
        /// <param name="label">The label to intern</param>
      /// <returns>The interned string instance</returns>
   public static string InternLabel(string label)
      {
          if (string.IsNullOrEmpty(label))
            return label;

            return _internedLabels.GetOrAdd(label, s => s);
        }

        /// <summary>
    /// Gets statistics about the interning cache (for monitoring/debugging)
     /// </summary>
        public static int GetInternedCount() => _internedLabels.Count;

        /// <summary>
        /// Clears the interning cache. Use with caution - mainly for testing.
        /// </summary>
        public static void ClearCache()
        {
  _internedLabels.Clear();
        }
    }
}
