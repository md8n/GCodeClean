// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

using GCodeClean.Processing;

namespace GCodeClean.Structure
{
    public sealed class Line : IEquatable<Line> {
        private string _source;

        private List<Token> _tokens;

        /// <summary>
        /// Get/Set the current list of all Tokens, get includes any line number token.
        /// Will reset the statuses for this line
        /// </summary>
        public List<Token> AllTokens {
            get {
                return _tokens;
            }
            set {
                SetTokens(value);
            }
        }

        /// <summary>
        /// Gets the current list of Tokens, does not include any line number token.
        /// </summary>
        public List<Token> Tokens {
            get {
#pragma warning disable S2365 // Properties should not make collection or array copies
                return _tokens.Where(t => !t.IsLineNumber).ToList();
#pragma warning restore S2365 // Properties should not make collection or array copies
            }
        }

        /// <summary>
        /// Gets all comment Tokens within the line.
        /// </summary>
        public List<Token> AllCommentTokens {
            get {
#pragma warning disable S2365 // Properties should not make collection or array copies
                return _tokens.Where(t => t.IsComment).ToList();
#pragma warning restore S2365 // Properties should not make collection or array copies
            }
        }

        /// <summary>
        /// Set the private member _tokens to the supplied value, ensuring that the order of tokens is correct
        /// Then set the status values
        /// </summary>
        /// <param name="tokens"></param>
        private void SetTokens(List<Token> tokens) {
            _tokens = tokens;
            SetTokens();
        }

        /// <summary>
        /// Set the private member _tokens, ensuring that the order of tokens is correct
        /// Then set the status values
        /// </summary>
        private void SetTokens() {
            var blockDeleteToken = _tokens.Where(t => t.IsBlockDelete).Take(1);
            var lineNumberToken = _tokens.Where(t => t.IsLineNumber).Take(1);
            var allCommentTokens = _tokens.Where(t => t.IsComment);
            var allOtherTokens = _tokens.Where(t => !(t.IsLineNumber || t.IsBlockDelete || t.IsComment));

            _tokens = [.. blockDeleteToken, .. lineNumberToken, .. allOtherTokens, .. allCommentTokens];

            // Reset the _source to match
            _source = string.Join(' ', _tokens.Select(t => t.Source));

            SetStatuses();
        }

        /// <summary>
        /// Sets all of the status values from the current list of tokens
        /// </summary>
        private void SetStatuses() {
            IsFileTerminator = false;
            HasBlockDelete = false;
            HasLineNumber = false;

            IsEmptyOrWhiteSpace = _tokens.TrueForAll(t => t.Source.Trim().Length == 0);
            if (IsEmptyOrWhiteSpace) {
                IsValid = true;
                return;
            }

            IsValid = _tokens.TrueForAll(t => t.IsValid);

            if (_tokens.Exists(t => t.IsFileTerminator)) {
                // Check the file terminator character is the only thing on the line
                IsFileTerminator = true;
                IsValid = Tokens.Count == 1;
                return;
            }

            HasBlockDelete = _tokens.Exists(t => t.IsBlockDelete);
            if (HasBlockDelete && !_tokens[0].IsBlockDelete) {
                // Assuming SetTokens has been called this will never be invoked, however ...
                IsValid = false;
            }

            HasLineNumber = _tokens.Exists(t => t.IsLineNumber);
        }

        public string Source {
            get => _source;
            set {
                _source = value;
                _tokens ??= [];

                SetTokens(_source.Tokenise().Select(s => new Token(s)).ToList());
            }
        }

        public bool IsFileTerminator { get; private set; }

        public bool IsEmptyOrWhiteSpace { get; private set; }

        public bool IsValid { get; private set; }

        public bool HasBlockDelete { get; private set; }

        public bool HasLineNumber { get; private set; }

        public bool HasTokens(char[] codes) => _tokens.Exists(t => codes.Contains(t.Code));

        public bool HasTokens(IEnumerable<string> tokens) {
            var parsedTokens = tokens.Select(t => new Token(t));
            return HasTokens(parsedTokens);
        }

        public bool HasTokens(IEnumerable<Token> tokens) => _tokens.Exists(tokens.Contains);

        public bool HasToken(char code) => _tokens.Exists(t => t.Code == code);

        public bool HasToken(string token) {
            var parsedToken = new Token(token);
            return HasToken(parsedToken);
        }

