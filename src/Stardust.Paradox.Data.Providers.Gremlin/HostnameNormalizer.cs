using System;

namespace Stardust.Paradox.Data.Providers.Gremlin
{
    /// <summary>
    /// Utilities for normalizing Gremlin server hostnames
    /// </summary>
    internal static class HostnameNormalizer
    {
  /// <summary>
        /// Normalizes a hostname by removing protocol prefixes, port numbers, trailing slashes, and whitespace
        /// </summary>
        /// <param name="hostname">The hostname to normalize</param>
     /// <returns>The normalized hostname</returns>
        public static string Normalize(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
          throw new ArgumentException("Hostname cannot be null or whitespace", nameof(hostname));

      // Trim whitespace
            hostname = hostname.Trim();

     // Remove protocol prefix if present (https://, http://, wss://, ws://)
  var protocolPrefixes = new[] { "https://", "http://", "wss://", "ws://" };
        foreach (var prefix in protocolPrefixes)
    {
         if (hostname.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
             {
     hostname = hostname.Substring(prefix.Length);
      break;
                }
   }

            // Remove trailing slashes
            hostname = hostname.TrimEnd('/');

            // Remove port number if present (e.g., :443, :8182)
            var colonIndex = hostname.IndexOf(':');
          if (colonIndex > 0)
          {
         // Make sure it's a port and not part of the hostname
       var portPart = hostname.Substring(colonIndex + 1);
                if (int.TryParse(portPart, out _))
                {
   hostname = hostname.Substring(0, colonIndex);
         }
    }

     // Validate result
       if (string.IsNullOrWhiteSpace(hostname))
         throw new ArgumentException("Hostname is invalid after normalization", nameof(hostname));

            return hostname;
        }
    }
}
