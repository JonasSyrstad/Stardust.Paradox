using System;
using System.Collections.Generic;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine
{
    /// <summary>
    /// TinkerGraph-inspired traversal representation
    /// </summary>
    public class TinkerGraphTraversal
    {
        public List<TinkerGraphStep> Steps { get; }
        public Dictionary<string, object> Parameters { get; set; }
        public Dictionary<string, int> LabelIndex { get; }
        public bool RequiresPath { get; set; }
        public bool HasBarriers { get; set; }
        public bool HasSideEffects { get; set; }

        public TinkerGraphTraversal()
        {
            Steps = new List<TinkerGraphStep>();
            Parameters = new Dictionary<string, object>();
            LabelIndex = new Dictionary<string, int>();
            RequiresPath = false;
            HasBarriers = false;
            HasSideEffects = false;
        }

        public void AddStep(TinkerGraphStep step)
        {
            Steps.Add(step);
            
            // Update traversal metadata
            if (step.StepType == TinkerGraphStepType.ReducingBarrier || 
                step.StepType == TinkerGraphStepType.CollectingBarrier)
            {
                HasBarriers = true;
            }
            
            if (step.StepType == TinkerGraphStepType.SideEffect)
            {
                HasSideEffects = true;
            }
        }

        public TinkerGraphStep GetStepByLabel(string label)
        {
            if (LabelIndex.TryGetValue(label, out var index) && index < Steps.Count)
            {
                return Steps[index];
            }
            return null;
        }

        public bool IsOptimizable()
        {
            // A traversal is optimizable if it doesn't have complex side effects
            return !HasSideEffects && Steps.Count > 1;
        }

        public override string ToString()
        {
            return string.Join(".", Steps.Select(s => s.ToString()));
        }
    }

    /// <summary>
    /// TinkerGraph step types based on TinkerPop classification
    /// </summary>
    public enum TinkerGraphStepType
    {
        Filter,
        Map,
        FlatMap,
        ReducingBarrier,
        CollectingBarrier,
        SideEffect,
        Branch
    }

    /// <summary>
    /// TinkerGraph-inspired step representation
    /// </summary>
    public class TinkerGraphStep
    {
        public string StepName { get; }
        public List<object> Arguments { get; }
        public List<string> Labels { get; }
        public TinkerGraphStepType StepType { get; set; }
        public bool IsStartStep { get; set; }
        public bool IsOptimized { get; set; }
        
        // Metadata for optimization
        public Dictionary<string, object> Metadata { get; }
        
        /// <summary>
        /// Nested traversal for steps like repeat(), until(), where(), etc.
        /// </summary>
        public List<TinkerGraphStep> NestedTraversal { get; set; }
        
        /// <summary>
        /// Additional traversals for steps like union(), coalesce(), choose(), etc.
        /// </summary>
        public List<List<TinkerGraphStep>> AdditionalTraversals { get; set; }

        public TinkerGraphStep(string stepName)
        {
            StepName = stepName ?? throw new ArgumentNullException(nameof(stepName));
            Arguments = new List<object>();
            Labels = new List<string>();
            Metadata = new Dictionary<string, object>();
            StepType = TinkerGraphStepType.SideEffect; // Default
            IsStartStep = false;
            IsOptimized = false;
            NestedTraversal = null;
            AdditionalTraversals = null;
        }

        public void AddLabel(string label)
        {
            if (!string.IsNullOrEmpty(label) && !Labels.Contains(label))
            {
                Labels.Add(label);
            }
        }

        public bool HasLabel(string label)
        {
            return Labels.Contains(label);
        }

        public T GetArgument<T>(int index, T defaultValue = default)
        {
            if (index >= 0 && index < Arguments.Count)
            {
                var arg = Arguments[index];
                if (arg is T typedArg)
                    return typedArg;
                    
                try
                {
                    return (T)Convert.ChangeType(arg, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        public string GetFirstStringArgument()
        {
            return GetArgument<string>(0);
        }

        public int GetFirstIntArgument()
        {
            return GetArgument<int>(0);
        }

        public bool IsFilter()
        {
            return StepType == TinkerGraphStepType.Filter;
        }

        public bool IsBarrier()
        {
            return StepType == TinkerGraphStepType.ReducingBarrier || 
                   StepType == TinkerGraphStepType.CollectingBarrier;
        }

        public override string ToString()
        {
            var argsStr = Arguments.Any() ? $"({string.Join(", ", Arguments)})" : "";
            var labelsStr = Labels.Any() ? $".as('{string.Join("', '", Labels)}')" : "";
            return $"{StepName}{argsStr}{labelsStr}";
        }
    }
}
