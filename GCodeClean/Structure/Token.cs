// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Linq;

namespace GCodeClean.Structure
{
    public class Token
    {
        private string _source;
        private char _code;
        private int? _parameter;
        private decimal? _number;

        public string Source {
            get => _source;
            set {
                _source = value;

                IsValid = true;
                IsFileTerminator = false;
                IsBlockDelete = false;
                IsComment = false;
                IsCommand = false;
                IsCode = false;
                IsArgument = false;
                IsLineNumber = false;
                IsParameterSetting = false;
                IsOther = false;

                if (string.IsNullOrWhiteSpace(_source)) {
                    IsValid = false;
                    return;
                }

                var token = _source.Trim();
                Code = token[0];
                if (token.Length == 1) {
                    IsValid = IsFileTerminator || IsBlockDelete;
                    return;
                }

                if (Code == Letter.commentSemi || token.EndsWith(Letter.commentEnd)) {
                    IsValid = IsComment;
                    return;
                }

                if (IsParameterSetting) {
                    var parameterParts = token[1..].Split('=', StringSplitOptions.RemoveEmptyEntries);
                    if (parameterParts.Length != 2) {
                        IsValid = false;
                        return;
                    }

                    if (!int.TryParse(parameterParts[0], out var parameter)) {
                        IsValid = false;
                        return;
                    }

                    if (!decimal.TryParse(parameterParts[1], out _)) {
                        IsValid = false;
                        return;
                    }

                    Parameter = parameter;
                }

                if (!decimal.TryParse(token[1..], out var number)) {
                    IsValid = false;
                    return;
                }

                Number = number;
            }
        }

        public char Code {
            get => _code;
            set {
                _code = value;

                IsFileTerminator = false;
                IsBlockDelete = false;
                IsComment = false;
                IsCommand = false;
                IsCode = false;
                IsArgument = false;
                IsLineNumber = false;
                IsParameterSetting = false;
                IsOther = false;

                if (Letter.FileTerminators.Contains(_code)) {
                    IsFileTerminator = true;
                } else if (Letter.BlockDeletes.Contains(_code)) {
                    IsBlockDelete = true;
                } else if (Letter.Comments.Contains(_code)) {
                    IsComment = true;
                } else if (Array.Exists(Letter.Commands, c => c == _code)) {
                    IsCommand = true;
                } else if (Array.Exists(Letter.Codes, c => c == _code)) {
                    IsCode = true;
                } else if (Array.Exists(Letter.Arguments, a => a == _code)) {
                    IsArgument = true;
                } else if (Array.Exists(Letter.LineNumbers, l => l == _code)) {
                    IsLineNumber = true;
                } else if (Array.Exists(Letter.Parameters, p => p == _code)) {
                    // Parameter Setting is technically a command, however we handle it separately from commands
                    IsParameterSetting = true;
                } else if (Array.Exists(Letter.Other, a => a == _code)) {
                    IsOther = true;
                }
            }
        }

        public decimal? Number {
            get => _number;
            set {
                _number = value;

                if (IsFileTerminator || IsBlockDelete || IsComment) {
                    IsValid = false;
                    return;
                }

                if (IsCommand) {
                    if (Code == Letter.gCommand) {
                        IsValid = _number.HasValue && Letter.GCodes.Contains(_number.Value);
                        return;
                    }

                    if (Code == Letter.mCommand) {
                        IsValid = _number.HasValue && Letter.MCodes.Contains(_number.Value);
                        return;
                    }

                    IsValid = false;
                }

                if (IsArgument || IsLineNumber || IsParameterSetting) {
                    IsValid = true;
                }
            }
        }

        public int? Parameter {
            get => _parameter;
            set {
                _parameter = value;

                if (IsFileTerminator || IsBlockDelete || IsComment) {
                    IsValid = false;
                    return;
                }

                if (IsCommand || IsArgument || IsLineNumber || IsParameterSetting) {
                    IsValid = _parameter >= 1 && _parameter <= 5399;
                }
            }
        }

        public bool IsFileTerminator { get; private set; }

        public bool IsBlockDelete { get; private set; }

        public bool IsCommand { get; private set; }

        public bool IsCode { get; private set; }

        public bool IsComment { get; private set; }

        public bool IsArgument { get; private set; }

        public bool IsLineNumber { get; private set; }

        public bool IsParameterSetting { get; private set; }

        public bool IsOther { get; private set; }

        public bool IsValid { get; private set; }

        /// <summary>
        /// Creates a token from the supplied string
        /// </summary>
        /// <param name="token"></param>
        public Token(string token) {
            Source = token;
        }

        /// <summary>
        /// Creates a new token from the supplied token (deep copy)
        /// </summary>
        /// <param name="token"></param>
        public Token(Token token) {
            _source = token.Source;
            _code = token.Code;
            _parameter = token.Parameter;
            _number = token.Number;

            IsValid = token.IsValid;
            IsFileTerminator = token.IsFileTerminator;
            IsBlockDelete = token.IsBlockDelete;
            IsComment = token.IsComment;
            IsCommand = token.IsCommand;
            IsCode = token.IsCode;
            IsArgument = token.IsArgument;
            IsLineNumber = token.IsLineNumber;
            IsParameterSetting = token.IsParameterSetting;
            IsOther = token.IsOther;
        }

        /// <summary>
        /// Convert almost any token to a comment token
        /// </summary>
        /// <remarks>Does not convert file terminators or tokens that are already comments</remarks>
        /// <returns></returns>
        public Token ToComment() {
            if (IsFileTerminator || IsComment) {
                return this;
            }

            Source = $"({ToString()})";

            return this;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Blocker Code Smell", "S3875:\"operator==\" should not be overloaded on reference types", Justification = "<Pending>")]
        public static bool operator ==(Token a, Token b) {
            if (a is null || b is null) {
                return a is null && b is null;
            }

            if (a.Code != b.Code) {
                return false;
            }

            if (a.IsComment) {
                return a.Source == b.Source;
            }

            if (a.IsFileTerminator || a.IsBlockDelete) {
                return true;
            }

            return a.Number == b.Number;
        }

        public static bool operator !=(Token a, Token b) {
            return !(a == b);
        }

        public override bool Equals(object obj) {
            // Compare run-time types.
            return GetType() == obj?.GetType() && this == (Token) obj;
        }

        public override int GetHashCode() {
            if (IsComment || IsFileTerminator || IsBlockDelete) {
                return Source.GetHashCode();
            }

            return (Code, Number).GetHashCode();
        }

        public override string ToString() {
            if (IsFileTerminator || IsBlockDelete || IsComment || !IsValid) {
                return Source;
            }

            if (IsParameterSetting) {
                return $"{Code}{Parameter}={Number:0.####}";
            }

            return Number.HasValue ? $"{Code}{Number:0.####}" : $"{Code}#{Parameter}";
        }
    }
}
