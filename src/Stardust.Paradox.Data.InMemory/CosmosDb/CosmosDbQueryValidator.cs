using System;
using System.Collections.Generic;
using System.Linq;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.ExecutionEngine;

namespace Stardust.Paradox.Data.InMemory.CosmosDb
{
    /// <summary>
    /// Validates Gremlin queries against Cosmos DB Gremlin API limitations.
    /// </summary>
    public class CosmosDbQueryValidator : IQueryValidator
    {
        private readonly InMemoryDatabaseOptions _options;
        
        public CosmosDbQueryValidator(InMemoryDatabaseOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }
        
        /// <summary>
        /// Validate a parsed query against Cosmos DB limitations
        /// </summary>
        public ValidationResult Validate(IEnumerable<TinkerGraphStep> steps, string originalQuery = null)
        {
            var errors = new List<ValidationError>();
            var warnings = new List<ValidationWarning>();
            
            var stepList = steps?.ToList() ?? new List<TinkerGraphStep>();
            
            foreach (var step in stepList)
            {
                ValidateStep(step, errors, warnings);
            }
            
            // Check repeat depth
            ValidateRepeatDepth(stepList, errors, warnings);
            
            // Check for partition key usage if configured
            if (_options.EnforceCrossPartitionQueryRestrictions && 
                !string.IsNullOrEmpty(_options.PartitionKeyPath))
            {
                ValidatePartitionKeyUsage(stepList, originalQuery, errors, warnings);
            }
            
            // Check query complexity
            ValidateQueryComplexity(stepList, warnings);
            
            return new ValidationResult(errors, warnings);
        }
        
        /// <summary>
        /// Validate a single step
        /// </summary>
        private void ValidateStep(TinkerGraphStep step, List<ValidationError> errors, List<ValidationWarning> warnings)
        {
            if (step == null) return;
            
            var stepName = step.StepName;
            
            // Check if step is unsupported
            if (CosmosDbGremlinLimitations.UnsupportedSteps.Contains(stepName))
            {
                var reason = CosmosDbGremlinLimitations.GetUnsupportedReason(stepName);
                
                if (_options.ThrowOnUnsupportedStep)
                {
                    errors.Add(new ValidationError(
                        stepName,
                        reason,
                        ValidationErrorCode.UnsupportedStep));
                }
                else
                {
                    warnings.Add(new ValidationWarning(
                        stepName,
                        $"Step '{stepName}' may not work as expected: {reason}",
                        ValidationWarningCode.UnsupportedStep));
                }
            }
            
            // Check if step has limited support
            if (CosmosDbGremlinLimitations.HasLimitedSupport(stepName, out var limitation))
            {
                warnings.Add(new ValidationWarning(
                    stepName,
                    limitation,
                    ValidationWarningCode.LimitedSupport));
            }
            
            // Validate predicates within the step arguments
            ValidatePredicates(step, errors, warnings);
            
            // Recursively validate nested traversals
            if (step.NestedTraversal != null)
            {
                foreach (var nestedStep in step.NestedTraversal)
                {
                    ValidateStep(nestedStep, errors, warnings);
                }
            }
            
            if (step.AdditionalTraversals != null)
            {
                foreach (var traversal in step.AdditionalTraversals)
                {
                    foreach (var nestedStep in traversal)
                    {
                        ValidateStep(nestedStep, errors, warnings);
                    }
                }
            }
        }
        
