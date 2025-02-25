namespace Copilot4.SyntaxHighlighter;

public interface ISyntaxHighlighter : IDisposable {
    public void Write ( string chunk );
}
