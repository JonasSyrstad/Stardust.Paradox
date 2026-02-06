namespace Stardust.Paradox.Data.Annotations
{
    public class PropertyChangingHandlerArgs : PropertyChangedHandlerArgs
    {
        public PropertyChangingHandlerArgs(object value, object oldValue, string propertyName) : base(value,
            propertyName)
        {
            OldValue = oldValue;
        }

        public object OldValue { get; }
    }

    public class PropertyChangedHandlerArgs
    {
        public PropertyChangedHandlerArgs(object value, string propertyName)
        {
            Value = value;
            PropertyName = propertyName;
        }

        public object Value { get; }

        public string PropertyName { get; }
    }
}