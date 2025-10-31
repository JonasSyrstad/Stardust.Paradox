using System;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Represents a Gremlin predicate
    /// </summary>
    public class GremlinPredicate
    {
        private readonly string _predicateName;
        private readonly object[] _values;

        public GremlinPredicate(string predicateName, params object[] values)
        {
            _predicateName = predicateName;
            _values = values;
        }

        public string PredicateString
        {
            get
            {
                if (_values.Length == 0)
                    return $"{_predicateName}()";

                if (_values.Length == 1)
                {
                    var val = FormatValue(_values[0]);
                    return $"{_predicateName}({val})";
                }

                var formattedValues = string.Join(", ", Array.ConvertAll(_values, FormatValue));
                return $"{_predicateName}({formattedValues})";
            }
        }

        private string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is string s) return $"'{s.Replace("'", "\\'")}'";
            if (value is bool b) return b.ToString().ToLowerInvariant();
            if (value is decimal d)
            {
                // Format decimal without trailing zeros
                return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            if (value is double db)
            {
                // Format double without trailing zeros
                return db.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            if (value is float f)
            {
                // Format float without trailing zeros
                return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return value.ToString();
        }
    }
}