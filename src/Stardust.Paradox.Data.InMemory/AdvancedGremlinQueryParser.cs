using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// Advanced Gremlin query parser using proper traversal state management
    /// Inspired by Apache TinkerPop's TinkerGraph implementation
    /// </summary>
    public class AdvancedGremlinQueryParser
    {
        private readonly InMemoryGraphDatabase _database;
        private readonly AdvancedGremlinQueryExecutor _executor;

        // TinkerPop step categories for better step handling
        private static readonly HashSet<string> BarrierSteps = new HashSet<string>
        {
            "group", "groupCount", "cap", "barrier", "fold", "order", "dedup"
        };

        private static readonly HashSet<string> SideEffectSteps = new HashSet<string>
        {
            "aggregate", "store", "sack", "tree"
        };

        public AdvancedGremlinQueryParser(InMemoryGraphDatabase database)
        {
            _database = database;
            _executor = new AdvancedGremlinQueryExecutor(database);
        }

        /// <summary>
        /// Parse and execute a Gremlin query with advanced traversal state management
        /// </summary>
        public IEnumerable<dynamic> ParseAndExecute(string query, Dictionary<string, object> parameters)
        {
            try
            {
                // Check for custom responses first
                var customResponse = _database.GetCustomResponse(query, parameters);
                if (customResponse != null)
                {
                    return customResponse;
                }

                // Substitute parameters first
                var processedQuery = SubstituteParameters(query, parameters);

                // Use enhanced regex-based parsing for better compatibility
                var parsedQuery = ParseQueryWithEnhancedRegex(processedQuery);
                parsedQuery.Parameters = parameters ?? new Dictionary<string, object>();

                // Index step labels for reference resolution
                parsedQuery.IndexStepLabels();

                // Execute the parsed query
                return _executor.Execute(parsedQuery);
            }
            catch (Exception)
            {
                // Fallback to simple regex parsing for unsupported queries
                return ExecuteFallback(query, parameters);
            }
        }

        /// <summary>
        /// Enhanced query parsing with better step detection and metadata extraction
        /// </summary>
        private ParsedGremlinQuery ParseQueryWithEnhancedRegex(string query)
        {
            var parsedQuery = new ParsedGremlinQuery();
            
            // Remove 'g.' prefix if present
            var cleanQuery = query.StartsWith("g.") ? query.Substring(2) : query;
            
            // Split by dots, but preserve content within parentheses
            var steps = SplitPreservingParentheses(cleanQuery);
            
            foreach (var stepStr in steps)
            {
                var step = ParseStepFromStringEnhanced(stepStr.Trim());
                if (step != null)
                {
                    parsedQuery.Steps.Add(step);
                    
                    // Mark query characteristics
                    if (BarrierSteps.Contains(step.StepName.ToLower()))
                    {
                        step.IsBarrier = true;
                        parsedQuery.HasBarriers = true;
                    }
                    
                    if (SideEffectSteps.Contains(step.StepName.ToLower()))
                    {
                        parsedQuery.HasSideEffects = true;
                    }
                }
            }
            
            return parsedQuery;
        }

        /// <summary>
        /// Parse query using the original regex patterns for backwards compatibility
        /// </summary>
        private ParsedGremlinQuery ParseQueryWithRegex(string query)
        {
            var parsedQuery = new ParsedGremlinQuery();
            
            // Remove 'g.' prefix if present
            var cleanQuery = query.StartsWith("g.") ? query.Substring(2) : query;
            
            // Split by dots, but preserve content within parentheses
            var steps = SplitPreservingParentheses(cleanQuery);
            
            foreach (var stepStr in steps)
            {
                var step = ParseStepFromString(stepStr.Trim());
                if (step != null)
                {
                    parsedQuery.Steps.Add(step);
                }
            }
            
            return parsedQuery;
        }

        /// <summary>
        /// Enhanced step parsing with label detection and metadata extraction
        /// </summary>
        private GremlinStep ParseStepFromStringEnhanced(string stepStr)
        {
            if (string.IsNullOrWhiteSpace(stepStr))
                return null;

            // Handle chained steps like step().as('label').other()
            var chainedSteps = SplitChainedSteps(stepStr);
            
            if (chainedSteps.Count == 1)
            {
                return ParseSingleStep(chainedSteps[0]);
            }
            else
            {
                // For now, parse the first step and extract labels from subsequent as() steps
                var mainStep = ParseSingleStep(chainedSteps[0]);
                if (mainStep != null)
                {
                    ExtractLabelsFromChain(mainStep, chainedSteps.Skip(1));
                }
                return mainStep;
            }
        }

        /// <summary>
        /// Split a step string that might contain chained method calls
        /// </summary>
        private List<string> SplitChainedSteps(string stepStr)
        {
            var steps = new List<string>();
            var current = "";
            var parenthesesLevel = 0;
            var inQuotes = false;
            var quoteChar = '\0';
            
            for (int i = 0; i < stepStr.Length; i++)
            {
                char c = stepStr[i];
                
                if (!inQuotes && (c == '\'' || c == '"'))
                {
                    inQuotes = true;
                    quoteChar = c;
                    current += c;
                }
                else if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    current += c;
                }
                else if (!inQuotes && c == '(')
                {
                    parenthesesLevel++;
                    current += c;
                }
                else if (!inQuotes && c == ')')
                {
                    parenthesesLevel--;
                    current += c;
                }
                else if (!inQuotes && c == '.' && parenthesesLevel == 0)
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        steps.Add(current.Trim());
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }
            
            if (!string.IsNullOrWhiteSpace(current))
            {
                steps.Add(current.Trim());
            }
            
            return steps;
        }

        /// <summary>
        /// Parse a single step without chained calls
        /// </summary>
        private GremlinStep ParseSingleStep(string stepStr)
        {
            // Extract step name and arguments
            var match = Regex.Match(stepStr, @"^([a-zA-Z_][a-zA-Z0-9_]*)\s*(\([^)]*\))?");
            if (!match.Success)
                return null;

            var stepName = match.Groups[1].Value;
            var step = new GremlinStep(stepName);

            if (match.Groups[2].Success)
            {
                var argsStr = match.Groups[2].Value.Trim('(', ')');
                if (!string.IsNullOrWhiteSpace(argsStr))
                {
                    step.Arguments.AddRange(ParseArguments(argsStr));
                }
            }

            return step;
        }

        /// <summary>
        /// Extract labels from chained as() calls
        /// </summary>
        private void ExtractLabelsFromChain(GremlinStep mainStep, IEnumerable<string> chainedSteps)
        {
            foreach (var chainedStep in chainedSteps)
            {
                if (chainedStep.StartsWith("as(", StringComparison.OrdinalIgnoreCase))
                {
                    var labelMatch = Regex.Match(chainedStep, @"as\s*\(\s*['""]([^'""]*)['""]?\s*\)", RegexOptions.IgnoreCase);
                    if (labelMatch.Success)
                    {
                        mainStep.AddLabel(labelMatch.Groups[1].Value);
                    }
                }
            }
        }

        /// <summary>
        /// Parse a single step from string representation (legacy method)
        /// </summary>
        private GremlinStep ParseStepFromString(string stepStr)
        {
            if (string.IsNullOrWhiteSpace(stepStr))
                return null;

            // Extract step name and arguments
            var match = Regex.Match(stepStr, @"^([a-zA-Z_][a-zA-Z0-9_]*)\s*(\([^)]*\))?");
            if (!match.Success)
                return null;

            var stepName = match.Groups[1].Value;
            var step = new GremlinStep(stepName);

            if (match.Groups[2].Success)
            {
                var argsStr = match.Groups[2].Value.Trim('(', ')');
                if (!string.IsNullOrWhiteSpace(argsStr))
                {
                    step.Arguments.AddRange(ParseArguments(argsStr));
                }
            }

            return step;
        }

        /// <summary>
        /// Split by dots while preserving content within parentheses
        /// </summary>
        private List<string> SplitPreservingParentheses(string query)
        {
            var steps = new List<string>();
            var currentStep = "";
            var parenthesesLevel = 0;
            var inQuotes = false;
            var quoteChar = '\0';
            
            for (int i = 0; i < query.Length; i++)
            {
                char c = query[i];
                
                if (!inQuotes && (c == '\'' || c == '"'))
                {
                    inQuotes = true;
                    quoteChar = c;
                    currentStep += c;
                }
                else if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    currentStep += c;
                }
                else if (!inQuotes && c == '(')
                {
                    parenthesesLevel++;
                    currentStep += c;
                }
                else if (!inQuotes && c == ')')
                {
                    parenthesesLevel--;
                    currentStep += c;
                }
                else if (!inQuotes && c == '.' && parenthesesLevel == 0)
                {
                    if (!string.IsNullOrWhiteSpace(currentStep))
                    {
                        steps.Add(currentStep);
                        currentStep = "";
                    }
                }
                else
                {
                    currentStep += c;
                }
            }
            
            if (!string.IsNullOrWhiteSpace(currentStep))
            {
                steps.Add(currentStep);
            }
            
            return steps;
        }

        /// <summary>
        /// Parse arguments from argument string with enhanced handling
        /// </summary>
        private List<object> ParseArguments(string argsStr)
        {
            var arguments = new List<object>();
            
            if (string.IsNullOrWhiteSpace(argsStr))
                return arguments;

            // Handle complex arguments including nested traversals
            if (argsStr.Contains("__") && argsStr.Contains("."))
            {
                // This is likely a nested traversal like __.otherV().hasId('id')
                arguments.Add(argsStr);
                return arguments;
            }

            // Split by comma, but preserve quoted strings and nested structures
            var parts = SplitArgumentsPreservingQuotes(argsStr);
            
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;
                    
                arguments.Add(ParseSingleArgument(trimmed));
            }

            return arguments;
        }

        /// <summary>
        /// Split arguments by comma while preserving quoted strings and nested structures
        /// </summary>
        private List<string> SplitArgumentsPreservingQuotes(string argsStr)
        {
            var parts = new List<string>();
            var current = "";
            var inQuotes = false;
            var quoteChar = '\0';
            var parenthesesLevel = 0;
            var bracketLevel = 0;

            for (int i = 0; i < argsStr.Length; i++)
            {
                char c = argsStr[i];

                if (!inQuotes && (c == '\'' || c == '"'))
                {
                    inQuotes = true;
                    quoteChar = c;
                    current += c;
                }
                else if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    current += c;
                }
                else if (!inQuotes && c == '(')
                {
                    parenthesesLevel++;
                    current += c;
                }
                else if (!inQuotes && c == ')')
                {
                    parenthesesLevel--;
                    current += c;
                }
                else if (!inQuotes && c == '[')
                {
                    bracketLevel++;
                    current += c;
                }
                else if (!inQuotes && c == ']')
                {
                    bracketLevel--;
                    current += c;
                }
                else if (!inQuotes && c == ',' && parenthesesLevel == 0 && bracketLevel == 0)
                {
                    parts.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }

            if (!string.IsNullOrEmpty(current))
            {
                parts.Add(current);
            }

            return parts;
        }

        /// <summary>
        /// Parse a single argument value with enhanced type detection
        /// </summary>
        private object ParseSingleArgument(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg))
                return null;

            // Handle quoted strings
            if ((arg.StartsWith("'") && arg.EndsWith("'")) || 
                (arg.StartsWith("\"") && arg.EndsWith("\"")))
            {
                return arg.Substring(1, arg.Length - 2);
            }

            // Handle arrays/lists [item1, item2, ...]
            if (arg.StartsWith("[") && arg.EndsWith("]"))
            {
                var innerContent = arg.Substring(1, arg.Length - 2);
                if (string.IsNullOrWhiteSpace(innerContent))
                    return new List<object>();
                
                var items = SplitArgumentsPreservingQuotes(innerContent);
                return items.Select(ParseSingleArgument).ToList();
            }

            // Handle numbers
            if (int.TryParse(arg, out int intVal))
                return intVal;
            if (long.TryParse(arg, out long longVal))
                return longVal;
            if (double.TryParse(arg, out double doubleVal))
                return doubleVal;
            if (decimal.TryParse(arg, out decimal decimalVal))
                return decimalVal;

            // Handle booleans
            if (bool.TryParse(arg, out bool boolVal))
                return boolVal;

            // Handle null
            if (arg.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            // Handle predicate expressions (P.eq, P.gt, etc.)
            if (arg.StartsWith("P.") || arg.StartsWith("p."))
            {
                return ParsePredicateExpression(arg);
            }

            // Return as string for everything else (including complex expressions)
            return arg;
        }

        /// <summary>
        /// Parse predicate expressions like P.eq('value'), P.gt(10), etc.
        /// </summary>
        private object ParsePredicateExpression(string predicateStr)
        {
            // For now, return as string - could be enhanced to parse into predicate objects
            return predicateStr;
        }

        private string SubstituteParameters(string query, Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return query;

            var result = query;
            foreach (var param in parameters)
            {
                var value = FormatParameterValue(param.Value);
                result = result.Replace(param.Key, value);
            }
            return result;
        }

        private string FormatParameterValue(object value)
        {
            if (value == null) return "null";
            if (value is string) return $"'{value}'";
            if (value is bool) return value.ToString().ToLower();
            return value.ToString();
        }

        private IEnumerable<dynamic> ExecuteFallback(string query, Dictionary<string, object> parameters)
        {
            // Use the original simple parser as fallback
            var simpleParser = new GremlinQueryParser(_database);
            return simpleParser.ParseAndExecuteAsync(query, parameters).Result;
        }
    }
}