using System;

namespace Stardust.Paradox.Data
{
    /// <summary>
    /// Declares supported capabilities for a connector or provider.
    /// </summary>
    public sealed class Features
    {
        /// <summary>
        /// Indicates whether the connector supports parameterized Gremlin queries.
        /// </summary>
        public bool CanParameterizeQueries { get; }

        /// <summary>
        /// Indicates whether the connector supports server-side projections for LINQ `Select`.
        /// </summary>
        public bool SupportsServerSideProjection { get; }

        /// <summary>
        /// Indicates whether the connector supports server-side `Distinct`/`dedup`.
        /// </summary>
        public bool SupportsDedup { get; }

        /// <summary>
        /// Indicates whether the connector supports server-side ordering.
        /// </summary>
        public bool SupportsOrdering { get; }

        /// <summary>
        /// Indicates whether the connector supports server-side paging via `skip`/`limit`.
        /// </summary>
        public bool SupportsPaging { get; }

        public Features(
            bool canParameterizeQueries,
            bool supportsServerSideProjection,
            bool supportsDedup,
            bool supportsOrdering,
            bool supportsPaging)
        {
            CanParameterizeQueries = canParameterizeQueries;
            SupportsServerSideProjection = supportsServerSideProjection;
            SupportsDedup = supportsDedup;
            SupportsOrdering = supportsOrdering;
            SupportsPaging = supportsPaging;
        }

        public static Features Default => new Features(
            canParameterizeQueries: false,
            supportsServerSideProjection: true,
            supportsDedup: true,
            supportsOrdering: true,
            supportsPaging: true);
    }
}
