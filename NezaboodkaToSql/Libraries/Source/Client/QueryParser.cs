using System;
using System.Collections.Generic;

namespace Nezaboodka
{
    public class QueryParser
    {
        public struct Token
        {
            public TokenId Id;
            public string Text;
            public int Position;
        }

        public enum TokenId
        {
            Unknown,
            End,
            Identifier,
            StringLiteral,
            IntegerLiteral,
            RealLiteral,
            Tilde,
            Exclamation,
            OpenBrace,
            CloseBrace,
            Percent,
            OpenParen,
            CloseParen,
            Asterisk,
            Plus,
            Comma,
            Minus,
            Dot,
            Slash,
            Colon,
            LessThan,
            Equal,
            GreaterThan,
            Question,
            DoubleQuestion,
            OpenBracket,
            CloseBracket,
            ExclamationEqual,
            DoubleAmphersand,
            LessThanEqual,
            LessGreater,
            DoubleEqual,
            GreaterThanEqual,
            DoubleBar
        }

        private string fText;
        private int fTextPosition;
        private char fCharacter;
        private Token fToken;

        public QueryParser(string text)
        {
            fText = text;
            fTextPosition = -1;
            NextCharacter();
            NextToken();
        }

        // Examples:
        // Group{Id, Title}
        // Group{Id, Title}, ExtGroup, AdminGroup~{Permissions}, HiddenGroup~
        public static List<TypeAndFields> ParseTypeAndFieldsList(string text)
        {
            var parser = new QueryParser(text);
            List<TypeAndFields> result = parser.ParseTypeAndFieldsList();
            parser.ValidateToken(TokenId.End, ErrorMessage.SyntaxError);
            return result;
        }

        // Examples:
        // Group{Id, Title}
        // ExtGroup
        // AdminGroup~{Permissions}
        // HiddenGroup~
        public static TypeAndFields ParseTypeAndFields(string text)
        {
            var parser = new QueryParser(text);
            TypeAndFields result = parser.ParseTypeAndFields();
            parser.ValidateToken(TokenId.End, ErrorMessage.SyntaxError);
            return result;
        }

        private List<TypeAndFields> ParseTypeAndFieldsList()
        {
            TypeAndFields item = ParseTypeAndFields();
            List<TypeAndFields> result = new List<TypeAndFields>();
            result.Add(item);
            while (fToken.Id == TokenId.Comma)
            {
                NextToken();
                item = ParseTypeAndFields();
                result.Add(item);
            }
            return result;
        }

        private TypeAndFields ParseTypeAndFields()
        {
            TypeAndFields result = new TypeAndFields();
            result.TypeName = ParseIdentifier();
            if (fToken.Id == TokenId.Tilde)
            {
                result.Inversion = true;
                NextToken();
            }
            if (fToken.Id == TokenId.OpenBrace)
                result.FieldNames = ParseFieldNameList();
            return result;
        }

        private string ParseTypeName()
        {
            return ParseIdentifier();
        }

        private List<string> ParseFieldNameList()
        {
            ValidateToken(TokenId.OpenBrace, ErrorMessage.OpenBraceExpected);
            NextToken();
            List<string> result = null; 
            if (fToken.Id != TokenId.CloseBrace)
                result = ParseFieldNames();
            ValidateToken(TokenId.CloseBrace, ErrorMessage.CloseBraceOrCommaExpected);
            NextToken();
            return result;
        }

        private List<string> ParseFieldNames()
        {
            List<string> result = new List<string>();
            bool stop = false;
            while (!stop)
            {
                result.Add(ParseIdentifier());
                if (fToken.Id == TokenId.Comma)
                    NextToken();
                else
                    stop = true;
            }
            return result;
        }

        private string ParseIdentifier()
        {
            ValidateToken(TokenId.Identifier, ErrorMessage.IdentifierExpected);
            string result = fToken.Text;
            NextToken();
            return result;
        }

