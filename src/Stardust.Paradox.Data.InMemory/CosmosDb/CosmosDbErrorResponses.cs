#if NET8_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using Stardust.Paradox.Data.InMemory.Server;

namespace Stardust.Paradox.Data.InMemory.CosmosDb
{
    /// <summary>
    /// Creates Cosmos DB-compatible error responses for the Gremlin server.
    /// Mimics the exact error format returned by Azure Cosmos DB Gremlin API.
    /// Note: This class is only available in .NET Core 3.1+ and .NET 5+.
    /// </summary>
    public static class CosmosDbErrorResponses
    {
        /// <summary>
        /// Cosmos DB-specific status codes
        /// </summary>
        public static class StatusCodes
        {
            public const int Success = 200;
            public const int NoContent = 204;
            public const int BadRequest = 400;
            public const int Unauthorized = 401;
            public const int Forbidden = 403;
            public const int NotFound = 404;
            public const int RequestTimeout = 408;
            public const int Conflict = 409;
            public const int Gone = 410;
            public const int PreconditionFailed = 412;
            public const int RequestEntityTooLarge = 413;
            public const int TooManyRequests = 429;
            public const int RetryWith = 449;
            public const int InternalServerError = 500;
            public const int ServiceUnavailable = 503;
        }
        
        /// <summary>
        /// Cosmos DB-specific substatus codes
        /// </summary>
        public static class SubStatusCodes
        {
            public const string Unknown = "0";
            public const string InvalidQuery = "1009";
            public const string PartitionKeyMismatch = "1001";
            public const string CrossPartitionQueryRequired = "1004";
            public const string RUExceeded = "3200";
            public const string TooManyRequests = "3200";
            public const string RequestTimeout = "3206";
            public const string QueryExecutionTimeout = "3206";
        }
        
        /// <summary>
        /// Create a rate limited (429) response
        /// </summary>
        public static TinkerPopResponse CreateRateLimitedResponse(Guid requestId, int retryAfterMs, double requestedRU = 0, double availableRU = 0)
        {
            var activityId = Guid.NewGuid().ToString();
            
            return new TinkerPopResponse
            {
                RequestId = requestId,
                Status = new TinkerPopStatus
                {
                    Code = StatusCodes.TooManyRequests,
                    Message = $"Request rate is large. More Request Units may be needed, so no changes were made. " +
                             $"Please retry this request later. ActivityId: {activityId}, " +
                             $"Requested RU: {requestedRU:F2}, Available RU: {availableRU:F2}",
                    Attributes = new Dictionary<string, object>
                    {
                        ["x-ms-retry-after-ms"] = retryAfterMs.ToString(),
                        ["x-ms-substatus"] = SubStatusCodes.RUExceeded,
                        ["x-ms-activity-id"] = activityId,
                        ["x-ms-request-charge"] = "0",
                        ["x-ms-total-request-charge"] = "0"
                    }
                },
                Result = new TinkerPopResult
                {
                    Data = new List<object>(),
                    Meta = new Dictionary<string, object>()
                }
            };
        }
        
        /// <summary>
        /// Create an unsupported step (400) response
        /// </summary>
        public static TinkerPopResponse CreateUnsupportedStepResponse(Guid requestId, string stepName, string reason = null)
        {
            var activityId = Guid.NewGuid().ToString();
            var message = reason ?? CosmosDbGremlinLimitations.GetUnsupportedReason(stepName);
            
            return new TinkerPopResponse
            {
                RequestId = requestId,
                Status = new TinkerPopStatus
                {
                    Code = StatusCodes.BadRequest,
                    Message = $"ActivityId: {activityId}, Microsoft.Azure.Documents.DocumentClientException, " +
                             $"Message: Gremlin step '{stepName}' is not supported. {message}",
                    Attributes = new Dictionary<string, object>
                    {
                        ["x-ms-substatus"] = SubStatusCodes.InvalidQuery,
                        ["x-ms-activity-id"] = activityId,
                        ["x-ms-request-charge"] = "0"
                    }
                },
                Result = new TinkerPopResult
                {
                    Data = new List<object>(),
                    Meta = new Dictionary<string, object>()
                }
            };
        }
        
