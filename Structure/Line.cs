// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.Linq;

using GCodeClean.Processing;

public class Line {
    private string _source;

    public List<Token> Tokens { get; set; }

    public string Source {
        get => _source;
        set {
            _source = value;

            IsValid = true;
            IsFileTerminator = false;

            if (string.IsNullOrWhiteSpace(_source)) {
                IsEmptyOrWhiteSpace = true;
                return;
            }

            var stringTokens = _source.Tokenize();
            foreach (var stringToken in stringTokens)
            {
                Tokens.Add(new Token(stringToken));
            }

            if (Tokens.Any(t => t.IsFileTerminator)) {
                IsFileTerminator = true;
                IsValid = Tokens.Count > 1;
                return;
            }
            if (Tokens.Any(t => t.Code == 'N') && Tokens[0].Code != 'N') {
                IsValid = false;
                return;
            }
        }
    }

    public bool IsFileTerminator { get; private set; }

    public bool IsEmptyOrWhiteSpace { get; private set; }

    public bool IsValid { get; private set; }

    public Line() { }

    public Line(string line) {
        Source = line;
    }
}