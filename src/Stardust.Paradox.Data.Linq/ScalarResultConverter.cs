using System;
using System.Collections;

namespace Stardust.Paradox.Data.Linq
{
    /// <summary>
    /// Handles conversion of scalar results from Gremlin queries to .NET types
    /// </summary>
    internal static class ScalarResultConverter
    {
        /// <summary>
        /// Converts a scalar result from a Gremlin query to the target type
        /// </summary>
        public static object ConvertScalarResult(object value, Type targetType)
        {
   if (value == null)
  return GetDefaultValue(targetType);

          // Handle nullable types
       if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
         var underlyingType = Nullable.GetUnderlyingType(targetType);
     var convertedValue = ConvertScalarResult(value, underlyingType);
    if (convertedValue == null)
return null;
                return Activator.CreateInstance(targetType, convertedValue);
      }

    // If value is already the target type, return it
            if (value.GetType() == targetType)
 return value;

// Handle bool specially - Gremlin returns long/int for boolean results
            if (targetType == typeof(bool))
            {
                return ConvertToBoolean(value);
      }

            // Handle string
      if (targetType == typeof(string))
        return value.ToString();

      // Handle numeric conversions
            if (IsNumericType(targetType))
        {
  return ConvertNumeric(value, targetType);
    }

          // Handle collections (for All method which returns a collection from Gremlin)
            if (value is IEnumerable enumerable && !(value is string))
      {
            // For boolean target (All method), check collection
      if (targetType == typeof(bool))
    {
   var enumerator = enumerable.GetEnumerator();
          var hasAny = enumerator.MoveNext();
         
     if (!hasAny)
  return false;
         
            // Get first value and convert to boolean
            var firstValue = enumerator.Current;
    return ConvertToBoolean(firstValue);
    }
         }

            // Try direct conversion as last resort - but check for IConvertible first
            if (value is IConvertible)
        {
          try
      {
   return Convert.ChangeType(value, targetType);
                }
         catch
             {
  // If conversion fails, return default
      return GetDefaultValue(targetType);
     }
            }
  
            // For non-IConvertible types, try some common conversions
     if (targetType == typeof(long) && value is int intVal)
            {
    return (long)intVal;
            }
   
       if (targetType == typeof(int) && value is long longVal)
            {
          if (longVal >= int.MinValue && longVal <= int.MaxValue)
         return (int)longVal;
    }
     
      if (targetType == typeof(double) && value is decimal decVal)
      {
              return (double)decVal;
       }

 // If all else fails, return default
        return GetDefaultValue(targetType);
        }

        private static bool ConvertToBoolean(object value)
   {
            if (value is bool boolValue)
  return boolValue;

   if (value is long longValue)
                return longValue > 0;

 if (value is int intValue)
    return intValue > 0;

        if (value is decimal decimalValue)
        return decimalValue > 0;

          if (value is double doubleValue)
                return doubleValue > 0;

       if (value is float floatValue)
      return floatValue > 0;

      if (value is string strValue)
   {
        if (bool.TryParse(strValue, out bool parsed))
        return parsed;
       // Try numeric conversion
        if (long.TryParse(strValue, out long numValue))
        return numValue > 0;
  }

            return false;
        }

        private static bool IsNumericType(Type type)
        {
       return type == typeof(int) ||
     type == typeof(long) ||
   type == typeof(decimal) ||
  type == typeof(double) ||
        type == typeof(float) ||
  type == typeof(short) ||
            type == typeof(byte) ||
         type == typeof(sbyte) ||
    type == typeof(uint) ||
  type == typeof(ulong) ||
        type == typeof(ushort);
        }

        private static object ConvertNumeric(object value, Type targetType)
  {
        try
        {
      // Handle the special case where value might be a boxed enum or other complex type
                if (value.GetType().IsPrimitive || value is decimal)
                {
               return Convert.ChangeType(value, targetType);
     }

  // Try to extract numeric value from complex objects
     if (long.TryParse(value.ToString(), out long longVal))
                {
       return Convert.ChangeType(longVal, targetType);
        }

                if (double.TryParse(value.ToString(), out double doubleVal))
    {
      return Convert.ChangeType(doubleVal, targetType);
                }

    return GetDefaultValue(targetType);
      }
            catch
     {
           return GetDefaultValue(targetType);
   }
        }

   private static object GetDefaultValue(Type type)
        {
  if (type.IsValueType)
        return Activator.CreateInstance(type);
            return null;
   }
    }
}
