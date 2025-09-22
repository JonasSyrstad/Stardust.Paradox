using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.ExecutionEngine;

namespace Stardust.Paradox.Data.InMemory.Tests;

/// <summary>
/// Tests for the Gremlin tokenizer
/// </summary>
public class GremlinTokenizerTests
{
    [Fact]
    public void Tokenize_SimpleQuery_ShouldReturnCorrectTokens()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("g.V()");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        tokens.Should().HaveCount(6); // g, ., V, (, ), EOF
        tokens[0].Type.Should().Be(TokenType.Identifier);
        tokens[0].Value.Should().Be("g");
        tokens[1].Type.Should().Be(TokenType.Dot);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[2].Value.Should().Be("V");
        tokens[3].Type.Should().Be(TokenType.OpenParen);
        tokens[4].Type.Should().Be(TokenType.CloseParen);
        tokens[5].Type.Should().Be(TokenType.EOF);
    }

    [Fact]
    public void Tokenize_QueryWithString_ShouldTokenizeCorrectly()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("g.V().hasLabel('person')");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);
        stringToken.Should().NotBeNull();
        stringToken!.Value.Should().Be("person");
    }

    [Fact]
    public void Tokenize_QueryWithNumber_ShouldTokenizeCorrectly()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("g.V().limit(10)");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        var numberToken = tokens.FirstOrDefault(t => t.Type == TokenType.Number);
        numberToken.Should().NotBeNull();
        numberToken!.Value.Should().Be("10");
    }

    [Fact]
    public void Tokenize_QueryWithFloat_ShouldTokenizeCorrectly()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("g.V().property('weight', 12.5)");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        var numberToken = tokens.FirstOrDefault(t => t.Type == TokenType.Number && t.Value.Contains("."));
        numberToken.Should().NotBeNull();
        numberToken!.Value.Should().Be("12.5");
    }

    [Fact]
    public void Tokenize_QueryWithBoolean_ShouldTokenizeCorrectly()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("g.V().property('active', true)");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        var boolToken = tokens.FirstOrDefault(t => t.Type == TokenType.Boolean);
        boolToken.Should().NotBeNull();
        boolToken!.Value.Should().Be("true");
    }

    [Fact]
    public void Tokenize_QueryWithNull_ShouldTokenizeCorrectly()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("g.V().property('optional', null)");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        var nullToken = tokens.FirstOrDefault(t => t.Type == TokenType.Null);
        nullToken.Should().NotBeNull();
        nullToken!.Value.Should().Be("null");
    }

    [Fact]
    public void Tokenize_QueryWithOperator_ShouldTokenizeCorrectly()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("g.V().has('age', gt(30))");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        var operatorToken = tokens.FirstOrDefault(t => t.Type == TokenType.Operator);
        operatorToken.Should().NotBeNull();
        operatorToken!.Value.Should().Be("gt");
    }

    [Fact]
    public void Tokenize_ComplexQuery_ShouldTokenizeCorrectly()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("g.V().hasLabel('person').has('age', 30).out('knows').values('name')");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        tokens.Should().NotBeEmpty();
        tokens.Count(t => t.Type == TokenType.Identifier).Should().BeGreaterThan(5);
        tokens.Count(t => t.Type == TokenType.Dot).Should().BeGreaterThan(4);
        tokens.Last().Type.Should().Be(TokenType.EOF);
    }

    [Fact]
    public void Tokenize_QueryWithWhitespace_ShouldIgnoreWhitespace()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("  g  .  V  (  )  ");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        tokens.Should().HaveCount(6); // g, ., V, (, ), EOF (whitespace ignored)
        tokens[0].Type.Should().Be(TokenType.Identifier);
        tokens[0].Value.Should().Be("g");
        tokens[1].Type.Should().Be(TokenType.Dot);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[2].Value.Should().Be("V");
        tokens[3].Type.Should().Be(TokenType.OpenParen);
        tokens[4].Type.Should().Be(TokenType.CloseParen);
        tokens[5].Type.Should().Be(TokenType.EOF);
    }

    [Fact]
    public void Tokenize_QueryWithEscapedString_ShouldHandleEscapes()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("g.V().property('name', 'John\\'s')");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        var stringTokens = tokens.Where(t => t.Type == TokenType.String).ToList();
        stringTokens.Should().HaveCount(2);
        stringTokens[1].Value.Should().Be("John's");
    }

    [Fact]
    public void Tokenize_QueryWithDoubleQuotes_ShouldTokenizeCorrectly()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("g.V().hasLabel(\"person\")");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);
        stringToken.Should().NotBeNull();
        stringToken!.Value.Should().Be("person");
    }

    [Fact]
    public void Tokenize_QueryWithNegativeNumber_ShouldTokenizeCorrectly()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("g.V().property('score', -10)");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        var numberToken = tokens.FirstOrDefault(t => t.Type == TokenType.Number && t.Value.StartsWith("-"));
        numberToken.Should().NotBeNull();
        numberToken!.Value.Should().Be("-10");
    }

    [Fact]
    public void Tokenize_EmptyString_ShouldReturnOnlyEOF()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        tokens.Should().HaveCount(1);
        tokens[0].Type.Should().Be(TokenType.EOF);
    }

    [Fact]
    public void Tokenize_UnknownCharacter_ShouldReturnUnknownToken()
    {
        // Arrange
        var tokenizer = new GremlinTokenizer("g.V()@");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        var unknownToken = tokens.FirstOrDefault(t => t.Type == TokenType.Unknown);
        unknownToken.Should().NotBeNull();
        unknownToken!.Value.Should().Be("@");
    }

    [Theory]
    [InlineData("eq")]
    [InlineData("neq")]
    [InlineData("gt")]
    [InlineData("gte")]
    [InlineData("lt")]
    [InlineData("lte")]
    [InlineData("within")]
    [InlineData("without")]
    [InlineData("containing")]
    [InlineData("notContaining")]
    [InlineData("startingWith")]
    [InlineData("endingWith")]
    [InlineData("inside")]
    [InlineData("outside")]
    [InlineData("between")]
    public void Tokenize_Operators_ShouldBeRecognizedAsOperators(string operatorName)
    {
        // Arrange
        var tokenizer = new GremlinTokenizer($"g.V().has('age', {operatorName}(30))");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        var operatorToken = tokens.FirstOrDefault(t => t.Type == TokenType.Operator && t.Value == operatorName);
        operatorToken.Should().NotBeNull();
    }

    [Theory]
    [InlineData("true", TokenType.Boolean)]
    [InlineData("false", TokenType.Boolean)]
    [InlineData("null", TokenType.Null)]
    public void Tokenize_Keywords_ShouldBeRecognizedCorrectly(string keyword, TokenType expectedType)
    {
        // Arrange
        var tokenizer = new GremlinTokenizer($"g.V().property('test', {keyword})");

        // Act
        var tokens = tokenizer.Tokenize();

        // Assert
        var keywordToken = tokens.FirstOrDefault(t => t.Type == expectedType && t.Value == keyword);
        keywordToken.Should().NotBeNull();
    }
}