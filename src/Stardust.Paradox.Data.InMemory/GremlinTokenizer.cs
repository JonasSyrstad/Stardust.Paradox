using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// Advanced tokenizer for Gremlin queries
    /// </summary>
    public class GremlinTokenizer
    {
        private readonly string _input;
        private int _position;
        private readonly List<Token> _tokens;

        private static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "g", "V", "E", "addV", "addE", "property", "properties", "values", "valueMap", "elementMap",
            "has", "hasLabel", "hasId", "hasKey", "hasValue", "hasNot",
            "in", "out", "both", "inE", "outE", "bothE", "inV", "outV", "bothV",
            "to", "from", "as", "select", "where", "is", "not",
            "and", "or", "filter", "range", "limit", "skip", "tail", "sample",
            "order", "by", "asc", "desc", "shuffle",
            "group", "groupCount", "count", "sum", "max", "min", "mean", "fold",
            "unfold", "path", "simplePath", "cyclicPath", "project",
            "union", "coalesce", "choose", "optional", "repeat", "until", "emit", "times",
            "local", "constant", "identity", "drop", "dedup", "unique",
            "aggregate", "store", "cap", "barrier", "coin", "tree",
            "true", "false", "null", "eq", "neq", "lt", "lte", "gt", "gte",
            "within", "without", "containing", "notContaining", "startingWith", "endingWith",
            "inside", "outside", "between"
        };

        public GremlinTokenizer(string input)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _position = 0;
            _tokens = new List<Token>();
        }

        /// <summary>
        /// Tokenize the input string
        /// </summary>
        public List<Token> Tokenize()
        {
            _tokens.Clear();
            _position = 0;

            while (_position < _input.Length)
            {
                SkipWhitespace();
                
                if (_position >= _input.Length)
                    break;

                var token = ReadNextToken();
                if (token != null)
                {
                    _tokens.Add(token);
                }
            }

            _tokens.Add(new Token(TokenType.EOF, "", _position));
            return _tokens;
        }

        private void SkipWhitespace()
        {
            while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
            {
                _position++;
            }
        }

        private Token ReadNextToken()
        {
            if (_position >= _input.Length)
                return null;

            var currentChar = _input[_position];
            var startPosition = _position;

            switch (currentChar)
            {
                case '(':
                    _position++;
                    return new Token(TokenType.OpenParen, "(", startPosition);
                
                case ')':
                    _position++;
                    return new Token(TokenType.CloseParen, ")", startPosition);
                
                case '.':
                    _position++;
                    return new Token(TokenType.Dot, ".", startPosition);
                
                case ',':
                    _position++;
                    return new Token(TokenType.Comma, ",", startPosition);
                
                case '\'':
                case '"':
                    return ReadString(currentChar, startPosition);
                
                default:
                    if (char.IsDigit(currentChar) || currentChar == '-')
                    {
                        return ReadNumber(startPosition);
                    }
                    else if (char.IsLetter(currentChar) || currentChar == '_')
                    {
                        return ReadIdentifier(startPosition);
                    }
                    else
                    {
                        _position++;
                        return new Token(TokenType.Unknown, currentChar.ToString(), startPosition);
                    }
            }
        }

        private Token ReadString(char quote, int startPosition)
        {
            _position++; // Skip opening quote
            var value = "";

            while (_position < _input.Length && _input[_position] != quote)
            {
                if (_input[_position] == '\\' && _position + 1 < _input.Length)
                {
                    _position++; // Skip escape character
                    value += _input[_position]; // Add escaped character
                }
                else
                {
                    value += _input[_position];
                }
                _position++;
            }

            if (_position < _input.Length && _input[_position] == quote)
            {
                _position++; // Skip closing quote
            }

            return new Token(TokenType.String, value, startPosition);
        }

        private Token ReadNumber(int startPosition)
        {
            var value = "";
            var hasDecimal = false;

            if (_input[_position] == '-')
            {
                value += '-';
                _position++;
            }

            while (_position < _input.Length && (char.IsDigit(_input[_position]) || _input[_position] == '.'))
            {
                if (_input[_position] == '.')
                {
                    if (hasDecimal)
                        break; // Second decimal point, stop
                    hasDecimal = true;
                }
                value += _input[_position];
                _position++;
            }

            return new Token(TokenType.Number, value, startPosition);
        }

        private Token ReadIdentifier(int startPosition)
        {
            var value = "";

            while (_position < _input.Length && (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
            {
                value += _input[_position];
                _position++;
            }

            var tokenType = TokenType.Identifier;

            if (value == "true" || value == "false")
            {
                tokenType = TokenType.Boolean;
            }
            else if (value == "null")
            {
                tokenType = TokenType.Null;
            }
            else if (IsOperator(value))
            {
                tokenType = TokenType.Operator;
            }

            return new Token(tokenType, value, startPosition);
        }

        private bool IsOperator(string value)
        {
            return new[] { "eq", "neq", "lt", "lte", "gt", "gte", "within", "without", 
                          "containing", "notContaining", "startingWith", "endingWith",
                          "inside", "outside", "between" }.Contains(value);
        }
    }
}