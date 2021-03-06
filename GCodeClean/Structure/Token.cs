// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
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

        public string Source
        {
            get => _source;
            set
            {
                _source = value;

                IsValid = true;
                IsFileTerminator = false;
                IsComment = false;
                IsCommand = false;
                IsCode = false;
                IsArgument = false;
                IsLineNumber = false;
                IsParameterSetting = false;
                IsOther = false;

                if (string.IsNullOrWhiteSpace(_source))
                {
                    IsValid = false;
                    return;
                }

                var token = _source.Trim();
                Code = token[0];
                if (token.Length == 1)
                {
                    IsValid = IsFileTerminator;
                    return;
                }

                if (Code == ';' || token.EndsWith(')'))
                {
                    IsValid = IsComment;
                    return;
                }

                decimal number;
                if (IsParameterSetting)
                {
                    var parameterParts = token[1..].Split('=', StringSplitOptions.RemoveEmptyEntries);
                    if (parameterParts.Length != 2)
                    {
                        IsValid = false;
                        return;
                    }

                    if (!int.TryParse(parameterParts[0], out var parameter))
                    {
                        IsValid = false;
                        return;
                    }

                    if (!decimal.TryParse(parameterParts[1], out number))
                    {
                        IsValid = false;
                        return;
                    }

                    Parameter = parameter;
                }

                if (!decimal.TryParse(token[1..], out number))
                {
                    IsValid = false;
                    return;
                }

                Number = number;
            }
        }

        public char Code
        {
            get => _code;
            set
            {
                _code = value;

                IsFileTerminator = false;
                IsComment = false;
                IsCommand = false;
                IsCode = false;
                IsArgument = false;
                IsLineNumber = false;
                IsParameterSetting = false;
                IsOther = false;

                if (FileTerminators.Contains(_code))
                {
                    IsFileTerminator = true;
                }
                else if (Comments.Contains(_code))
                {
                    IsComment = true;
                }
                else if (Commands.Any(c => c == _code))
                {
                    IsCommand = true;
                }
                else if (Codes.Any(c => c == _code))
                {
                    IsCode = true;
                }
                else if (Arguments.Any(a => a == _code))
                {
                    IsArgument = true;
                }
                else if (LineNumbers.Any(l => l == _code))
                {
                    IsLineNumber = true;
                }
                else if (Parameters.Any(p => p == _code))
                {
                    // Parameter Setting is technically a command, however we handle it separately from commands
                    IsParameterSetting = true;
                }
                else if (Other.Any(a => a == _code))
                {
                    IsOther = true;
                }
            }
        }

        public decimal? Number
        {
            get => _number;
            set
            {
                _number = value;

                if (IsFileTerminator || IsComment)
                {
                    IsValid = false;
                    return;
                }

                if (IsCommand)
                {
                    if (Code == 'G')
                    {
                        IsValid = _number.HasValue && GCodes.Contains(_number.Value);
                        return;
                    }

                    if (Code == 'M')
                    {
                        IsValid = _number.HasValue && MCodes.Contains(_number.Value);
                        return;
                    }

                    IsValid = false;
                }

                if (IsArgument || IsLineNumber || IsParameterSetting)
                {
                    IsValid = true;
                }
            }
        }

        public int? Parameter
        {
            get => _parameter;
            set
            {
                _parameter = value;

                if (IsFileTerminator || IsComment)
                {
                    IsValid = false;
                    return;
                }

                if (IsCommand || IsArgument || IsLineNumber || IsParameterSetting)
                {
                    IsValid = _parameter >= 1 && _parameter <= 5399;
                }
            }
        }

        public bool IsFileTerminator { get; private set; }

        public bool IsCommand { get; private set; }

        public bool IsCode { get; private set; }

        public bool IsComment { get; private set; }

        public bool IsArgument { get; private set; }

        public bool IsLineNumber { get; private set; }

        public bool IsParameterSetting { get; private set; }

        public bool IsOther { get; private set; }

        public bool IsValid { get; private set; }

        public static readonly char[] FileTerminators = {'%'};

        public static readonly char[] Comments = {'(', ';'};

        public static readonly char[] Commands = {'G', 'M'};

        public static readonly char[] Codes = {'F', 'S', 'T'};

        public static readonly char[] Arguments = {'A', 'B', 'C', 'D', 'H', 'I', 'J', 'K', 'L', 'P', 'R', 'X', 'Y', 'Z'};

        public static readonly char[] LineNumbers = { 'N' };

        /// <summary>
        /// Parameters are identified by a Hash followed by an integer (from 1 to 5399)
        /// Parameters may be set (a command) or may be used as a value (after a command, code or argument)
        /// </summary>
        public static readonly char[] Parameters = { '#' };

        public static readonly char[] Other = {'E', 'O', 'Q', 'U', 'V'};

        public static readonly decimal[] GCodes =
        {
            0, 1, 2, 3, 4, 10, 17, 18, 19, 20, 21, 28, 30, 38.2M,
            40, 41, 42, 43, 49, 53, 54, 55, 56, 57, 58, 59, 59.1M, 59.2M, 59.3M,
            61, 61.1M, 64, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89,
            90, 91, 92, 92.1M, 92.2M, 92.3M, 93, 94, 98, 99
        };

        public static readonly decimal[] MCodes =
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 30, 48, 49, 60
        };

        public Token(string token)
        {
            Source = token;
        }

        public static bool operator ==(Token a, Token b)
        {
            if (a is null || b is null)
            {
                return a is null && b is null;
            }

            if (a.Code != b.Code)
            {
                return false;
            }

            if (a.IsComment)
            {
                return a.Source == b.Source;
            }

            if (a.IsFileTerminator)
            {
                return true;
            }

            return a.Number == b.Number;
        }

        public static bool operator !=(Token a, Token b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            // Compare run-time types.
            return GetType() == obj?.GetType() && this == (Token) obj;
        }

        public override int GetHashCode()
        {
            if (IsComment || IsFileTerminator)
            {
                return Source.GetHashCode();
            }

            return (Code, Number).GetHashCode();
        }

        public override string ToString()
        {
            if (IsFileTerminator || IsComment || !IsValid)
            {
                return Source;
            }

            if (IsParameterSetting)
            {
                return $"{Code}{Parameter}={Number:0.####}";
            }

            return Number.HasValue ? $"{Code}{Number:0.####}" : $"{Code}#{Parameter}";
        }
    }
}
