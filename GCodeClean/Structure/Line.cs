// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.Linq;

using GCodeClean.Processing;

namespace GCodeClean.Structure
{
    public class Line
    {
        private string _source;

        public List<Token> Tokens { get; set; }

        public string Source
        {
            get => _source;
            set
            {
                _source = value;

                IsValid = true;
                IsFileTerminator = false;

                if (Tokens is null)
                {
                    Tokens = new List<Token>();
                }

                if (string.IsNullOrWhiteSpace(_source))
                {
                    IsEmptyOrWhiteSpace = true;
                    return;
                }

                var stringTokens = _source.Tokenise();
                foreach (var stringToken in stringTokens)
                {
                    Tokens.Add(new Token(stringToken));
                }

                if (Tokens.Any(t => t.IsFileTerminator))
                {
                    // Check the file terminator character is the only thing on the line
                    IsFileTerminator = true;
                    IsValid = Tokens.Count > 1;
                    return;
                }

                if (Tokens.All(t => t.Code != 'N') || Tokens[0].Code == 'N')
                {
                    return;
                }

                // If there's a line number, make sure it is the first token on the line
                IsValid = false;
            }
        }

        public bool IsFileTerminator { get; private set; }

        public bool IsEmptyOrWhiteSpace { get; private set; }

        public bool IsValid { get; private set; }

        public bool HasTokens(List<char> codes)
        {
            return Tokens.Any(t => codes.Contains(t.Code));
        }

        public bool HasTokens(IEnumerable<string> tokens)
        {
            var parsedTokens = tokens.Select(t => new Token(t));
            return HasTokens(parsedTokens);
        }

        public bool HasTokens(IEnumerable<Token> tokens)
        {
            return Tokens.Any(tokens.Contains);
        }

        public bool HasToken(char code)
        {
            return Tokens.Any(t => t.Code == code);
        }

        public bool HasToken(string token)
        {
            var parsedToken = new Token(token);
            return HasToken(parsedToken);
        }

        public bool HasToken(Token token)
        {
            return Tokens.Any(t => t == token);
        }

        /// <summary>
        /// Roughly equivalent to `IsNullOrWhiteSpace` this returns true if there are:
        /// * no tokens,
        /// * only a file terminator,
        /// * only one or more comments
        /// </summary>
        public bool IsNotCommandCodeOrArguments()
        {
            return Tokens.Count == 0 || Tokens.All(t => t.IsFileTerminator) || Tokens.All(t => t.IsComment);
        }

        /// <summary>
        /// This returns true if there are one or more Arguments but no Commands or Codes, comments and codes are ignored for this test
        /// </summary>
        public bool IsArgumentsOnly()
        {
            if (this.IsNotCommandCodeOrArguments())
            {
                return false;
            }

            return HasTokens(Token.Arguments.ToList()) && !HasTokens(Token.Commands.ToList()) && !HasTokens(Token.Codes.ToList());
        }

        /// <summary>
        /// This returns true if there are one or more Arguments but no Commands, comments are ignored for this test
        /// </summary>
        public bool HasMovementCommand()
        {
            return !IsArgumentsOnly() && HasTokens(Token.MovementCommands.ToList());
        }

        public Line()
        {
            Source = "";
        }

        public Line(string source)
        {
            Source = source;
        }

        public Line(Line line)
        {
            Source = line.Source;
        }

        public Line(Token token)
        {
            Source = token.ToString();
        }

        public Line(IEnumerable<Token> tokens)
        {
            Source = string.Join(' ', tokens);
        }

        public List<Token> RemoveTokens(List<char> codes)
        {
            var removedTokens = new List<Token>();

            for (var ix = Tokens.Count - 1; ix >= 0; ix--)
            {
                if (!codes.Contains(Tokens[ix].Code))
                {
                    continue;
                }

                removedTokens.Add(Tokens[ix]);
                Tokens.RemoveAt(ix);
            }

            return removedTokens;
        }

        public List<Token> RemoveTokens(List<Token> tokens)
        {
            var removedTokens = new List<Token>();

            for (var ix = Tokens.Count - 1; ix >= 0; ix--)
            {
                if (!tokens.Contains(Tokens[ix]))
                {
                    continue;
                }

                removedTokens.Add(Tokens[ix]);
                Tokens.RemoveAt(ix);
            }

            return removedTokens;
        }

        /// <summary>
        /// Compares two lines to ensure they are `compatible`
        /// </summary>
        public bool IsCompatible(Line lineB)
        {
            if (Tokens.Count != lineB.Tokens.Count)
            {
                return false;
            }

            var isCompatible = true;
            for (var ix = 0; ix < lineB.Tokens.Count; ix++)
            {
                if (Tokens[ix].Code != lineB.Tokens[ix].Code)
                {
                    isCompatible = false;
                    break;
                }

                if (Tokens[ix].Code != 'G' && Tokens[ix].Code != 'M')
                {
                    continue;
                }

                // For 'Commands' the whole thing must be the same
                if (Tokens[ix] == lineB.Tokens[ix])
                {
                    continue;
                }
                isCompatible = false;
                break;
            }

            return isCompatible;
        }

        public static implicit operator Coord(Line line)
        {
            var coords = new Coord();
            foreach (var token in line.Tokens)
            {
                var value = token.Number;
                if (!value.HasValue)
                {
                    continue;
                }

                if (token.Code == 'X')
                {
                    coords.X = value.Value;
                    coords.Set |= CoordSet.X;
                }
                if (token.Code == 'Y')
                {
                    coords.Y = value.Value;
                    coords.Set |= CoordSet.Y;
                }
                if (token.Code == 'Z')
                {
                    coords.Z = value.Value;
                    coords.Set |= CoordSet.Z;
                }
            }

            return coords;
        }

        public static bool operator ==(Line a, Line b)
        {
            if (a is null || b is null)
            {
                return a is null && b is null;
            }

            if (a.Tokens.Count != b.Tokens.Count)
            {
                return false;
            }

            return !b.Tokens.Where((t, ix) => a.Tokens[ix] != t).Any();
        }

        public static bool operator !=(Line a, Line b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            // Compare run-time types.
            return GetType() == obj?.GetType() && this == (Line)obj;
        }

        public override int GetHashCode()
        {
            return Tokens.Select(t => t.GetHashCode()).GetHashCode();
        }
        
        public override string ToString()
        {
            return string.Join(" ", Tokens);
        }
    }
}
