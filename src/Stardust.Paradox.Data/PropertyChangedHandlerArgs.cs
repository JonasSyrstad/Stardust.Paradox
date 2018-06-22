namespace Stardust.Paradox.Data
{
    public class PropertyChangedHandlerArgs
    {
        private object value;
        private string propertyName;

        internal PropertyChangedHandlerArgs(object value, string propertyName)
        {
            this.value = value;
            this.propertyName = propertyName;
        }

        public object Value
        {
            get { return value; }
        }

        public string PropertyName
        {
            get { return propertyName; }
        }
    }
}