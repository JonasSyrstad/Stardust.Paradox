using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Fluent graph traversal API for building Gremlin queries
    /// </summary>
    /// <typeparam name="T">The vertex or edge type</typeparam>
    public class GraphTraversal<T> where T : IGraphEntity
    {
        private readonly List<string> _steps = new List<string>();

        public GraphTraversal()
        {
            // Constructor allows object creation for tests
        }

        internal GraphTraversal(List<string> steps)
        {
            _steps = new List<string>(steps);
        }

        internal void AddStep(string step)
        {
            _steps.Add(step);
        }

        internal List<string> GetSteps()
        {
            return new List<string>(_steps);
        }

        public string ToGremlinQuery()
        {
            if (_steps.Count == 0)
                return "";

            return string.Join(".", _steps);
        }
    }
}
