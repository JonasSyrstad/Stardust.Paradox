namespace Stardust.Paradox.Data.InMemory.ExecutionEngine;

/// <summary>
/// Represents a token in the Gremlin query
/// </summary>
public class Token
{
    public TokenType Type { get; set; }
    public string Value { get; set; }
    public int Position { get; set; }

    public Token(TokenType type, string value, int position = 0)
    {
        Type = type;
        Value = value;
        Position = position;
    }

    public override string ToString()
    {
        return $"{Type}: {Value}";
    }
}
