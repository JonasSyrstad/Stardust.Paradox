using System;
using System.Collections.Generic;

namespace GremlinNetComprehensiveTestApp
{
    /// <summary>
    /// Test results aggregator for comprehensive validation
    /// </summary>
    public class TestResults
    {
        public List<string> Successes { get; } = new List<string>();
        public Dictionary<string, string> Failures { get; } = new Dictionary<string, string>();
        
        public int SuccessCount => Successes.Count;
        public int FailureCount => Failures.Count;
        public int TotalCount => SuccessCount + FailureCount;
        public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount : 0;

        public void RecordSuccess(string testName)
        {
            Successes.Add(testName);
        }

        public void RecordFailure(string testName, string reason)
        {
            Failures[testName] = reason;
        }

        public void Merge(TestResults? other)
        {
            if (other != null)
            {
                Successes.AddRange(other.Successes);
                foreach (var failure in other.Failures)
                {
                    Failures[failure.Key] = failure.Value;
                }
            }
        }
    }
}