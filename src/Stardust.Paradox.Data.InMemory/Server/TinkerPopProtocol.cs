using System;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.InMemory.Server
{
    /// <summary>
    /// TinkerPop WebSocket protocol message structure
    /// Based on Apache TinkerPop's gremlin-driver implementation
    /// </summary>
    public class TinkerPopMessage
    {
        /// <summary>
        /// Unique request identifier
        /// </summary>
        public Guid RequestId { get; set; }

        /// <summary>
        /// Operation type (eval, authentication, etc.)
        /// </summary>
        public string Op { get; set; }

        /// <summary>
        /// Processor to handle the request (default is "")
        /// </summary>
        public string Processor { get; set; } = "";

        /// <summary>
        /// Operation arguments
        /// </summary>
        public Dictionary<string, object> Args { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// TinkerPop WebSocket response message structure
    /// </summary>
    public class TinkerPopResponse
    {
        /// <summary>
        /// Request identifier this response is for
        /// </summary>
        public Guid RequestId { get; set; }

        /// <summary>
        /// Response status
        /// </summary>
        public TinkerPopStatus Status { get; set; } = new TinkerPopStatus();

        /// <summary>
        /// Response result
        /// </summary>
        public TinkerPopResult Result { get; set; } = new TinkerPopResult();
    }

    /// <summary>
    /// TinkerPop response status
    /// </summary>
    public class TinkerPopStatus
    {
        /// <summary>
        /// Status message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Status code (200 = success, 500 = error, etc.)
        /// Using int for Gremlin.Net compatibility - the serializers will handle conversion appropriately
        /// </summary>
        public int Code { get; set; } = 200;

        /// <summary>
        /// Additional status attributes
        /// </summary>
        public Dictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// TinkerPop response result
    /// </summary>
    public class TinkerPopResult
    {
        /// <summary>
        /// Result data
        /// </summary>
        public List<object> Data { get; set; } = new List<object>();

        /// <summary>
        /// Result metadata
        /// </summary>
        public Dictionary<string, object> Meta { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// TinkerPop status codes
    /// Based on Apache TinkerPop reference implementation
    /// </summary>
    public static class TinkerPopStatusCodes
    {
        public const int Success = 200;
        public const int NoContent = 204;
        public const int PartialContent = 206;
        public const int Unauthorized = 401;
        public const int Authenticate = 407;
        public const int MalformedRequest = 498;
        public const int InvalidRequestArguments = 499;
        public const int ServerError = 500;
        public const int ScriptEvaluationError = 597;
        public const int ServerTimeout = 598;
        public const int ServerSerializationError = 599;
    }

    /// <summary>
    /// TinkerPop operation types
    /// </summary>
    public static class TinkerPopOperations
    {
        public const string Eval = "eval";
        public const string Close = "close";
        public const string Authentication = "authentication";
        public const string Invalid = "invalid";
    }

    /// <summary>
    /// TinkerPop MIME types for different GraphSON versions
    /// </summary>
    public static class TinkerPopMimeTypes
    {
        public const string GraphSONV1 = "application/vnd.gremlin-v1.0+json";
        public const string GraphSONV2 = "application/vnd.gremlin-v2.0+json";
        public const string GraphSONV3 = "application/vnd.gremlin-v3.0+json";
        public const string Json = "application/json";
    }

    /// <summary>
    /// Interface for TinkerPop-compatible serializers
    /// </summary>
    public interface ITinkerPopSerializer
    {
        string SerializeResponse(TinkerPopResponse response);
        TinkerPopMessage DeserializeMessage(string json);
        TinkerPopResponse CreateErrorResponse(Guid requestId, int statusCode, string message, Exception exception = null);
        TinkerPopResponse CreateSuccessResponse(Guid requestId, IEnumerable<dynamic> data, Dictionary<string, object> meta = null);
    }
}