        /// <summary>
        /// Create a partition key required (400) response
        /// </summary>
        public static TinkerPopResponse CreatePartitionKeyRequiredResponse(Guid requestId, string partitionKeyPath)
        {
            var activityId = Guid.NewGuid().ToString();
            
            return new TinkerPopResponse
            {
                RequestId = requestId,
                Status = new TinkerPopStatus
                {
                    Code = StatusCodes.BadRequest,
                    Message = $"ActivityId: {activityId}, " +
                             $"Cross partition query is required but disabled. " +
                             $"Please set 'x-ms-documentdb-query-enablecrosspartition' to true, " +
                             $"supply partition key ('{partitionKeyPath}') in request, " +
                             $"or revise your query to target a single partition.",
                    Attributes = new Dictionary<string, object>
                    {
                        ["x-ms-substatus"] = SubStatusCodes.CrossPartitionQueryRequired,
                        ["x-ms-activity-id"] = activityId,
                        ["x-ms-request-charge"] = "0"
                    }
                },
                Result = new TinkerPopResult
                {
                    Data = new List<object>(),
                    Meta = new Dictionary<string, object>()
                }
            };
        }
        
        /// <summary>
        /// Create a timeout (408) response
        /// </summary>
        public static TinkerPopResponse CreateTimeoutResponse(Guid requestId, int timeoutMs)
        {
            var activityId = Guid.NewGuid().ToString();
            
            return new TinkerPopResponse
            {
                RequestId = requestId,
                Status = new TinkerPopStatus
                {
                    Code = StatusCodes.RequestTimeout,
                    Message = $"ActivityId: {activityId}, " +
                             $"Request timed out. Query execution exceeded the maximum allowed execution time of {timeoutMs}ms.",
                    Attributes = new Dictionary<string, object>
                    {
                        ["x-ms-substatus"] = SubStatusCodes.QueryExecutionTimeout,
                        ["x-ms-activity-id"] = activityId,
                        ["x-ms-request-charge"] = "0"
                    }
                },
                Result = new TinkerPopResult
                {
                    Data = new List<object>(),
                    Meta = new Dictionary<string, object>()
                }
            };
        }
        
        /// <summary>
        /// Create a not found (404) response
        /// </summary>
        public static TinkerPopResponse CreateNotFoundResponse(Guid requestId, string resourceType, string resourceId)
        {
            var activityId = Guid.NewGuid().ToString();
            
            return new TinkerPopResponse
            {
                RequestId = requestId,
                Status = new TinkerPopStatus
                {
                    Code = StatusCodes.NotFound,
                    Message = $"ActivityId: {activityId}, " +
                             $"Resource Not Found. {resourceType} with id '{resourceId}' was not found.",
                    Attributes = new Dictionary<string, object>
                    {
                        ["x-ms-substatus"] = SubStatusCodes.Unknown,
                        ["x-ms-activity-id"] = activityId,
                        ["x-ms-request-charge"] = "1"
                    }
                },
                Result = new TinkerPopResult
                {
                    Data = new List<object>(),
                    Meta = new Dictionary<string, object>()
                }
            };
        }
        
        /// <summary>
        /// Create a conflict (409) response for duplicate IDs
        /// </summary>
        public static TinkerPopResponse CreateConflictResponse(Guid requestId, string resourceType, string resourceId)
        {
            var activityId = Guid.NewGuid().ToString();
            
            return new TinkerPopResponse
            {
                RequestId = requestId,
                Status = new TinkerPopStatus
                {
                    Code = StatusCodes.Conflict,
                    Message = $"ActivityId: {activityId}, " +
                             $"Resource with id '{resourceId}' already exists. {resourceType} IDs must be unique.",
                    Attributes = new Dictionary<string, object>
                    {
                        ["x-ms-substatus"] = SubStatusCodes.Unknown,
                        ["x-ms-activity-id"] = activityId,
                        ["x-ms-request-charge"] = "1"
                    }
                },
                Result = new TinkerPopResult
                {
                    Data = new List<object>(),
                    Meta = new Dictionary<string, object>()
                }
            };
        }
        
