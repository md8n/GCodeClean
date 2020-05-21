// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

using GCodeClean.Processing;

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

            var stringTokens = _source.Tokenize();
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
            if (Tokens.Any(t => t.Code == 'N') && Tokens[0].Code != 'N')
            {
                // If there's a line number, make sure it is the first token on the line
                IsValid = false;
                return;
            }
        }
    }

    public bool IsFileTerminator { get; private set; }

    public bool IsEmptyOrWhiteSpace { get; private set; }

    public bool IsValid { get; private set; }

    public bool HasTokens(List<char> codes)
    {
        return this.Tokens.Any(t => codes.Contains(t.Code));
    }

    public bool HasTokens(List<string> tokens)
    {
        return this.Tokens.Any(t => tokens.Contains(t.Source));
    }

    /// <summary>
    /// This returns true if there are one or more Arguments but no Commands or Codes, comments and codes are ignored for this test
    /// </summary>
    public Boolean IsArgumentsOnly()
    {
        if (this.IsNotCommandCodeOrArguments())
        {
            return false;
        }

        return this.HasTokens(Token.Arguments.ToList()) && !this.HasTokens(Token.Commands.ToList()) && !this.HasTokens(Token.Codes.ToList());
    }

    /// <summary>
    /// This returns true if there are one or more Arguments but no Commands, comments are ignored for this test
    /// </summary>
    public Boolean HasMovementCommand()
    {
        if (this.IsArgumentsOnly())
        {
            return false;
        }

        return this.HasTokens(Token.MovementCommands.ToList());
    }

    public Line()
    {
        this.Source = "";
    }

    public Line(string source)
    {
        this.Source = source;
    }

    public Line(List<Token> tokens)
    {
        this.Source = string.Join(' ', tokens);
    }

    public List<Token> RemoveTokens(List<char> codes)
    {
        var removedTokens = new List<Token>();

        for (var ix = this.Tokens.Count - 1; ix >= 0; ix--)
        {
            if (codes.Contains(this.Tokens[ix].Code))
            {
                removedTokens.Add(this.Tokens[ix]);
                this.Tokens.RemoveAt(ix);
            }
        }

        return removedTokens;
    }

    public List<Token> RemoveTokens(List<Token> tokens)
    {
        var removedTokens = new List<Token>();

        for (var ix = this.Tokens.Count - 1; ix >= 0; ix--)
        {
            if (tokens.Contains(this.Tokens[ix]))
            {
                removedTokens.Add(this.Tokens[ix]);
                this.Tokens.RemoveAt(ix);
            }
        }

        return removedTokens;
    }

    /// <sumary>
    /// Compares two lines to ensure they are `compatible`
    /// </summary>
    public Boolean IsCompatible(Line lineB)
    {
        if (this.Tokens.Count != lineB.Tokens.Count)
        {
            return false;
        }

        var isCompatible = true;
        for (var ix = 0; ix < lineB.Tokens.Count; ix++)
        {
            if (this.Tokens[ix].Code != lineB.Tokens[ix].Code)
            {
                isCompatible = false;
                break;
            }
            if (this.Tokens[ix].Code == 'G' || this.Tokens[ix].Code == 'M')
            {
                // For 'Commands' the whole thing must be the same
                if (this.Tokens[ix] != lineB.Tokens[ix])
                {
                    isCompatible = false;
                    break;
                }
            }
        }

        return isCompatible;
    }

    public static implicit operator Coord(Line line)
    {
        var coords = new Coord();
        decimal? value = null;

        foreach (var token in line.Tokens)
        {
            value = token.Number;
            if (value.HasValue)
            {
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
        }

        return coords;
    }

    public static Boolean operator ==(Line a, Line b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a.Tokens.Count != b.Tokens.Count)
        {
            return false;
        }

        var isDuplicate = true;
        for (var ix = 0; ix < b.Tokens.Count; ix++)
        {
            if (a.Tokens[ix] != b.Tokens[ix])
            {
                isDuplicate = false;
                break;
            }
        }

        return isDuplicate;
    }

    public static Boolean operator !=(Line a, Line b)
    {
        return !(a == b);
    }

    public override bool Equals(Object obj)
    {
        // Compare run-time types.
        return (!this.GetType().Equals(obj.GetType()))
            ? false : this == (Line)obj;
    }

    public override int GetHashCode()
    {
        return (this.Tokens).GetHashCode();
    }
}