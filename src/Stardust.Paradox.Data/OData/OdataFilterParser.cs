using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Stardust.Paradox.Data.Traversals;

namespace Stardust.Paradox.Data.OData
{
    internal static class OdataFilterParser
    {
        public static GremlinQuery ApplyODataFilter(this GremlinQuery baseGremlinQuery, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return baseGremlinQuery;

            // Parse filter expression with support for parentheses
            var result = baseGremlinQuery;
            var parsedFilter = ParseFilterExpression(filter);
            return ApplyParsedFilter(result, parsedFilter);
        }

        private static FilterExpression ParseFilterExpression(string filter)
        {
            filter = filter.Trim();
            
            // Handle parentheses - find the outermost logical operator not inside parentheses
            var logicalOp = FindTopLevelLogicalOperator(filter);
            
            if (logicalOp.HasValue)
            {
                var parts = SplitByTopLevelOperator(filter, logicalOp.Value);
                var expressions = parts.Select(ParseFilterExpression).ToList();
                return new FilterExpression
                {
                    Type = logicalOp.Value == LogicalOperator.And ? FilterType.And : FilterType.Or,
                    SubExpressions = expressions
                };
            }
            
            // Remove outer parentheses if present
            if (filter.StartsWith("(") && filter.EndsWith(")"))
            {
                var inner = filter.Substring(1, filter.Length - 2);
                // Make sure parentheses are balanced
                if (IsBalanced(inner))
                {
                    return ParseFilterExpression(inner);
                }
            }
            
            // Parse single condition
            return new FilterExpression
            {
                Type = FilterType.Condition,
                Condition = ParseCondition(filter)
            };
        }