        public bool HasToken(Token token) => _tokens.Exists(t => t == token);

        /// <summary>
        /// Roughly equivalent to `IsNullOrWhiteSpace` this returns true if there are:
        /// * no tokens,
        /// * only a file terminator
        /// * only one or more comments
        /// </summary>
        public bool IsNotCommandCodeOrArguments() {
            return _tokens.Count == 0 || _tokens.TrueForAll(t => t.IsFileTerminator) || _tokens.TrueForAll(t => t.IsComment);
        }

        /// <summary>
        /// This returns true if there are one or more Arguments but no Commands or Codes.
        /// Line numbers, comments, codes are ignored for this test
        /// </summary>
        public bool IsArgumentsOnly() {
            if (IsNotCommandCodeOrArguments()) {
                return false;
            }

            return HasTokens(Letter.Arguments) && !HasTokens(Letter.Commands) && !HasTokens(Letter.Codes);
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
            AllTokens = line.AllTokens.Select(t => new Token(t)).ToList();
        }

        /// <summary>
        /// Create a new line of GCode from a single GCode token (deep copy)
        /// </summary>
        /// <param name="token"></param>
        public Line(Token token)
        {
            _source = token.ToString();
            _tokens = [new Token(token)];
            SetStatuses();
        }

        /// <summary>
        /// Create a new line of GCode from a list of GCode tokens (deep copy)
        /// </summary>
        /// <param name="tokens"></param>
        public Line(IEnumerable<Token> tokens)
        {
            _source = string.Join(' ', tokens);
            SetTokens(tokens.Select(t => new Token(t)).ToList());
        }
        #endregion

        public void ClearTokens()
        {
            _tokens.Clear();
        }

        public void PrependToken(Token token)
        {
            _tokens.Insert(0, token);
            SetTokens();
        }

        public void AppendToken(Token token)
        {
            _tokens.Add(token);
            SetTokens();
        }

        public void AppendTokens(IEnumerable<Token> tokens)
        {
            _tokens.AddRange(tokens);
            SetTokens();
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
            SetTokens();

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
            SetTokens();

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
            SetTokens();

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
            SetTokens();

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
            SetTokens();

            return removedTokens;
        }

        /// <summary>
        /// Compares two lines to ensure they are `compatible`.
        /// Ignores any line number tokens
        /// </summary>
        public bool IsCompatible(Line lineB) {
            var aTokens = Tokens;
            var bTokens = lineB.Tokens;
            if (aTokens.Count != bTokens.Count) {
                return false;
            }

            var isCompatible = true;
            for (var ix = 0; ix < bTokens.Count; ix++) {
                if (aTokens[ix].Code != bTokens[ix].Code) {
                    isCompatible = false;
                    break;
                }

                if (!Letter.Commands.Contains(aTokens[ix].Code)) {
                    continue;
                }

                // For 'Commands' the whole thing must be the same
                if (aTokens[ix] == bTokens[ix]) {
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

        public override bool Equals(object obj) => this.Equals(obj as Line);

        /// <summary>
        /// Compare this line and another for equality.
        /// Note that line numbers are not included in the comparison
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public bool Equals(Line line) {
            if (line is null) {
                return false;
            }

            if (Object.ReferenceEquals(this, line)) {
                return true;
            }

            if (Tokens.Count != line.Tokens.Count) {
                return false;
            }

            return !line.Tokens.Where((t, ix) => Tokens[ix] != t).Any();
        }

        public override int GetHashCode() => Tokens.Select(t => t.GetHashCode()).GetHashCode();

        /// <summary>
        /// Compare two lines for equality.
        /// Note that line numbers are not included in the comparison
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator ==(Line a, Line b) {
            if (a is null) {
                return b is null;
            }

            return a.Equals(b);
        }

        public static bool operator !=(Line a, Line b) => !(a == b);
        
        /// <summary>
        /// Return the line as a formatted string, with any block delete and line number first and any comments last
        /// </summary>
        /// <returns></returns>
        public override string ToString() => string.Join(" ", _tokens).Trim();

        /// <summary>
        /// Return the line as a formatted string, but without any line number or comment
        /// </summary>
        /// <returns></returns>
        public string ToSimpleString() => string.Join(" ", _tokens.Where(t => !(t.IsLineNumber || t.IsComment))).Trim();

        public string ToXYCoord() => ((Coord)this).ToXYCoord();
    }
}