        /// <summary>
        /// Create a request entity too large (413) response
        /// </summary>
        public static TinkerPopResponse CreateRequestTooLargeResponse(Guid requestId, int sizeBytes, int maxSizeBytes)
        {
            var activityId = Guid.NewGuid().ToString();
            
            return new TinkerPopResponse
            {
                RequestId = requestId,
                Status = new TinkerPopStatus
                {
                    Code = StatusCodes.RequestEntityTooLarge,
                    Message = $"ActivityId: {activityId}, " +
                             $"Request size ({sizeBytes} bytes) exceeds the maximum allowed size ({maxSizeBytes} bytes).",
                    Attributes = new Dictionary<string, object>
                    {
                        ["x-ms-substatus"] = SubStatusCodes.Unknown,
                        ["x-ms-activity-id"] = activityId,
                        ["x-ms-request-charge"] = "0"
                    }
                },
                Result = new TinkerPopResult
                {
                    Data = new List<object>(),
                    Meta = new Dictionary<string, object>()
                }
            };
        }
        
        /// <summary>
        /// Create a success response with RU charge information
        /// </summary>
        public static TinkerPopResponse CreateSuccessResponse(Guid requestId, IEnumerable<object> data, double requestCharge)
        {
            var activityId = Guid.NewGuid().ToString();
            var dataList = data != null ? new List<object>(data) : new List<object>();
            
            return new TinkerPopResponse
            {
                RequestId = requestId,
                Status = new TinkerPopStatus
                {
                    Code = StatusCodes.Success,
                    Message = "",
                    Attributes = new Dictionary<string, object>
                    {
                        ["x-ms-activity-id"] = activityId,
                        ["x-ms-request-charge"] = requestCharge.ToString("F2"),
                        ["x-ms-total-request-charge"] = requestCharge.ToString("F2"),
                        ["x-ms-status-code"] = StatusCodes.Success.ToString()
                    }
                },
                Result = new TinkerPopResult
                {
                    Data = dataList,
                    Meta = new Dictionary<string, object>
                    {
                        ["x-ms-request-charge"] = requestCharge
                    }
                }
            };
        }
        
        /// <summary>
        /// Create a general error response
        /// </summary>
        public static TinkerPopResponse CreateErrorResponse(Guid requestId, int statusCode, string message, Exception exception = null)
        {
            var activityId = Guid.NewGuid().ToString();
            var attributes = new Dictionary<string, object>
            {
                ["x-ms-activity-id"] = activityId,
                ["x-ms-request-charge"] = "0"
            };
            
            if (exception != null)
            {
                attributes["x-ms-exception-type"] = exception.GetType().Name;
#if DEBUG
                attributes["x-ms-exception-stacktrace"] = exception.StackTrace;
#endif
            }
            
            return new TinkerPopResponse
            {
                RequestId = requestId,
                Status = new TinkerPopStatus
                {
                    Code = statusCode,
                    Message = $"ActivityId: {activityId}, {message}",
                    Attributes = attributes
                },
                Result = new TinkerPopResult
                {
                    Data = new List<object>(),
                    Meta = new Dictionary<string, object>()
                }
            };
        }
        
        /// <summary>
        /// Add Cosmos DB headers to an existing response
        /// </summary>
        public static void AddCosmosDbHeaders(TinkerPopResponse response, double requestCharge, 
            bool isCrossPartition = false, int serverTimeMs = 0)
        {
            if (response?.Status?.Attributes == null)
                return;
            
            var activityId = response.Status.Attributes.ContainsKey("x-ms-activity-id")
                ? response.Status.Attributes["x-ms-activity-id"]?.ToString()
                : Guid.NewGuid().ToString();
            
            response.Status.Attributes["x-ms-activity-id"] = activityId;
            response.Status.Attributes["x-ms-request-charge"] = requestCharge.ToString("F2");
            response.Status.Attributes["x-ms-total-request-charge"] = requestCharge.ToString("F2");
            
            if (isCrossPartition)
            {
                response.Status.Attributes["x-ms-documentdb-query-iscrosspartition"] = "true";
            }
            
            if (serverTimeMs > 0)
            {
                response.Status.Attributes["x-ms-server-time-ms"] = serverTimeMs.ToString();
            }
        }
    }
}
#endif