        private static LogicalOperator? FindTopLevelLogicalOperator(string filter)
        {
            int level = 0;
            int lastOrPos = -1;
            int lastAndPos = -1;
            
            for (int i = 0; i < filter.Length; i++)
            {
                if (filter[i] == '(')
                    level++;
                else if (filter[i] == ')')
                    level--;
                else if (level == 0)
                {
                    // Check for " or " at current position (case insensitive)
                    if (i + 4 <= filter.Length && 
                        string.Compare(filter.Substring(i, 4), " or ", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        lastOrPos = i;
                    }
                    // Check for " and " at current position (case insensitive)
                    else if (i + 5 <= filter.Length && 
                        string.Compare(filter.Substring(i, 5), " and ", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        lastAndPos = i;
                    }
                }
            }
            
            // OR has lower precedence than AND, so we process OR first
            if (lastOrPos >= 0)
                return LogicalOperator.Or;
            if (lastAndPos >= 0)
                return LogicalOperator.And;
            
            return null;
        }

        private static List<string> SplitByTopLevelOperator(string filter, LogicalOperator op)
        {
            var separator = op == LogicalOperator.And ? " and " : " or ";
            var parts = new List<string>();
            int level = 0;
            int lastStart = 0;
            
            for (int i = 0; i < filter.Length; i++)
            {
                if (filter[i] == '(')
                    level++;
                else if (filter[i] == ')')
                    level--;
                else if (level == 0)
                {
                    var matchLength = separator.Length;
                    if (i + matchLength <= filter.Length &&
                        string.Compare(filter.Substring(i, matchLength), separator, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        parts.Add(filter.Substring(lastStart, i - lastStart).Trim());
                        lastStart = i + matchLength;
                        i += matchLength - 1; // -1 because loop will increment
                    }
                }
            }
            
            // Add the last part
            if (lastStart < filter.Length)
            {
                parts.Add(filter.Substring(lastStart).Trim());
            }
            
            return parts;
        }

        private static bool IsBalanced(string str)
        {
            int level = 0;
            foreach (char c in str)
            {
                if (c == '(') level++;
                else if (c == ')') level--;
                if (level < 0) return false;
            }
            return level == 0;
        }

        private static GremlinQuery ApplyParsedFilter(GremlinQuery query, FilterExpression filter)
        {
            switch (filter.Type)
            {
                case FilterType.And:
                    foreach (var subExpr in filter.SubExpressions)
                    {
                        query = ApplyParsedFilter(query, subExpr);
                    }
                    return query;
                
                case FilterType.Or:
                    return query.Or(filter.SubExpressions
                        .Select<FilterExpression, Func<PredicateGremlinQuery, GremlinQuery>>(
                            expr => q => ApplySingleConditionForOr(q, expr.Condition))
                        .ToArray());
                
                case FilterType.Condition:
                    return ApplySingleCondition(query, filter.Condition);
                
                default:
                    return query;
            }
        }

        private static GremlinQuery ApplySingleCondition(GremlinQuery query, Condition condition)
        {
            if (condition == null) return query;

            switch (condition.Operator)
            {
                case "eq":
                    return ApplyEqualFilter(query, condition.PropertyName, condition.Value, condition.ValueType);
                
                case "ne":
                case "gt":
                case "ge":
                case "lt":
                case "le":
                    // Use .has() with predicate instead of .values().is()
                    return query.Has(condition.PropertyName, p => ApplyComparisonPredicate(p, condition.Operator, condition.Value, condition.ValueType));
                
                case "contains":
                    return query.Has(condition.PropertyName, p => ((PredicateGremlinQuery)p).Containing(condition.Value.ToString()));
                
                case "startswith":
                    return query.Has(condition.PropertyName, p => ((PredicateGremlinQuery)p).StartingWith(condition.Value.ToString()));
                
                case "endswith":
                    return query.Has(condition.PropertyName, p => ((PredicateGremlinQuery)p).EndingWith(condition.Value.ToString()));
                
                default:
                    return query;
            }
        }

        private static GremlinQuery ApplyComparisonPredicate(GremlinQuery predicateQuery, string op, object value, ValueType type)
        {
            var p = (PredicateGremlinQuery)predicateQuery;
            
            switch (op)
            {
                case "ne":
                    switch (type)
                    {
                        case ValueType.Int:
                            return p.Neq((long)(int)value);
                        case ValueType.Long:
                            return p.Neq((long)value);
                        case ValueType.Decimal:
                            return p.Neq((decimal)value);
                        case ValueType.Bool:
                            return p.Neq((bool)value);
                        default:
                            return p.Neq(value.ToString());
                    }
                case "gt":
                    switch (type)
                    {
                        case ValueType.Int:
                            return p.Gt((int)value);
                        case ValueType.Long:
                            return p.Gt((int)(long)value);
                        case ValueType.Decimal:
                            return p.Gt((decimal)value);
                        default:
                            return p.Gt(value.ToString());
                    }
                case "ge":
                    switch (type)
                    {
                        case ValueType.Int:
                            return p.Gte((int)value);
                        case ValueType.Long:
                            return p.Gte((int)(long)value);
                        case ValueType.Decimal:
                            return p.Gte((decimal)value);
                        default:
                            return p.Gte((decimal)value);
                    }
                case "lt":
                    switch (type)
                    {
                        case ValueType.Int:
                            return p.Lt((int)value);
                        case ValueType.Long:
                            return p.Lt((int)(long)value);
                        case ValueType.Decimal:
                            return p.Lt((decimal)value);
                        default:
                            return p.Lt((decimal)value);
                    }
                case "le":
                    switch (type)
                    {
                        case ValueType.Int:
                            return p.Lte((int)value);
                        case ValueType.Long:
                            return p.Lte((int)(long)value);
                        case ValueType.Decimal:
                            return p.Lte((decimal)value);
                        default:
                            return p.Lte((decimal)value);
                    }
                default:
                    return predicateQuery;
            }
        }

        private static GremlinQuery ApplySingleConditionForOr(PredicateGremlinQuery predicateQuery, Condition condition)
        {
            if (condition == null) return null;

            switch (condition.Operator)
            {
                case "eq":
                    return predicateQuery.Has(condition.PropertyName, p =>
                        ConvertPredicateForEq(p, condition.Value, condition.ValueType));
                
                case "ne":
                case "gt":
                case "ge":
                case "lt":
                case "le":
                    return predicateQuery.Has(condition.PropertyName, p => ApplyComparisonPredicate(p, condition.Operator, condition.Value, condition.ValueType));
                
                case "contains":
                    return predicateQuery.Has(condition.PropertyName, p => ((PredicateGremlinQuery)p).Containing(condition.Value.ToString()));
                
                case "startswith":
                    return predicateQuery.Has(condition.PropertyName, p => ((PredicateGremlinQuery)p).StartingWith(condition.Value.ToString()));
                
                case "endswith":
                    return predicateQuery.Has(condition.PropertyName, p => ((PredicateGremlinQuery)p).EndingWith(condition.Value.ToString()));
                
                default:
                    return null;
            }
        }

        private static GremlinQuery ConvertPredicateForEq(PredicateGremlinQuery predicate, object value, ValueType type)
        {
            switch (type)
            {
                case ValueType.Int:
                    return predicate.Eq((int)value);
                case ValueType.Long:
                    return predicate.Eq((long)value);
                case ValueType.Decimal:
                    return predicate.Eq((decimal)value);
                case ValueType.Bool:
                    return predicate.Eq((bool)value);
                case ValueType.String:
                default:
                    return predicate.Eq(value.ToString());
            }
        }

        private static Condition ParseCondition(string condition)
        {
            // Parse: propertyName operator value
            var match = Regex.Match(condition, @"(\w+)\s+(eq|ne|gt|ge|lt|le|contains|startswith|endswith)\s+(.+)", RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            var propertyName = match.Groups[1].Value;
            var op = match.Groups[2].Value.ToLower();
            var value = match.Groups[3].Value.Trim();

            // Remove quotes from string values
            if (value.StartsWith("'") && value.EndsWith("'"))
                value = value.Substring(1, value.Length - 2);

            ParseValue(value, out var parsedValue, out var valueType);

            return new Condition
            {
                PropertyName = propertyName,
                Operator = op,
                Value = parsedValue,
                ValueType = valueType
            };
        }

        private static GremlinQuery ApplyEqualFilter(GremlinQuery query, string propertyName, object value, ValueType type)
        {
            switch (type)
            {
                case ValueType.String:
                    return query.Has(propertyName, (string)value);
                case ValueType.Int:
                    return query.Has(propertyName, (int)value);
                case ValueType.Long:
                    return query.Has(propertyName, (long)value);
                case ValueType.Decimal:
                    return query.Has(propertyName, (decimal)value);
                case ValueType.Bool:
                    return query.Has(propertyName, (bool)value);
                default:
                    return query;
            }
        }

        private static bool ParseValue(string value, out object parsed, out ValueType type)
        {
            // Try int
            if (int.TryParse(value, out var intVal))
            {
                parsed = intVal;
                type = ValueType.Int;
                return true;
            }

            // Try long
            if (long.TryParse(value, out var longVal))
            {
                parsed = longVal;
                type = ValueType.Long;
                return true;
            }

            // Try decimal
            if (decimal.TryParse(value, out var decVal))
            {
                parsed = decVal;
                type = ValueType.Decimal;
                return true;
            }

            // Try bool
            if (bool.TryParse(value, out var boolVal))
            {
                parsed = boolVal;
                type = ValueType.Bool;
                return true;
            }

            // Default to string
            parsed = value;
            type = ValueType.String;
            return true;
        }

        private enum ValueType
        {
            String,
            Int,
            Long,
            Decimal,
            Bool
        }

        private enum FilterType
        {
            Condition,
            And,
            Or
        }

        private enum LogicalOperator
        {
            And,
            Or
        }

        private class FilterExpression
        {
            public FilterType Type { get; set; }
            public Condition Condition { get; set; }
            public List<FilterExpression> SubExpressions { get; set; }
        }

        private class Condition
        {
            public string PropertyName { get; set; }
            public string Operator { get; set; }
            public object Value { get; set; }
            public ValueType ValueType { get; set; }
        }
    }
}