namespace Stardust.Paradox.Data.Annotations
{
    public static class SanitizerExtensions
    {
        public static string EscapeGremlinString(this string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            return value.Replace("\\", "")
                .Replace("'", "\\'")
                .Replace("`", "")
                .Replace("´", "");
        }
    }
}