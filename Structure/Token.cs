// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Linq;

public class Token {
    private string _source;

    private char _code;

    private decimal _number;

    public string Source {
        get => _source;
        set {
            _source = value;

            IsValid = true;
            IsFileTerminator = false;
            IsComment = false;
            IsCommand = false;
            IsArgument = false;

            if (string.IsNullOrWhiteSpace(_source)) {
                this.IsValid = false;
                return;
            }

            var token = _source.Trim();
            Code = token[0];
            if (token.Length == 1) {
                IsValid = IsFileTerminator;
                return;
            }
            if (token.EndsWith(')')) {
                IsValid = IsComment;
                return;
            }

            decimal number;
            if (!decimal.TryParse(token.Substring(1), out number)) {
                IsValid = false;
                return;
            }
            Number = number;
        }
    }

    public char Code
    {
        get => _code;
        set {
            _code = value;

            IsFileTerminator = false;
            IsComment = false;
            IsCommand = false;
            IsArgument = false;

            if (_code == FileTerminators[0])
            {
                IsFileTerminator = true;
            }
            else if (_code == Comments[0])
            {
                IsComment = true;
            }
            else if (Commands.Any(c => c == _code))
            {
                IsCommand = true;
            } 
            else if (Arguments.Any(a => a == _code))
            {
                IsArgument = true;
            }
        }
    }

    public decimal Number { 
        get => _number;
        set {
            _number = value;

            if (IsFileTerminator || IsComment) {
                IsValid = false;
                return;
            }
            if (IsCommand)
            {
                if (Code == 'G') {
                    IsValid = GCodes.Contains(_number);
                    return;
                }
                if (Code == 'M') {
                    IsValid = MCodes.Contains(_number);
                    return;
                }
                IsValid = false;
            }
            if (IsArgument)
            {
                IsValid = true;
            }
        }
    }

    public bool IsFileTerminator { get; private set; }

    public bool IsCommand { get; private set; }

    public bool IsComment { get; private set; }

    public bool IsArgument { get; private set; }

    public bool IsValid { get; private set; }

    public static char[] FileTerminators = { '%' };

    public static char[] Comments = { '(' };

    public static char[] Commands = { 'C', 'M' };

    public static char[] Arguments = { 'A', 'B', 'D', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'N', 'P', 'R', 'S', 'T', 'X', 'Y', 'Z' };

    public static decimal[] GCodes = {
        0, 1, 2, 3, 4, 10, 17, 18, 19, 20, 21, 28, 30, 38.2M, 
        40, 41, 42, 43, 49, 53, 54, 55, 56, 57, 58, 59, 59.1M, 59.2M, 59.3M, 
        61, 61.1M, 64, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 
        90, 91, 92, 92.1M, 92.2M, 92.3M, 93, 94, 98, 99
    };

    public static decimal[] MCodes = {
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 30, 48, 49, 60
    };

    public Token (string token) {
        this.Source = token;
    }
}