// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
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
        /// Get/Set the current list of all Tokens, get includes any line number token
        /// </summary>
        public List<Token> AllTokens
        {
            get
            {
                // Always manipulate the returned list of tokens to put any line number first
                // Even though we are doing this below in the set
                var lineNumberToken = _tokens.Where(t => t.IsLineNumber).Take(1);
                var allOtherTokens = _tokens.Where(t => !t.IsLineNumber);
#pragma warning disable S2365 // Properties should not make collection or array copies
                return lineNumberToken.Concat(allOtherTokens).ToList();
#pragma warning restore S2365 // Properties should not make collection or array copies
            }
            set
            {
                var lineNumberToken = value.Where(t => t.IsLineNumber).Take(1);
                var allOtherTokens = value.Where(t => !t.IsLineNumber);
                _tokens = lineNumberToken.Concat(allOtherTokens).ToList();
            }
        }

        /// <summary>
        /// Gets the current list of Tokens, does not include any line number token.
        /// </summary>
        public List<Token> Tokens {
            get
            {
#pragma warning disable S2365 // Properties should not make collection or array copies
                return _tokens.Where(t => !t.IsLineNumber).ToList();
#pragma warning restore S2365 // Properties should not make collection or array copies
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

                _tokens ??= [];

                if (string.IsNullOrWhiteSpace(_source))
                {
                    IsEmptyOrWhiteSpace = true;
                    return;
                }

                AllTokens = _source.Tokenise().Select(s => new Token(s)).ToList();

                if (Tokens.Exists(t => t.IsFileTerminator))
                {
                    // Check the file terminator character is the only thing on the line
                    IsFileTerminator = true;
                    IsValid = Tokens.Count == 1;
                    return;
                }

                if (!AllTokens.Exists(t => t.IsLineNumber))
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

        public bool HasTokens(List<char> codes) => AllTokens.Exists(t => codes.Contains(t.Code));

        public bool HasTokens(IEnumerable<string> tokens) {
            var parsedTokens = tokens.Select(t => new Token(t));
            return HasTokens(parsedTokens);
        }

        public bool HasTokens(IEnumerable<Token> tokens) => AllTokens.Exists(tokens.Contains);

        public bool HasToken(char code) => AllTokens.Exists(t => t.Code == code);

        public bool HasToken(string token) {
            var parsedToken = new Token(token);
            return HasToken(parsedToken);
        }

        public bool HasToken(Token token) => AllTokens.Exists(t => t == token);

        /// <summary>
        /// Roughly equivalent to `IsNullOrWhiteSpace` this returns true if there are:
        /// * no tokens,
        /// * only a file terminator,
        /// * only one or more comments
        /// </summary>
        public bool IsNotCommandCodeOrArguments() {
            return AllTokens.Count == 0 || AllTokens.TrueForAll(t => t.IsFileTerminator) || AllTokens.TrueForAll(t => t.IsComment);
        }

        /// <summary>
        /// This returns true if there are one or more Arguments but no Commands or Codes.
        /// Line numbers, comments, codes are ignored for this test
        /// </summary>
        public bool IsArgumentsOnly() {
            if (IsNotCommandCodeOrArguments()) {
                return false;
            }

            return HasTokens(Token.Arguments.ToList()) && !HasTokens(Token.Commands.ToList()) && !HasTokens(Token.Codes.ToList());
        }

        /// <summary>
        /// This returns true if there are one or more Arguments and a movement Command, comments are ignored for this test
        /// </summary>
        public bool HasMovementCommand() {
            return !IsArgumentsOnly() && HasTokens(ModalGroup.ModalAllMotion);
        }

        #region Constructors
        /// <summary>
        /// Create an empty line of GCode
        /// </summary>
        public Line() {
            Source = "";
        }

        /// <summary>
        /// Create a new line of GCode from the supplied string
        /// </summary>
        /// <param name="source"></param>
        public Line(string source) {
            Source = source;
        }

        /// <summary>
        /// Create a new line of GCode by copying an existing line (deep copy)
        /// </summary>
        /// <param name="line"></param>
        public Line(Line line)
        {
            _source = line.ToString();
            _tokens = line.AllTokens.Select(t => new Token(t)).ToList();
        }

        /// <summary>
        /// Create a new line of GCode from a single GCode token (deep copy)
        /// </summary>
        /// <param name="token"></param>
        public Line(Token token)
        {
            _source = token.ToString();
            _tokens = [new Token(token)];
        }

        /// <summary>
        /// Create a new line of GCode from a list of GCode tokens (deep copy)
        /// </summary>
        /// <param name="tokens"></param>
        public Line(IEnumerable<Token> tokens)
        {
            _source = string.Join(' ', tokens);
            _tokens = tokens.Select(t => new Token(t)).ToList();
        }
        #endregion

        public void ClearTokens()
        {
            _tokens.Clear();
        }

        public void PrependToken(Token token)
        {
            _tokens.Insert(0, token);

            // This little hack handles situations where we have an existing line number
            // If the supplied token is a line number, then the existing line number will be eliminated
            // If the supplied token is not a line number, then it will end up immediately after any line number
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

        public List<Token> RemoveTokens(List<char> codes) {
            var removedTokens = new List<Token>();

            for (var ix = _tokens.Count - 1; ix >= 0; ix--) {
                if (!codes.Contains(_tokens[ix].Code)) {
                    continue;
                }

                removedTokens.Add(_tokens[ix]);
                _tokens.RemoveAt(ix);
            }

            return removedTokens;
        }

        public List<Token> RemoveTokens(List<Token> tokens) {
            var removedTokens = new List<Token>();

            for (var ix = _tokens.Count - 1; ix >= 0; ix--) {
                if (!tokens.Contains(_tokens[ix])) {
                    continue;
                }

                removedTokens.Add(_tokens[ix]);
                _tokens.RemoveAt(ix);
            }

            return removedTokens;
        }

        public List<Token> RemoveToken(char code) {
            var removedTokens = new List<Token>();

            for (var ix = _tokens.Count - 1; ix >= 0; ix--) {
                if (code != _tokens[ix].Code) {
                    continue;
                }

                removedTokens.Add(_tokens[ix]);
                _tokens.RemoveAt(ix);
            }

            return removedTokens;
        }

        public List<Token> RemoveToken(Token token) {
            var removedTokens = new List<Token>();

            for (var ix = _tokens.Count - 1; ix >= 0; ix--) {
                if (token != _tokens[ix]) {
                    continue;
                }

                removedTokens.Add(_tokens[ix]);
                _tokens.RemoveAt(ix);
            }

            return removedTokens;
        }

        public List<Token> ReplaceToken(Token searchToken, Token replaceToken) {
            var removedTokens = new List<Token>();

            for (var ix = _tokens.Count - 1; ix >= 0; ix--) {
                if (searchToken != _tokens[ix]) {
                    continue;
                }

                removedTokens.Add(_tokens[ix]);
                _tokens.RemoveAt(ix);
                _tokens.Insert(ix, replaceToken);
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Blocker Code Smell", "S3875:\"operator==\" should not be overloaded on reference types", Justification = "<Pending>")]
        public static bool operator ==(Line a, Line b) {
            if (a is null || b is null) {
                return a is null && b is null;
            }

            if (a.Tokens.Count != b.Tokens.Count) {
                return false;
            }

            return !b.Tokens.Where((t, ix) => a.Tokens[ix] != t).Any();
        }

        public static bool operator !=(Line a, Line b) {
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
        
        /// <summary>
        /// Return the line as a formatted string, with any line number first and any comments last
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var allTokensOrdered = _tokens.Where(t => t.IsLineNumber).Take(1)
                .Concat(_tokens.Where(t => !(t.IsLineNumber || t.IsComment)))
                .Concat(_tokens.Where(t => t.IsComment));
            return string.Join(" ", allTokensOrdered).Trim();
        }

        /// <summary>
        /// Return the line as a formatted string, but without any line number or comment
        /// </summary>
        /// <returns></returns>
        public string ToSimpleString() {
            var allTokensOrdered = _tokens.Where(t => !(t.IsLineNumber || t.IsComment));
            return string.Join(" ", allTokensOrdered).Trim();
        }

        public string ToXYCoord() {
            var xyz = (Coord)this;
            return $"X{xyz.X}Y{xyz.Y}";
        }
    }
}
