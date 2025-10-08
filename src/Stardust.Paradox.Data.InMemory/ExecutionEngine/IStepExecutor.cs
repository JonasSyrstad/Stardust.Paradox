using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine;

/// <summary>
/// Interface for step executors in the TinkerGraph-inspired traversal engine.
/// Each step executor is responsible for executing a specific Gremlin step.
/// </summary>
public interface IStepExecutor
{
   /// <summary>
    /// The name of the Gremlin step this executor handles (e.g., "v", "out", "has", etc.)
    /// </summary>
    public string StepName { get; }
    
    /// <summary>
    /// A description of what this step does and its expected behavior
    /// </summary>
    public string StepDescription { get; }
    
    /// <summary>
    /// Execute the step with the given context
    /// </summary>
    /// <param name="step">The step to execute containing arguments and labels</param>
    /// <param name="context">The traversal context containing traversers and metadata</param>
    void Execute(TinkerGraphStep step, TinkerTraversalContext context);
}