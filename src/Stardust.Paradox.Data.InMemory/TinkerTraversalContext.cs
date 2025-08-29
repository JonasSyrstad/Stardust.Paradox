using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// Enhanced traversal context inspired by Apache TinkerPop's traversal engine
    /// Fully compatible with TinkerGraph's traversal semantics
    /// </summary>
    public class TinkerTraversalContext
    {
        public List<Traverser> Traversers { get; set; }
        public Dictionary<string, object> Variables { get; set; }
        public Dictionary<string, List<dynamic>> SideEffects { get; set; }
        public Dictionary<string, object> SackInitialValue { get; set; }
        public bool KeepLabels { get; set; }
        public TraversalScope Scope { get; set; }
        
        // Step labels for referencing previous steps
        public Dictionary<string, List<Traverser>> StepLabels { get; set; }
        
        // Barriers for bulk operations
        public Dictionary<string, List<Traverser>> Barriers { get; set; }
        
        // Loops for repeat steps
        public Dictionary<string, int> LoopCounters { get; set; }
        
        // Optimization metadata
        public bool RequiresPath { get; set; }
        public bool RequiresSideEffects { get; set; }

        public TinkerTraversalContext()
        {
            Traversers = new List<Traverser>();
            Variables = new Dictionary<string, object>();
            SideEffects = new Dictionary<string, List<dynamic>>();
            SackInitialValue = new Dictionary<string, object>();
            StepLabels = new Dictionary<string, List<Traverser>>();
            Barriers = new Dictionary<string, List<Traverser>>();
            LoopCounters = new Dictionary<string, int>();
            KeepLabels = false;
            Scope = TraversalScope.Global;
            RequiresPath = false;
            RequiresSideEffects = false;
        }

        public TinkerTraversalContext(IEnumerable<dynamic> initialResults)
        {
            Traversers = initialResults?.Select(r => new Traverser(r)).ToList() ?? new List<Traverser>();
            Variables = new Dictionary<string, object>();
            SideEffects = new Dictionary<string, List<dynamic>>();
            SackInitialValue = new Dictionary<string, object>();
            StepLabels = new Dictionary<string, List<Traverser>>();
            Barriers = new Dictionary<string, List<Traverser>>();
            LoopCounters = new Dictionary<string, int>();
            KeepLabels = false;
            Scope = TraversalScope.Global;
            RequiresPath = false;
            RequiresSideEffects = false;
        }

        /// <summary>
        /// Get current results as enumerable of dynamic objects
        /// </summary>
        public IEnumerable<dynamic> GetCurrentResults()
        {
            foreach (var traverser in Traversers)
            {
                // Handle both single values and enumerable values
                if (traverser.Value is IEnumerable<dynamic> enumerable && !(traverser.Value is string))
                {
                    // If it's already an enumerable (but not a string), flatten it with bulk
                    for (int i = 0; i < traverser.Bulk; i++)
                    {
                        foreach (var item in enumerable)
                        {
                            yield return item;
                        }
                    }
                }
                else
                {
                    // If it's a single value, repeat it according to bulk
                    for (int i = 0; i < traverser.Bulk; i++)
                    {
                        yield return traverser.Value;
                    }
                }
            }
        }

        /// <summary>
        /// Add a step label for later reference (optimized for TinkerGraph)
        /// </summary>
        public void AddStepLabel(string label)
        {
            if (!string.IsNullOrEmpty(label))
            {
                StepLabels[label] = Traversers.Select(t => t.Split()).ToList();
                
                // Tag traversers with the label
                foreach (var traverser in Traversers)
                {
                    traverser.Tags[label] = traverser.Value;
                }
            }
        }

        /// <summary>
        /// Get traversers for a specific step label
        /// </summary>
        public IEnumerable<Traverser> GetLabeledTraversers(string label)
        {
            return StepLabels.TryGetValue(label, out var labeled) ? labeled : Enumerable.Empty<Traverser>();
        }

        /// <summary>
        /// Add to side effects with proper TinkerGraph semantics
        /// </summary>
        public void AddSideEffect(string key, dynamic value)
        {
            if (!SideEffects.ContainsKey(key))
            {
                SideEffects[key] = new List<dynamic>();
            }
            SideEffects[key].Add(value);
            RequiresSideEffects = true;
        }

        /// <summary>
        /// Filter traversers based on a predicate with bulk optimization
        /// </summary>
        public void Filter(Func<Traverser, bool> predicate)
        {
            var filtered = new List<Traverser>();
            
            foreach (var traverser in Traversers)
            {
                if (predicate(traverser))
                {
                    filtered.Add(traverser);
                }
            }
            
            Traversers = filtered;
        }

        /// <summary>
        /// Transform traversers with TinkerGraph-compatible mapping
        /// </summary>
        public void Map<TResult>(Func<Traverser, IEnumerable<TResult>> mapper)
        {
            var newTraversers = new List<Traverser>();
            
            foreach (var traverser in Traversers)
            {
                var mapped = mapper(traverser);
                foreach (var result in mapped)
                {
                    var newTraverser = new Traverser(result)
                    {
                        Bulk = traverser.Bulk,
                        Path = RequiresPath ? new List<dynamic>(traverser.Path) : new List<dynamic>(),
                        Sack = new Dictionary<string, object>(traverser.Sack),
                        SideEffects = RequiresSideEffects ? new Dictionary<string, object>(traverser.SideEffects) : new Dictionary<string, object>(),
                        Loops = new Dictionary<string, int>(traverser.Loops),
                        Tags = new Dictionary<string, dynamic>(traverser.Tags)
                    };
                    
                    if (RequiresPath)
                    {
                        newTraverser.AddToPath(result);
                    }
                    
                    newTraversers.Add(newTraverser);
                }
            }
            
            Traversers = newTraversers;
        }

        /// <summary>
        /// Flat map traversers to handle nested collections with TinkerGraph semantics
        /// </summary>
        public void FlatMap<TResult>(Func<Traverser, IEnumerable<TResult>> mapper)
        {
            var newTraversers = new List<Traverser>();
            
            foreach (var traverser in Traversers)
            {
                var mapped = mapper(traverser);
                foreach (var result in mapped)
                {
                    var newTraverser = traverser.Split();
                    newTraverser.Value = result;
                    
                    if (RequiresPath)
                    {
                        newTraverser.AddToPath(result);
                    }
                    
                    newTraversers.Add(newTraverser);
                }
            }
            
            Traversers = newTraversers;
        }

        /// <summary>
        /// Group traversers by a key function with bulk handling
        /// </summary>
        public Dictionary<TKey, List<Traverser>> GroupBy<TKey>(Func<Traverser, TKey> keySelector)
        {
            return Traversers.GroupBy(keySelector).ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Deduplicate traversers with TinkerGraph semantics
        /// </summary>
        public void Dedup(Func<Traverser, object> keySelector = null)
        {
            if (keySelector == null)
            {
                keySelector = t => t.Value;
            }

            var seen = new Dictionary<object, Traverser>();

            foreach (var traverser in Traversers)
            {
                var key = keySelector(traverser);
                
                if (seen.TryGetValue(key, out var existing))
                {
                    // Merge bulk counts for duplicate values
                    existing.Bulk += traverser.Bulk;
                }
                else
                {
                    seen[key] = traverser.Split();
                }
            }

            Traversers = seen.Values.ToList();
        }

        /// <summary>
        /// Limit the number of traversers with bulk optimization
        /// </summary>
        public void Limit(long limit)
        {
            if (limit <= 0)
            {
                Traversers.Clear();
                return;
            }

            var limited = new List<Traverser>();
            long count = 0;

            foreach (var traverser in Traversers)
            {
                if (count >= limit)
                    break;

                if (count + traverser.Bulk <= limit)
                {
                    limited.Add(traverser);
                    count += traverser.Bulk;
                }
                else
                {
                    var remaining = limit - count;
                    var newTraverser = traverser.Split();
                    newTraverser.Bulk = remaining;
                    limited.Add(newTraverser);
                    count = limit;
                }
            }

            Traversers = limited;
        }

        /// <summary>
        /// Skip a number of traversers with bulk optimization
        /// </summary>
        public void Skip(long skip)
        {
            if (skip <= 0)
                return;

            var skipped = new List<Traverser>();
            long count = 0;

            foreach (var traverser in Traversers)
            {
                if (count + traverser.Bulk <= skip)
                {
                    count += traverser.Bulk;
                    continue;
                }

                if (count < skip)
                {
                    var remaining = traverser.Bulk - (skip - count);
                    var newTraverser = traverser.Split();
                    newTraverser.Bulk = remaining;
                    skipped.Add(newTraverser);
                }
                else
                {
                    skipped.Add(traverser);
                }

                count += traverser.Bulk;
            }

            Traversers = skipped;
        }

        /// <summary>
        /// Range of traversers (skip + limit combined) with TinkerGraph optimization
        /// </summary>
        public void Range(long low, long high)
        {
            Skip(low);
            Limit(high - low);
        }

        /// <summary>
        /// Sample traversers randomly with TinkerGraph semantics
        /// </summary>
        public void Sample(int amountToSample)
        {
            if (amountToSample <= 0)
            {
                Traversers.Clear();
                return;
            }

            if (amountToSample >= Traversers.Count)
                return;

            var random = new Random();
            var sampled = Traversers.OrderBy(x => random.Next()).Take(amountToSample).ToList();
            Traversers = sampled;
        }

        /// <summary>
        /// Apply a barrier operation for bulk processing
        /// </summary>
        public void Barrier(string barrierName = "barrier")
        {
            if (!Barriers.ContainsKey(barrierName))
            {
                Barriers[barrierName] = new List<Traverser>();
            }
            
            Barriers[barrierName].AddRange(Traversers.Select(t => t.Split()));
        }

        /// <summary>
        /// Release barrier and continue processing
        /// </summary>
        public void ReleaseBarrier(string barrierName = "barrier")
        {
            if (Barriers.TryGetValue(barrierName, out var barrierTraversers))
            {
                Traversers = barrierTraversers;
                Barriers.Remove(barrierName);
            }
        }

        /// <summary>
        /// Initialize sack for all traversers
        /// </summary>
        public void InitializeSack(string key, object initialValue)
        {
            SackInitialValue[key] = initialValue;
            
            foreach (var traverser in Traversers)
            {
                traverser.SetSack(key, initialValue);
            }
        }

        /// <summary>
        /// Apply sack operation to all traversers
        /// </summary>
        public void ApplySackOperation(string key, Func<object, object, object> operation, object operand)
        {
            foreach (var traverser in Traversers)
            {
                var currentValue = traverser.GetSack<object>(key);
                var newValue = operation(currentValue, operand);
                traverser.SetSack(key, newValue);
            }
        }

        /// <summary>
        /// Start a repeat loop
        /// </summary>
        public void StartRepeat(string label)
        {
            LoopCounters[label] = 0;
            
            foreach (var traverser in Traversers)
            {
                traverser.ResetLoops(label);
            }
        }

        /// <summary>
        /// Check repeat condition and increment loop counters
        /// </summary>
        public bool CheckRepeatCondition(string label, int maxLoops, Func<Traverser, bool> whileCondition = null)
        {
            bool hasActiveTraversers = false;
            var activeTraversers = new List<Traverser>();
            
            foreach (var traverser in Traversers)
            {
                var loops = traverser.IncrementLoops(label);
                
                bool shouldContinue = loops < maxLoops;
                if (whileCondition != null)
                {
                    shouldContinue = shouldContinue && whileCondition(traverser);
                }
                
                if (shouldContinue)
                {
                    activeTraversers.Add(traverser);
                    hasActiveTraversers = true;
                }
            }
            
            Traversers = activeTraversers;
            return hasActiveTraversers;
        }

        /// <summary>
        /// Create a copy of the context with TinkerGraph semantics
        /// </summary>
        public TinkerTraversalContext Clone()
        {
            var clone = new TinkerTraversalContext
            {
                Traversers = Traversers.Select(t => t.Split()).ToList(),
                Variables = new Dictionary<string, object>(Variables),
                SideEffects = SideEffects.ToDictionary(kvp => kvp.Key, kvp => new List<dynamic>(kvp.Value)),
                SackInitialValue = new Dictionary<string, object>(SackInitialValue),
                StepLabels = StepLabels.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(t => t.Split()).ToList()),
                Barriers = Barriers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(t => t.Split()).ToList()),
                LoopCounters = new Dictionary<string, int>(LoopCounters),
                KeepLabels = KeepLabels,
                Scope = Scope,
                RequiresPath = RequiresPath,
                RequiresSideEffects = RequiresSideEffects
            };

            return clone;
        }

        /// <summary>
        /// Optimize traverser bulk by merging identical values
        /// </summary>
        public void OptimizeBulk()
        {
            var bulkMap = new Dictionary<dynamic, Traverser>();
            
            foreach (var traverser in Traversers)
            {
                var key = traverser.Value;
                
                if (bulkMap.TryGetValue(key, out Traverser existing))
                {
                    existing.Merge(traverser);
                }
                else
                {
                    bulkMap[key] = traverser.Split();
                }
            }
            
            Traversers = bulkMap.Values.ToList();
        }

        /// <summary>
        /// Check if context has any traversers
        /// </summary>
        public bool HasTraversers => Traversers.Any();

        /// <summary>
        /// Count total elements considering bulk
        /// </summary>
        public long Count => Traversers.Sum(t => t.Bulk);

        /// <summary>
        /// Clear all traversers
        /// </summary>
        public void Clear()
        {
            Traversers.Clear();
        }

        /// <summary>
        /// Get statistics about the traversal context
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["traverserCount"] = Traversers.Count,
                ["totalBulk"] = Count,
                ["averageBulk"] = Traversers.Any() ? (double)Count / Traversers.Count : 0,
                ["hasPath"] = RequiresPath,
                ["hasSideEffects"] = RequiresSideEffects,
                ["labelCount"] = StepLabels.Count,
                ["barrierCount"] = Barriers.Count,
                ["activeLoops"] = LoopCounters.Count
            };
        }
    }
}