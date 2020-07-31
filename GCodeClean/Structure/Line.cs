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

        private List<Token> _tokens;

        /// <summary>
        /// Get/Set  the current list of all Tokens, get includes any line number token
        /// </summary>
        public List<Token> AllTokens
        {
            get
            {
                // Always manipulate the returned list of tokens to put any line number first
                // Even though we are doing this below in the set
                return _tokens.Where(t => t.IsLineNumber).Take(1).Concat(_tokens.Where(t => !t.IsLineNumber)).ToList();
            }
            set
            {
                _tokens = value.Where(t => t.IsLineNumber).Take(1).Concat(value.Where(t => !t.IsLineNumber)).ToList();
            }
        }

        /// <summary>
        /// Gets the current list of Tokens, does not include any line number token.
        /// </summary>
        public List<Token> Tokens {
            get
            {
                return _tokens.Where(t => !t.IsLineNumber).ToList();
            }
        }

        public string Source
        {
            get => _source;
            set
            {
                _source = value;

                IsValid = true;
                IsFileTerminator = false;
                HasLineNumber = false;

                if (_tokens is null)
                {
                    _tokens = new List<Token>();
                }

                if (string.IsNullOrWhiteSpace(_source))
                {
                    IsEmptyOrWhiteSpace = true;
                    return;
                }

                AllTokens = _source.Tokenise().Select(s => new Token(s)).ToList();

                if (Tokens.Any(t => t.IsFileTerminator))
                {
                    // Check the file terminator character is the only thing on the line
                    IsFileTerminator = true;
                    IsValid = Tokens.Count == 1;
                    return;
                }

                if (!AllTokens.Any(t => t.IsLineNumber))
                {
                    return;
                }

                // We have a line number
                HasLineNumber = true;
            }
        }

        public bool IsFileTerminator { get; private set; }

        public bool IsEmptyOrWhiteSpace { get; private set; }

        public bool IsValid { get; private set; }

        public bool HasLineNumber { get; private set; }

        public bool HasTokens(List<char> codes)
        {
            return AllTokens.Any(t => codes.Contains(t.Code));
        }

        public bool HasTokens(IEnumerable<string> tokens)
        {
            var parsedTokens = tokens.Select(t => new Token(t));
            return HasTokens(parsedTokens);
        }

        public bool HasTokens(IEnumerable<Token> tokens)
        {
            return AllTokens.Any(tokens.Contains);
        }

        public bool HasToken(char code)
        {
            return AllTokens.Any(t => t.Code == code);
        }

        public bool HasToken(string token)
        {
            var parsedToken = new Token(token);
            return HasToken(parsedToken);
        }

        public bool HasToken(Token token)
        {
            return AllTokens.Any(t => t == token);
        }

        /// <summary>
        /// Roughly equivalent to `IsNullOrWhiteSpace` this returns true if there are:
        /// * no tokens,
        /// * only a file terminator,
        /// * only one or more comments
        /// </summary>
        public bool IsNotCommandCodeOrArguments()
        {
            return AllTokens.Count == 0 || AllTokens.All(t => t.IsFileTerminator) || AllTokens.All(t => t.IsComment);
        }

        /// <summary>
        /// This returns true if there are one or more Arguments but no Commands or Codes.
        /// Line numbers, comments, codes are ignored for this test
        /// </summary>
        public bool IsArgumentsOnly()
        {
            if (IsNotCommandCodeOrArguments())
            {
                return false;
            }

            return HasTokens(Token.Arguments.ToList()) && !HasTokens(Token.Commands.ToList()) && !HasTokens(Token.Codes.ToList());
        }

        /// <summary>
        /// This returns true if there are one or more Arguments and a movement Command, comments are ignored for this test
        /// </summary>
        public bool HasMovementCommand()
        {
            return !IsArgumentsOnly() && HasTokens(ModalGroup.ModalSimpleMotion);
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

        public void ClearTokens()
        {
            _tokens.Clear();
        }

        public void PrependToken(Token token)
        {
            _tokens.Insert(0, token);

            // This little hack handles situations where we have an existing line number
            // If the supplied token is a line number, then the existing line number will be eliminated
            // If the supplied toke is not a line number, then it will end up immediately after any line number
            _tokens = AllTokens;
        }

        public void AppendToken(Token token)
        {
            _tokens.Add(token);
        }

        public void AppendTokens(IEnumerable<Token> tokens)
        {
            _tokens.AddRange(tokens);
        }

        public List<Token> RemoveTokens(List<char> codes)
        {
            var removedTokens = new List<Token>();

            for (var ix = _tokens.Count - 1; ix >= 0; ix--)
            {
                if (!codes.Contains(_tokens[ix].Code))
                {
                    continue;
                }

                removedTokens.Add(_tokens[ix]);
                _tokens.RemoveAt(ix);
            }

            return removedTokens;
        }

        public List<Token> RemoveTokens(List<Token> tokens)
        {
            var removedTokens = new List<Token>();

            for (var ix = _tokens.Count - 1; ix >= 0; ix--)
            {
                if (!tokens.Contains(_tokens[ix]))
                {
                    continue;
                }

                removedTokens.Add(_tokens[ix]);
                _tokens.RemoveAt(ix);
            }

            return removedTokens;
        }

        /// <summary>
        /// Compares two lines to ensure they are `compatible`.
        /// Ignores any line number tokens
        /// </summary>
        public bool IsCompatible(Line lineB)
        {
            var aTokens = Tokens;
            var bTokens = lineB.Tokens;
            if (aTokens.Count != bTokens.Count)
            {
                return false;
            }

            var isCompatible = true;
            for (var ix = 0; ix < bTokens.Count; ix++)
            {
                if (aTokens[ix].Code != bTokens[ix].Code)
                {
                    isCompatible = false;
                    break;
                }

                if (aTokens[ix].Code != 'G' && aTokens[ix].Code != 'M')
                {
                    continue;
                }

                // For 'Commands' the whole thing must be the same
                if (aTokens[ix] == bTokens[ix])
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

        /// <summary>
        /// Compare two lines for equality.
        /// Note that line numbers are not included in the comparison
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
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
            return string.Join(" ", AllTokens).Trim();
        }
    }
}
