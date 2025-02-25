using Spectre.Console;

namespace Copilot4.SyntaxHighlighter;

public sealed class PlainSyntaxHighlighter : ISyntaxHighlighter {
    public void Dispose () {
        ;
    }

    public void Write ( string chunk ) {
        AnsiConsole.Write ( new RawText ( chunk ) );
    }
}