        /// <summary>
        /// Validate predicates used in the step
        /// </summary>
        private void ValidatePredicates(TinkerGraphStep step, List<ValidationError> errors, List<ValidationWarning> warnings)
        {
            if (step.Arguments == null) return;
            
            foreach (var arg in step.Arguments)
            {
                var argStr = arg?.ToString() ?? "";
                
                // Check for unsupported predicates
                foreach (var unsupported in CosmosDbGremlinLimitations.UnsupportedPredicates)
                {
                    if (ContainsIgnoreCase(argStr, unsupported))
                    {
                        if (_options.ThrowOnUnsupportedStep)
                        {
                            errors.Add(new ValidationError(
                                unsupported,
                                $"Predicate '{unsupported}' is not supported by Cosmos DB",
                                ValidationErrorCode.UnsupportedPredicate));
                        }
                        else
                        {
                            warnings.Add(new ValidationWarning(
                                unsupported,
                                $"Predicate '{unsupported}' may not work as expected",
                                ValidationWarningCode.UnsupportedPredicate));
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Validate repeat depth doesn't exceed Cosmos DB limits
        /// </summary>
        private void ValidateRepeatDepth(List<TinkerGraphStep> steps, List<ValidationError> errors, List<ValidationWarning> warnings)
        {
            foreach (var step in steps)
            {
                if (step.StepName.Equals("times", StringComparison.OrdinalIgnoreCase))
                {
                    if (step.Arguments?.Count > 0)
                    {
                        if (int.TryParse(step.Arguments[0]?.ToString(), out var times))
                        {
                            if (times > CosmosDbGremlinLimitations.MaxRepeatDepth)
                            {
                                errors.Add(new ValidationError(
                                    "times",
                                    $"Repeat depth {times} exceeds Cosmos DB limit of {CosmosDbGremlinLimitations.MaxRepeatDepth}",
                                    ValidationErrorCode.ExceedsLimit));
                            }
                            else if (times > CosmosDbGremlinLimitations.MaxRepeatDepth / 2)
                            {
                                warnings.Add(new ValidationWarning(
                                    "times",
                                    $"High repeat depth ({times}) may cause performance issues or timeouts",
                                    ValidationWarningCode.PerformanceRisk));
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Validate partition key usage for cross-partition query restrictions
        /// </summary>
        private void ValidatePartitionKeyUsage(List<TinkerGraphStep> steps, string originalQuery, 
            List<ValidationError> errors, List<ValidationWarning> warnings)
        {
            // Extract property name from partition key path (e.g., "/pk" -> "pk")
            var pkProperty = _options.PartitionKeyPath?.TrimStart('/');
            
            if (string.IsNullOrEmpty(pkProperty))
                return;
            
            // Check if any has() step filters on the partition key
            var hasPartitionKeyFilter = steps.Any(s =>
                s.StepName.Equals("has", StringComparison.OrdinalIgnoreCase) &&
                s.Arguments?.Count >= 1 &&
                s.Arguments[0]?.ToString()?.Equals(pkProperty, StringComparison.OrdinalIgnoreCase) == true);
            
            // Also check the original query string for partition key filtering
            if (!hasPartitionKeyFilter && !string.IsNullOrEmpty(originalQuery))
            {
                hasPartitionKeyFilter = ContainsIgnoreCase(originalQuery, $"has('{pkProperty}'") ||
                                       ContainsIgnoreCase(originalQuery, $"has(\"{pkProperty}\"");
            }
            
            if (!hasPartitionKeyFilter)
            {
                warnings.Add(new ValidationWarning(
                    "partition",
                    $"Query does not filter on partition key '{pkProperty}' - cross-partition query will be used, which may be slower and consume more RUs",
                    ValidationWarningCode.CrossPartitionQuery));
            }
        }
        
        /// <summary>
        /// Validate query complexity and warn about potential performance issues
        /// </summary>
        private void ValidateQueryComplexity(List<TinkerGraphStep> steps, List<ValidationWarning> warnings)
        {
            // Count expensive operations
            var traversalSteps = new[] { "out", "in", "both", "outE", "inE", "bothE" };
            var traversalCount = steps.Count(s => 
                traversalSteps.Any(t => t.Equals(s.StepName, StringComparison.OrdinalIgnoreCase)));
            
            if (traversalCount > 5)
            {
                warnings.Add(new ValidationWarning(
                    "complexity",
                    $"Query has {traversalCount} traversal steps which may be expensive. Consider optimizing.",
                    ValidationWarningCode.PerformanceRisk));
            }
            
            // Check for potentially slow operations
            var aggregationSteps = new[] { "group", "groupCount", "tree" };
            var hasGroupOrTree = steps.Any(s =>
                aggregationSteps.Any(a => a.Equals(s.StepName, StringComparison.OrdinalIgnoreCase)));
            
            var limitSteps = new[] { "limit", "range" };
            var hasNoLimit = !steps.Any(s =>
                limitSteps.Any(l => l.Equals(s.StepName, StringComparison.OrdinalIgnoreCase)));
            
            if (hasGroupOrTree && hasNoLimit)
            {
                warnings.Add(new ValidationWarning(
                    "aggregation",
                    "Aggregation operation without limit may process large datasets and timeout",
                    ValidationWarningCode.PerformanceRisk));
            }
        }
        
        /// <summary>
        /// Helper method for case-insensitive string contains (compatible with .NET Standard 2.0)
        /// </summary>
        private static bool ContainsIgnoreCase(string source, string value)
        {
            if (source == null || value == null)
                return false;
            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// Interface for query validators
    /// </summary>
    public interface IQueryValidator
    {
        ValidationResult Validate(IEnumerable<TinkerGraphStep> steps, string originalQuery = null);
    }

    /// <summary>
    /// Result of query validation
    /// </summary>
    public class ValidationResult
    {
        public List<ValidationError> Errors { get; }
        public List<ValidationWarning> Warnings { get; }
        public bool IsValid => Errors.Count == 0;
        public bool HasWarnings => Warnings.Count > 0;
        
        public ValidationResult(List<ValidationError> errors, List<ValidationWarning> warnings)
        {
            Errors = errors ?? new List<ValidationError>();
            Warnings = warnings ?? new List<ValidationWarning>();
        }
        
        public void ThrowIfInvalid()
        {
            if (!IsValid)
            {
                var errorMessages = string.Join("; ", Errors.Select(e => e.Message));
                throw new CosmosDbUnsupportedOperationException(errorMessages);
            }
        }
    }

    /// <summary>
    /// Validation error details
    /// </summary>
    public class ValidationError
    {
        public string StepName { get; }
        public string Message { get; }
        public ValidationErrorCode Code { get; }
        
        public ValidationError(string stepName, string message, ValidationErrorCode code = ValidationErrorCode.General)
        {
            StepName = stepName;
            Message = message;
            Code = code;
        }
    }

    /// <summary>
    /// Validation warning details
    /// </summary>
    public class ValidationWarning
    {
        public string StepName { get; }
        public string Message { get; }
        public ValidationWarningCode Code { get; }
        
        public ValidationWarning(string stepName, string message, ValidationWarningCode code = ValidationWarningCode.General)
        {
            StepName = stepName;
            Message = message;
            Code = code;
        }
    }

    /// <summary>
    /// Validation error codes
    /// </summary>
    public enum ValidationErrorCode
    {
        General = 0,
        UnsupportedStep = 1,
        UnsupportedPredicate = 2,
        ExceedsLimit = 3,
        InvalidSyntax = 4,
        MissingPartitionKey = 5
    }

    /// <summary>
    /// Validation warning codes
    /// </summary>
    public enum ValidationWarningCode
    {
        General = 0,
        UnsupportedStep = 1,
        UnsupportedPredicate = 2,
        LimitedSupport = 3,
        CrossPartitionQuery = 4,
        PerformanceRisk = 5
    }

    /// <summary>
    /// Exception thrown when an unsupported operation is attempted in Cosmos DB emulation mode
    /// </summary>
    public class CosmosDbUnsupportedOperationException : Exception
    {
        public string StepName { get; }
        
        public CosmosDbUnsupportedOperationException(string message) 
            : base(message)
        {
        }
        
        public CosmosDbUnsupportedOperationException(string stepName, string message) 
            : base(message)
        {
            StepName = stepName;
        }
        
        public CosmosDbUnsupportedOperationException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}
