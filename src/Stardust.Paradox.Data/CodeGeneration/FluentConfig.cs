using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.CodeGeneration
{
    internal class FluentConfig
    {
        public string EdgeLabel { get; set; }

        public string ReverseEdgeLabel { get; set; }

        public string Query { get; set; }

        public bool Inline { get; set; }
        public SerializationType? Serialization { get; set; }
        public bool EagerLoading { get; set; }
    }
}