// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Linq;

public class Token {
    private string _source;

    private char _code;

    private decimal? _number;

    public string Source {
        get => _source;
        set {
            _source = value;

            IsValid = true;
            IsFileTerminator = false;
            IsComment = false;
            IsCommand = false;
            IsCode = false;
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
            IsCode = false;
            IsArgument = false;

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
        }
    }

    public decimal? Number { 
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
                    IsValid = _number.HasValue && GCodes.Contains(_number.Value);
                    return;
                }
                if (Code == 'M') {
                    IsValid = _number.HasValue && MCodes.Contains(_number.Value);
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

    public bool IsCode { get; private set; }

    public bool IsComment { get; private set; }

    public bool IsArgument { get; private set; }

    public bool IsValid { get; private set; }

    public static char[] FileTerminators = { '%' };

    public static char[] Comments = { '(' };

    public static char[] Commands = { 'G', 'M' };

    public static char[] Codes = { 'F', 'S', 'T' };    

    public static char[] Arguments = { 'A', 'B', 'C', 'D', 'H', 'I', 'J', 'K', 'L', 'N', 'P', 'R', 'X', 'Y', 'Z' };

    public static string[] MovementCommands = { "G0", "G1", "G2", "G3", "G00", "G01", "G02", "G03" };

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

    public static Boolean operator == (Token a, Token b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a.Code != b.Code)
        {
            return false;
        }

        if (a.IsComment) {
            return a.Source == b.Source;
        }
        if (a.IsFileTerminator) {
            return true;
        }

        return a.Number == b.Number;
    }

    public static Boolean operator != (Token a, Token b)
    {
        return !(a == b);
    }
    
    public override bool Equals(Object obj)
    {
        // Compare run-time types.
        return (!this.GetType().Equals(obj.GetType()))
            ? false : this == (Token)obj;
    }

    public override int GetHashCode()
    {
        if (this.IsComment || this.IsFileTerminator)
        {
            return (this.Source).GetHashCode();
        }

        return (this.Code, this.Number).GetHashCode();
    }

    public override string ToString()
    {
        if (this.IsFileTerminator || this.IsComment || !this.IsValid) {
            return this.Source;
        }

        return $"{this.Code}{this.Number:0.####}";
    }
}