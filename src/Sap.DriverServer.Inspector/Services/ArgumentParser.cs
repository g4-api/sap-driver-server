using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Parses the argument text of the generic member invoker into typed COM arguments.
    /// </summary>
    /// <remarks>
    /// Arguments are separated by commas outside quotes. Each token becomes, in order of precedence: a string when
    /// it is quoted with <c>"</c> or <c>'</c>, a Boolean for <c>true</c> or <c>false</c>, an <see cref="int"/>, a
    /// <see cref="double"/> (invariant culture), or otherwise the trimmed text as a string. The parser is
    /// compute-only and fully unit-testable.
    /// </remarks>
    public static class ArgumentParser
    {
        #region *** Methods      ***
        /// <summary>
        /// Converts argument text into typed arguments.
        /// </summary>
        /// <param name="text">The argument text, such as <c>"&amp;MB_EXPORT", 2, true</c>.</param>
        /// <returns>The arguments; empty for null or whitespace text.</returns>
        /// <exception cref="FormatException">A quoted argument is not terminated, or text follows a closing quote.</exception>
        public static object[] ConvertToArguments(string text)
        {
            // Treat empty input as "no arguments".
            if (string.IsNullOrWhiteSpace(text))
            {
                return [];
            }

            // Split into raw tokens, then type each one.
            var arguments = new List<object>();
            foreach (var (token, isQuoted) in SplitTokens(text))
            {
                arguments.Add(isQuoted ? token : ConvertToken(token));
            }

            return [.. arguments];
        }

        // Types one unquoted token: Boolean, integer, floating point, or plain string.
        private static object ConvertToken(string token)
        {
            if (bool.TryParse(token, out var boolean))
            {
                return boolean;
            }

            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                return integer;
            }

            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return number;
            }

            return token;
        }

        // Splits argument text on commas outside quotes, returning each trimmed token and whether it was quoted.
        private static List<(string Token, bool IsQuoted)> SplitTokens(string text)
        {
            var tokens = new List<(string Token, bool IsQuoted)>();
            var current = new StringBuilder();
            var quote = '\0';
            var isQuoted = false;

            foreach (var character in text)
            {
                // Inside quotes everything is literal until the matching quote.
                if (quote != '\0')
                {
                    if (character == quote)
                    {
                        quote = '\0';
                    }
                    else
                    {
                        current.Append(character);
                    }

                    continue;
                }

                // A comma ends the current token.
                if (character == ',')
                {
                    tokens.Add((isQuoted ? current.ToString() : current.ToString().Trim(), isQuoted));
                    current.Clear();
                    isQuoted = false;
                    continue;
                }

                // An opening quote is allowed only at the start of a token; text after a closing quote is an error.
                if (character is '"' or '\'')
                {
                    if (isQuoted || current.ToString().Trim().Length > 0)
                    {
                        throw new FormatException("A quote can only start an argument.");
                    }

                    current.Clear();
                    quote = character;
                    isQuoted = true;
                    continue;
                }

                if (isQuoted && !char.IsWhiteSpace(character))
                {
                    throw new FormatException("Text cannot follow a closing quote; separate arguments with commas.");
                }

                current.Append(character);
            }

            // Reject an unterminated quote, then add the final token.
            if (quote != '\0')
            {
                throw new FormatException("A quoted argument is not terminated.");
            }

            tokens.Add((isQuoted ? current.ToString() : current.ToString().Trim(), isQuoted));

            return tokens;
        }
        #endregion
    }
}
