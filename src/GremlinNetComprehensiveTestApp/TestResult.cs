using System;

namespace GremlinNetComprehensiveTestApp
{
    /// <summary>
    /// Simple test result container
    /// </summary>
    public class TestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Details { get; set; } = "";

        public TestResult()
        {
        }

        public TestResult(bool success, string message, string details = "")
        {
            Success = success;
            Message = message;
            Details = details;
        }

        public static TestResult CreateSuccess(string message, string details = "")
        {
            return new TestResult(true, message, details);
        }

        public static TestResult CreateFailure(string message, string details = "")
        {
            return new TestResult(false, message, details);
        }

        public override string ToString()
        {
            var status = Success ? "? SUCCESS" : "? FAILURE";
            var result = $"{status}: {Message}";
            if (!string.IsNullOrEmpty(Details))
            {
                result += $" ({Details})";
            }
            return result;
        }
    }
}