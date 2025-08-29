namespace Stardust.Paradox.Data.InMemory;

/// <summary>
/// Token types for Gremlin query parsing
/// </summary>
public enum TokenType
{
    Identifier,     // g, V, E, addV, etc.
    OpenParen,      // (
    CloseParen,     // )
    Dot,            // .
    String,         // 'value' or "value"
    Number,         // 123, 123.45
    Comma,          // ,
    Boolean,        // true, false
    Null,           // null
    Operator,       // eq, neq, gt, lt, etc.
    EOF,            // End of input
    Unknown
}