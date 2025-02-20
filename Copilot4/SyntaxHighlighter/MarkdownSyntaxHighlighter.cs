using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace Copilot4.SyntaxHighlighter;

public sealed class MarkdownSyntaxHighlighter : ISyntaxHighlighter {

    bool isBold;
    bool isItalic;
    bool isMarkdownBlock;
    bool isThink;

    int starIndex = 0;
    int markdownCodeIndex = 0;

    StringBuilder currentLine = new StringBuilder ();

    Regex HeadingRegex = new Regex ( @"^#+\s*.*$", RegexOptions.Multiline | RegexOptions.Compiled );

    void DumpChar ( char c ) {

        Thread.SpinWait ( 1 );

        // -------------------------------
        // apply newline terminators
        if (c == '\n' || c == '\r') {
            this.currentLine.Clear ();
        }
        else {
            this.currentLine.Append ( c );
        }

        string line = this.currentLine.ToString ();

        // handle thinking
        if (line.EndsWith ( "<think>" )) {
            this.isThink = true;
        }
        else if (line.EndsWith ( "</think>" )) {
            this.isThink = false;
        }

        // handle bold/italic
        else if (c == '*') {
            this.starIndex++;

            if (this.starIndex >= 2) {
                this.isItalic = false;
                this.isBold = !this.isBold;
            }
            else if (this.starIndex >= 1) {
                this.isItalic = !this.isItalic;
            }
            return;
        }

        // handle markdown block
        else if (c == '`') {
            this.markdownCodeIndex++;

            if (this.markdownCodeIndex >= 1) {
                this.isMarkdownBlock = !this.isMarkdownBlock;
            }
            return;
        }
        else {
            this.starIndex = 0;
            this.markdownCodeIndex = 0;
        }

        // -------------------------------
        // apply styles
        if (this.isMarkdownBlock) {
            AnsiConsole.Write ( new RawText ( c.ToString (), new Style ( foreground: Color.DarkSlateGray3 ) ) );
        }
        else if (this.isThink) {
            AnsiConsole.Write ( new RawText ( c.ToString (), new Style ( foreground: Color.Grey, decoration: Decoration.Italic ) ) );
        }
        else if (this.isBold) {
            AnsiConsole.Write ( new RawText ( c.ToString (), new Style ( foreground: Color.White, decoration: Decoration.Bold ) ) );
        }
        else if (this.isItalic) {
            AnsiConsole.Write ( new RawText ( c.ToString (), new Style ( foreground: Color.White, decoration: Decoration.Italic ) ) );
        }
        else if (this.HeadingRegex.IsMatch ( line )) {
            AnsiConsole.Write ( new RawText ( c.ToString (), new Style ( foreground: Color.White, decoration: Decoration.Bold | Decoration.Underline ) ) );
        }
        else {
            AnsiConsole.Write ( new RawText ( c.ToString () ) );
        }
    }

    public void Write ( string chunk ) {
        for (int i = 0; i < chunk.Length; i++) {
            char c = chunk [ i ];
            this.DumpChar ( c );
        }
    }
}
