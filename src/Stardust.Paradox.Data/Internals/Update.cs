using System;
using System.Collections.Generic;
using System.Globalization;
using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Internals
{
	internal class Update
	{
		internal string Parameterless { get; set; }
		internal string UpdateStatement
		{
			get
			{
				if (Parameterless != null) return Parameterless;
				return $".property('{PropertyName}','{GetValue(Value)}')";
			}
		}

		internal string ParameterizedUpdateStatement
		{
			get
			{
				if (Parameterless != null) return Parameterless;
				return $".property('{PropertyName}',__{PropertyName})";
			}
		}

		internal bool HasParameters => PropertyName != null;

		public KeyValuePair<string, object> Parameter => new KeyValuePair<string, object>(PropertyName, Value);

		internal string PropertyName { get; set; }

		internal object Value { get; set; }
		internal static string GetValue(object value)
		{
			if (value == null) return null;
			switch (value)
			{
				case string s:
					var r = $"'{s.EscapeGremlinString()}'";
					if (r == "'''") return "''";
					return r;
				case DateTime time:
					return $"{time.Ticks}";
				case int no:
					return no.ToString(CultureInfo.InvariantCulture);
				case decimal dec:
					return dec.ToString(CultureInfo.InvariantCulture);
				case long lng:
					return lng.ToString(CultureInfo.InvariantCulture);
				case float flt:
					return flt.ToString(CultureInfo.InvariantCulture);
				case double dbl:
					return dbl.ToString(CultureInfo.InvariantCulture);
				case bool yn:
					return yn.ToString(CultureInfo.InvariantCulture).ToLower();
				case IInlineCollection i:
					return $"'{i.ToTransferData()}'";
			}
			throw new ArgumentException("Unknown type", nameof(value));
		}
	}
}