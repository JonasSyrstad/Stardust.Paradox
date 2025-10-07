namespace Stardust.Paradox.Data.InMemory.ExecutionEngine;

public interface IStepExecutor
{
    public string StepName { get; }
    public string StepDescription { get; }
    void ExecuteVStep(TinkerGraphStep step, TinkerTraversalContext context);
}