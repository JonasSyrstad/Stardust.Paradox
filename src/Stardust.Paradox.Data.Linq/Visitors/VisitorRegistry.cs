using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Registry for discovering and managing expression visitors with optimized lookup
    /// Visitors are loaded dynamically on first use
    /// </summary>
    public class VisitorRegistry
    {
  private static readonly Lazy<VisitorRegistry> _instance = 
      new Lazy<VisitorRegistry>(() => new VisitorRegistry());

// Optimized dictionary with tuple keys: (DeclaringType, MethodName) for O(1) lookup
        private readonly Dictionary<(Type, string), List<IExpressionVisitor>> _visitorMap;
        private readonly Dictionary<string, List<IExpressionVisitor>> _visitorsByMethod;
        private readonly List<IExpressionVisitor> _allVisitors;

        public static VisitorRegistry Instance => _instance.Value;

        private VisitorRegistry()
      {
            _visitorMap = new Dictionary<(Type, string), List<IExpressionVisitor>>(128);
        _visitorsByMethod = new Dictionary<string, List<IExpressionVisitor>>(64);
            _allVisitors = new List<IExpressionVisitor>(64);
      DiscoverVisitors();
    }

        /// <summary>
        /// Discovers all visitor implementations in the current assembly
        /// </summary>
        private void DiscoverVisitors()
{
      var visitorType = typeof(IExpressionVisitor);
    var assembly = visitorType.Assembly;

          var visitorTypes = assembly.GetTypes()
       .Where(t => !t.IsAbstract && !t.IsInterface && visitorType.IsAssignableFrom(t))
          .ToList();

        foreach (var type in visitorTypes)
            {
       try
        {
     var visitor = (IExpressionVisitor)Activator.CreateInstance(type);
   RegisterVisitor(visitor);
        }
                catch (Exception ex)
         {
             // Log or handle visitor creation failure
   System.Diagnostics.Debug.WriteLine($"Failed to create visitor {type.Name}: {ex.Message}");
  }
       }
        }

        /// <summary>
        /// Registers a visitor instance and builds optimized lookup tables
        /// </summary>
  public void RegisterVisitor(IExpressionVisitor visitor)
        {
            _allVisitors.Add(visitor);

     // Register by method name only
 if (!_visitorsByMethod.ContainsKey(visitor.MethodName))
{
    _visitorsByMethod[visitor.MethodName] = new List<IExpressionVisitor>();
            }
         _visitorsByMethod[visitor.MethodName].Add(visitor);

         // Register in optimized map with (Type, MethodName) tuple key
// Most visitors target GraphTraversalExtensions or Queryable/Enumerable
            var targetTypes = new[] 
            { 
      typeof(GraphTraversalExtensions),
    typeof(Queryable),
     typeof(Enumerable)
            };

      foreach (var targetType in targetTypes)
      {
        var key = (targetType, visitor.MethodName);
      if (!_visitorMap.ContainsKey(key))
   {
    _visitorMap[key] = new List<IExpressionVisitor>();
  }
                _visitorMap[key].Add(visitor);
}

          // Sort by priority (lower number = higher priority, executes first)
         foreach (var key in _visitorMap.Keys.ToList())
      {
                _visitorMap[key] = _visitorMap[key].OrderBy(v => v.Priority).ToList();
          }

  foreach (var key in _visitorsByMethod.Keys.ToList())
          {
       _visitorsByMethod[key] = _visitorsByMethod[key].OrderBy(v => v.Priority).ToList();
            }
        }

        /// <summary>
     /// Finds a visitor for the given method call expression using optimized O(1) lookup
        /// </summary>
        public IExpressionVisitor FindVisitor(MethodCallExpression node, IVisitorContext context)
        {
       var methodName = node.Method.Name;
            var declaringType = node.Method.DeclaringType;

        // Fast path: Try optimized tuple-based lookup first
    if (declaringType != null)
      {
             var key = (declaringType, methodName);
         if (_visitorMap.TryGetValue(key, out var visitors))
    {
     foreach (var visitor in visitors)
       {
            if (visitor.CanVisit(node, context))
      {
       return visitor;
    }
           }
     }
            }

      // Fallback path: Try method name only
  if (_visitorsByMethod.TryGetValue(methodName, out var methodVisitors))
   {
  foreach (var visitor in methodVisitors)
     {
   if (visitor.CanVisit(node, context))
      {
    return visitor;
             }
         }
      }

            // Last resort: Check all visitors (for custom multi-method visitors)
            foreach (var visitor in _allVisitors)
   {
         if (visitor.CanVisit(node, context))
       {
              return visitor;
          }
            }

       return null;
     }

        /// <summary>
        /// Gets all registered visitors
        /// </summary>
        public IEnumerable<IExpressionVisitor> GetAllVisitors()
        {
         return _allVisitors;
 }

        /// <summary>
        /// Gets statistics about the visitor registry (for debugging/monitoring)
        /// </summary>
        public (int TotalVisitors, int MethodMappings, int OptimizedMappings) GetStatistics()
        {
return (_allVisitors.Count, _visitorsByMethod.Count, _visitorMap.Count);
        }
    }
}