        private void NextToken()
        {
            while (char.IsWhiteSpace(fCharacter))
                NextCharacter();
            TokenId tokenId = TokenId.Unknown;
            int tokenPosition = fTextPosition;
            switch (fCharacter)
            {
                case '!':
                    NextCharacter();
                    if (fCharacter == '=')
                    {
                        NextCharacter();
                        tokenId = TokenId.ExclamationEqual;
                    }
                    else
                        tokenId = TokenId.Exclamation;
                    break;
                case '%':
                    NextCharacter();
                    tokenId = TokenId.Percent;
                    break;
                case '&':
                    NextCharacter();
                    if (fCharacter == '&')
                    {
                        NextCharacter();
                        tokenId = TokenId.DoubleAmphersand;
                    }
                    break;
                case '(':
                    NextCharacter();
                    tokenId = TokenId.OpenParen;
                    break;
                case ')':
                    NextCharacter();
                    tokenId = TokenId.CloseParen;
                    break;
                case '*':
                    NextCharacter();
                    tokenId = TokenId.Asterisk;
                    break;
                case '+':
                    NextCharacter();
                    tokenId = TokenId.Plus;
                    break;
                case ',':
                    NextCharacter();
                    tokenId = TokenId.Comma;
                    break;
                case '-':
                    NextCharacter();
                    tokenId = TokenId.Minus;
                    break;
                case '.':
                    NextCharacter();
                    tokenId = TokenId.Dot;
                    break;
                case '/':
                    NextCharacter();
                    tokenId = TokenId.Slash;
                    break;
                case ':':
                    NextCharacter();
                    tokenId = TokenId.Colon;
                    break;
                case '<':
                    NextCharacter();
                    switch (fCharacter)
                    {
                        case '=':
                            NextCharacter();
                            tokenId = TokenId.LessThanEqual;
                            break;
                        case '>':
                            NextCharacter();
                            tokenId = TokenId.LessGreater;
                            break;
                        default:
                            tokenId = TokenId.LessThan;
                            break;
                    }
                    break;
                case '=':
                    NextCharacter();
                    if (fCharacter == '=')
                    {
                        NextCharacter();
                        tokenId = TokenId.DoubleEqual;
                    }
                    else
                        tokenId = TokenId.Equal;
                    break;
                case '>':
                    NextCharacter();
                    if (fCharacter == '=')
                    {
                        NextCharacter();
                        tokenId = TokenId.GreaterThanEqual;
                    }
                    else
                        tokenId = TokenId.GreaterThan;
                    break;
                case '?':
                    NextCharacter();
                    if (fCharacter == '?')
                    {
                        NextCharacter();
                        tokenId = TokenId.DoubleQuestion;
                    }
                    else
                        tokenId = TokenId.Question;
                    break;
                case '~':
                    NextCharacter();
                    tokenId = TokenId.Tilde;
                    break;
                case '[':
                    NextCharacter();
                    tokenId = TokenId.OpenBracket;
                    break;
                case ']':
                    NextCharacter();
                    tokenId = TokenId.CloseBracket;
                    break;
                case '|':
                    NextCharacter();
                    if (fCharacter == '|')
                    {
                        NextCharacter();
                        tokenId = TokenId.DoubleBar;
                    }
                    break;
                case '{':
                    NextCharacter();
                    tokenId = TokenId.OpenBrace;
                    break;
                case '}':
                    NextCharacter();
                    tokenId = TokenId.CloseBrace;
                    break;
                case '"':
                case '\'':
                    char quote = fCharacter;
                    NextCharacter();
                    do
                    {
                        NextCharacter();
                        while (fTextPosition < fText.Length && fCharacter != quote)
                            NextCharacter();
                        if (fTextPosition != fText.Length)
                            NextCharacter();
                        else
                            throw ParseError(fTextPosition, ErrorMessage.UnterminatedStringLiteral);
                    } while (fCharacter == quote);
                    tokenId = TokenId.StringLiteral;
                    break;
                default:
                    if (char.IsLetter(fCharacter) || fCharacter == '_')
                    {
                        NextCharacter();
                        while (char.IsLetterOrDigit(fCharacter) || fCharacter == '_')
                            NextCharacter();
                        tokenId = TokenId.Identifier;
                        break;
                    }
                    if (char.IsDigit(fCharacter))
                    {
                        tokenId = TokenId.IntegerLiteral;
                        NextCharacter();
                        while (char.IsDigit(fCharacter))
                            NextCharacter();
                        if (fCharacter == '.')
                        {
                            tokenId = TokenId.RealLiteral;
                            NextCharacter();
                            ValidateDigit();
                            NextCharacter();
                            while (char.IsDigit(fCharacter))
                                NextCharacter();
                        }
                        if (fCharacter == 'E' || fCharacter == 'e')
                        {
                            tokenId = TokenId.RealLiteral;
                            NextCharacter();
                            if (fCharacter == '+' || fCharacter == '-')
                                NextCharacter();
                            ValidateDigit();
                            NextCharacter();
                            while (char.IsDigit(fCharacter))
                                NextCharacter();
                        }
                        if (fCharacter == 'F' || fCharacter == 'f')
                            NextCharacter();
                        break;
                    }
                    if (fTextPosition == fText.Length)
                    {
                        tokenId = TokenId.End;
                        break;
                    }
                    else
                        throw ParseError(fTextPosition, ErrorMessage.InvalidCharacter, fCharacter);
            }

            if (tokenId == TokenId.Unknown && fTextPosition == fText.Length)
            {
                tokenId = TokenId.End;
            }
            fToken.Id = tokenId;
            fToken.Text = fText.Substring(tokenPosition, fTextPosition - tokenPosition);
        }

        private void NextCharacter()
        {
            if (fTextPosition < fText.Length)
                fTextPosition++;
            if (fTextPosition < fText.Length)
                fCharacter = fText[fTextPosition];
            else
                fCharacter = '\0';
        }

        private void ValidateDigit()
        {
            if (!char.IsDigit(fCharacter))
                throw ParseError(fTextPosition, ErrorMessage.DigitExpected);
        }

        private void ValidateToken(TokenId tokenId, string errorMessage)
        {
            if (fToken.Id != tokenId)
                throw ParseError(fToken.Position, errorMessage);
        }

        private NezaboodkaParseException ParseError(int position, string format, params object[] args)
        {
            return new NezaboodkaParseException(
                string.Format(System.Globalization.CultureInfo.CurrentCulture, format, args), position);
        }
    }

    public sealed class NezaboodkaParseException : Exception
    {
        private readonly int fPosition;

        public NezaboodkaParseException(string message, int position)
            : base(message)
        {
            fPosition = position;
        }

        public int Position
        {
            get { return fPosition; }
        }

        public override string ToString()
        {
            return string.Format(ErrorMessage.ParseExceptionFormat, Message, fPosition);
        }
    }

    internal static class ErrorMessage
    {
        public const string ParseExceptionFormat = "{0} (at index {1})";
        public const string InvalidCharacter = "Invalid character '{0}'";
        public const string SyntaxError = "Syntax error";
        public const string UnterminatedStringLiteral = "Unterminated string literal";
        public const string DigitExpected = "Digit expected";
        public const string IdentifierExpected = "Identifier expected";
        public const string OpenBraceExpected = "'{' expected";
        public const string CloseBraceOrCommaExpected = "'}' or ',' expected";
    }
}
