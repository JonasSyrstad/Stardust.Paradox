using System;
using System.Linq;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.ExecutionEngine.Steps
{
    /// <summary>
    /// Executes the coin() step - randomly filters out traversers based on a probability
    /// TinkerPop spec: coin(probability) where probability is between 0.0 and 1.0
    /// </summary>
    public class CoinStepExecutor : StepExecutorBase
    {
        private static readonly Random _random = new Random();

        public CoinStepExecutor(InMemoryGraphDatabase database) : base(database)
        {
        }

        public override string StepName => "coin";

        public override string StepDescription => "Randomly filters traversers based on a probability value between 0.0 and 1.0";

        public override void Execute(TinkerGraphStep step, TinkerTraversalContext context)
        {
            if (!step.Arguments.Any())
            {
                throw new ArgumentException("coin() step requires a probability argument between 0.0 and 1.0");
            }

            // Get the probability argument (should be between 0.0 and 1.0)
            double probability = GetDoubleArgument(step, 0);

            if (probability < 0.0 || probability > 1.0)
            {
                throw new ArgumentException($"coin() probability must be between 0.0 and 1.0, got {probability}");
            }

            // Filter traversers randomly based on probability
            var newTraversers = context.Traversers
                .Where(t => _random.NextDouble() <= probability)
                .ToList();

            context.Traversers.Clear();
            foreach (var traverser in newTraversers)
            {
                context.Traversers.Add(traverser);
            }
        }

        /// <summary>
        /// Helper method to extract double argument from step
        /// </summary>
        private double GetDoubleArgument(TinkerGraphStep step, int index)
        {
            var arg = step.Arguments[index];
            
            if (arg is double d)
                return d;
            if (arg is float f)
                return f;
            if (arg is decimal dec)
                return (double)dec;
            if (arg is int i)
                return i;
            if (arg is long l)
                return l;
            
            // Try to parse string
            if (double.TryParse(arg?.ToString(), out double result))
                return result;

            throw new ArgumentException($"coin() step requires a numeric probability argument, got {arg?.GetType().Name}");
        }
    }
}
