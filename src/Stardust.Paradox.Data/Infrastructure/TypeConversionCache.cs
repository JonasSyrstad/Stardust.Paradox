using System;
using System.Collections.Concurrent;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Annotations.DataTypes;
using Newtonsoft.Json;

namespace Stardust.Paradox.Data.Infrastructure
{
    /// <summary>
    /// Provides high-performance type conversion strategies with caching to eliminate repeated type checks.
    /// </summary>
    internal static class TypeConversionCache
    {
        private static readonly ConcurrentDictionary<Type, TypeConversionStrategy> _conversionStrategies = 
   new ConcurrentDictionary<Type, TypeConversionStrategy>(
       concurrencyLevel: Environment.ProcessorCount * 2,
                capacity: 128);

        /// <summary>
      /// Gets or creates a conversion strategy for the specified property type.
        /// </summary>
        public static TypeConversionStrategy GetConversionStrategy(Type propertyType)
        {
      return _conversionStrategies.GetOrAdd(propertyType, CreateConversionStrategy);
  }

        private static TypeConversionStrategy CreateConversionStrategy(Type propertyType)
        {
 // DateTime conversions
         if (propertyType == typeof(DateTime))
   return new TypeConversionStrategy(ConvertToDateTime);
     
            if (propertyType == typeof(DateTime?))
       return new TypeConversionStrategy(ConvertToNullableDateTime);

     // EpochDateTime conversions
         if (propertyType == typeof(EpochDateTime))
         return new TypeConversionStrategy(ConvertToEpochDateTime);
    
    if (propertyType == typeof(EpochDateTime?))
   return new TypeConversionStrategy(ConvertToNullableEpochDateTime);

     // Integer conversions
    if (propertyType == typeof(int))
        return new TypeConversionStrategy(ConvertToInt);
            
 if (propertyType == typeof(int?))
                return new TypeConversionStrategy(ConvertToNullableInt);

          // Boolean conversions
if (propertyType == typeof(bool))
         return new TypeConversionStrategy(ConvertToBool);
      
      if (propertyType == typeof(bool?))
   return new TypeConversionStrategy(ConvertToNullableBool);

         // Enum conversions
            if (propertyType.IsEnum)
         return new TypeConversionStrategy(value => 
        value == null ? Activator.CreateInstance(propertyType) : Enum.Parse(propertyType, (string)value));

     // IComplexProperty conversions
            if (typeof(IComplexProperty).IsAssignableFrom(propertyType))
         return new TypeConversionStrategy(value => 
        value == null ? null : JsonConvert.DeserializeObject(value.ToString(), propertyType));

    // Default: return value as-is
            return new TypeConversionStrategy(value => value);
        }

        #region Conversion Methods

        private static object ConvertToDateTime(object value)
        {
  if (value is DateTime dt)
     return dt;
 return new DateTime(long.Parse(value.ToString()));
        }

        private static object ConvertToNullableDateTime(object value)
        {
      if (value is DateTime dt)
            return dt;
            if (value == null)
         return (DateTime?)null;
       return new DateTime(long.Parse(value.ToString()));
    }

  private static object ConvertToEpochDateTime(object value)
     {
            if (value is EpochDateTime e)
     return e;

            var valueStr = value.ToString();
  if (valueStr.EndsWith("Epoch", StringComparison.OrdinalIgnoreCase))
       {
      var numericPart = valueStr.Substring(0, valueStr.Length - 5);
                if (int.TryParse(numericPart, out int epochValue))
                {
         return new EpochDateTime { Epoch = epochValue };
        }
          if (long.TryParse(numericPart, out long ticksValue))
 {
       var dateTime = new DateTime(ticksValue);
    var epoch = (int)((DateTimeOffset)dateTime).ToUnixTimeSeconds();
       return new EpochDateTime { Epoch = epoch };
        }
      return new EpochDateTime { Epoch = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        }
            return new EpochDateTime { Epoch = int.Parse(valueStr) };
        }

        private static object ConvertToNullableEpochDateTime(object value)
   {
   if (value is EpochDateTime e)
 return e;
      if (value == null)
             return (EpochDateTime?)null;

       var valueStr = value.ToString();
        if (valueStr.EndsWith("Epoch", StringComparison.OrdinalIgnoreCase))
       {
  var numericPart = valueStr.Substring(0, valueStr.Length - 5);
     if (int.TryParse(numericPart, out int epochValue))
   {
        return new EpochDateTime { Epoch = epochValue };
           }
    if (long.TryParse(numericPart, out long ticksValue))
     {
         var dateTime = new DateTime(ticksValue);
      var epoch = (int)((DateTimeOffset)dateTime).ToUnixTimeSeconds();
         return new EpochDateTime { Epoch = epoch };
                }
                return new EpochDateTime { Epoch = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
  }
          return new EpochDateTime { Epoch = int.Parse(valueStr) };
        }

        private static object ConvertToInt(object value)
        {
        return value == null ? 0 : int.Parse(value.ToString());
        }

        private static object ConvertToNullableInt(object value)
        {
        return value == null ? (int?)null : int.Parse(value.ToString());
        }

        private static object ConvertToBool(object value)
        {
       if (value is bool b)
     return b;
     if (value != null)
         return bool.Parse(value.ToString());
            return false;
        }

 private static object ConvertToNullableBool(object value)
{
            if (value is bool b)
   return b;
if (value == null)
     return (bool?)null;
          return bool.Parse(value.ToString());
        }

        #endregion
    }

    /// <summary>
 /// Encapsulates a type conversion strategy.
    /// </summary>
    internal class TypeConversionStrategy
    {
        private readonly Func<object, object> _converter;

      public TypeConversionStrategy(Func<object, object> converter)
{
            _converter = converter;
        }

        public object Convert(object value)
        {
    return _converter(value);
        }
    }
}
