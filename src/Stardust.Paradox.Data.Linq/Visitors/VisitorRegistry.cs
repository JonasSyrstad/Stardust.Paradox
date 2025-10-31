using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Stardust.Paradox.Data.Linq.Visitors
{
    /// <summary>
    /// Registry for discovering and managing expression visitors
    /// Visitors are loaded dynamically on first use
    /// </summary>
    public class VisitorRegistry
    {
private static readonly Lazy<VisitorRegistry> _instance = 
       new Lazy<VisitorRegistry>(() => new VisitorRegistry());

        private readonly Dictionary<string, List<IExpressionVisitor>> _visitorsByMethod;
        private readonly List<IExpressionVisitor> _allVisitors;

        public static VisitorRegistry Instance => _instance.Value;

        private VisitorRegistry()
        {
            _visitorsByMethod = new Dictionary<string, List<IExpressionVisitor>>();
   _allVisitors = new List<IExpressionVisitor>();
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
        /// Registers a visitor instance
        /// </summary>
        public void RegisterVisitor(IExpressionVisitor visitor)
        {
     _allVisitors.Add(visitor);

  if (!_visitorsByMethod.ContainsKey(visitor.MethodName))
            {
 _visitorsByMethod[visitor.MethodName] = new List<IExpressionVisitor>();
            }

 _visitorsByMethod[visitor.MethodName].Add(visitor);
    
            // Sort by priority (lower first)
  _visitorsByMethod[visitor.MethodName] = _visitorsByMethod[visitor.MethodName]
    .OrderBy(v => v.Priority)
      .ToList();
        }

     /// <summary>
        /// Finds a visitor for the given method call expression
        /// </summary>
        public IExpressionVisitor FindVisitor(MethodCallExpression node, IVisitorContext context)
 {
            var methodName = node.Method.Name;

   // First try exact method name match
       if (_visitorsByMethod.TryGetValue(methodName, out var visitors))
      {
    foreach (var visitor in visitors)
            {
          if (visitor.CanVisit(node, context))
           {
        return visitor;
 }
           }
        }

 // If no exact match, check all visitors (for multi-method visitors)
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
    }
}
