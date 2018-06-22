using System;

namespace Stardust.Paradox.Data.CodeGeneration
{
    internal static class CasingHelper
    {
        public static string ToCamelCase(this string name)
        {
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        public static string ToPascalCase(this string name)
        {
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }
    }
}