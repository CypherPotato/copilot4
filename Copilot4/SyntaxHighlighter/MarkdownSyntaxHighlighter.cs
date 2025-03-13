using System.Text;
using CommonMarkSharp;
using CommonMarkSharp.Blocks;
using CommonMarkSharp.Inlines;
using Spectre.Console;
using Paragraph = CommonMarkSharp.Blocks.Paragraph;

namespace Copilot4.SyntaxHighlighter;

public sealed class MarkdownSyntaxHighlighter : ISyntaxHighlighter {

    CommonMark cm = new CommonMark ();
    StringBuilder sb = new StringBuilder ();
    int blockIndex = 0;
    int offset = 1;

    bool isThinking = false;

    public void Dispose () {
        offset = 0;
        WriteCurrentBuffer ();
    }

    public void Write ( string chunk ) {
        sb.Append ( chunk );
        WriteCurrentBuffer ();
    }

    void WriteCurrentBuffer () {
        var data = cm.Parse ( sb.ToString () );
        var child = data.Children.Skip ( blockIndex ).ToArray ();

        for (int i = 0; i < child.Length - offset; i++) {
            var block = child [ i ];
            blockIndex++;

            WriteBlock ( block );
        }
    }

    void WriteBlock ( Block block ) {
        if (block is Paragraph para) {
            WriteInlines ( para.Inlines );
            AnsiConsole.WriteLine ( "\n" );
        }
        else if (block is ATXHeader header) {
            AnsiConsole.Write ( new RawText ( $"{header.Contents.TrimEnd ()}\n\n", new Style ( foreground: Color.White, decoration: Decoration.Bold | Decoration.Underline ) ) );
        }
        else if (block is FencedCode fcode) {
            AnsiConsole.MarkupLineInterpolated ( $"[gray]--- {fcode.Info}[/]" );
            AnsiConsole.Write ( new CodeHighlighter ( fcode.Contents.Trim (), fcode.Info ) );
            AnsiConsole.MarkupLine ( $"[gray]---[/]\n" );
        }
        else if (block is IndentedCode icode) {
            AnsiConsole.MarkupLine ( $"[gray]---[/]" );
            AnsiConsole.Write ( new RawText ( $"{icode.Contents.Trim ()}\n", new Style ( foreground: Color.CadetBlue_1 ) ) );
            AnsiConsole.MarkupLine ( $"[gray]---[/]\n" );
        }
        else if (block is HorizontalRule hrule) {
            AnsiConsole.Write ( new RawText ( $"{hrule.Contents}", new Style ( foreground: Color.Grey ) ) );
        }
        else if (block is List listblock) {
            foreach (var listItem in listblock.Children) {
                AnsiConsole.Write ( new RawText ( "- " ) );
                WriteBlock ( listItem );
            }
        }
        else if (block is ListItem listitem) {
            foreach (var listItemData in listitem.Children) {
                WriteBlock ( listItemData );
            }
        }
        else if (block is BlockQuote quote) {
            foreach (var quoteItem in quote.Children) {
                AnsiConsole.Write ( new RawText ( "> ", new Style ( foreground: Color.Grey ) ) );
                WriteBlock ( quoteItem );
            }
        }
        else if (block is HtmlBlock htmBlock) {
            AnsiConsole.Write ( new RawText ( htmBlock.Contents ) );
        }
        else {
            ;
        }
    }

    void WriteInlines ( IEnumerable<Inline> inlines ) {
        foreach (var inline in inlines) {
            string text = GetInlineContent ( inline );

            if (inline is InlineString istr) {
                if (text.StartsWith ( "<think>" )) {
                    isThinking = true;
                }

                if (isThinking) {
                    AnsiConsole.Write ( new RawText ( text, new Style ( foreground: Color.Grey, decoration: Decoration.Italic ) ) );
                }
                else {
                    AnsiConsole.Write ( new RawText ( text ) );
                }

                if (text.EndsWith ( "</think>" )) {
                    isThinking = false;
                }
            }
            else if (inline is StrongEmphasis iespm) {
                AnsiConsole.Write ( new RawText ( text, new Style ( foreground: Color.White, decoration: Decoration.Bold ) ) );
            }
            else if (inline is Emphasis iemph) {
                AnsiConsole.Write ( new RawText ( text, new Style ( foreground: Color.White, decoration: Decoration.Italic ) ) );
            }
            else if (inline is InlineCode icode) {
                AnsiConsole.Write ( new RawText ( text, new Style ( foreground: Color.CadetBlue_1 ) ) );
            }
            else if (inline is Link ilink) {
                var src = GetInlineContent ( ilink.Destination ).EscapeMarkup ();
                var linkText = ilink.Label.Literal.EscapeMarkup ();

                AnsiConsole.Markup ( $"[underline deepskyblue3][link={src}]{linkText}[/][/]" );
            }
            else if (inline is SoftBreak) {
                AnsiConsole.WriteLine ();
            }
            else if (inline is HardBreak) {
                AnsiConsole.WriteLine ( "\n" );
            }
            else if (inline is Image img) {
                // not supported yet
                WriteInlines ( [ img.Link ] );
            }
            else {
                ;
            }
        }
    }

    static string GetInlineContent ( Inline i ) {
        if (i is InlineString istr) {
            return istr.Value;
        }
        else if (i is InlineCode incode) {
            return incode.Code;
        }
        else if (i is InlineList inlist) {
            return string.Concat ( inlist.Inlines.Select ( GetInlineContent ) );
        }
        else {
            return string.Empty;
        }
    }
}
