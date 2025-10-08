using Stardust.Paradox.Data.Annotations.Annotations;
using System.Linq;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the hasId() step which filters elements by their ID.
    /// 
    /// Behavior:
    /// - hasId(id1, id2, ...): Filters to keep only elements with one of the specified IDs
    /// 
    /// Example:
    /// g.V().hasId('vertex1', 'vertex2') - vertices with ID vertex1 or vertex2
    /// </summary>
    [UsedImplicitly]
    public class HasIdStepExecutor : StepExecutorBase
    {
        public HasIdStepExecutor(Core.InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "hasid";

        public override string StepDescription => 
            "Filters elements by their ID. " +
            "hasId(id1, id2, ...) keeps only elements whose ID matches one of the provided values.";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            var expectedIds = step.Arguments.Select(arg => arg.ToString()).ToHashSet();

            context.Filter(traverser =>
            {
                var id = ExtractId(traverser.Value);
                return id != null && expectedIds.Contains(id);
            });
        }
    }
}
