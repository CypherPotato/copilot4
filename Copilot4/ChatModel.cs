using Copilot4.SyntaxHighlighter;

namespace Copilot4;

public sealed class AppConfiguration {
    public ChatModel [] Models { get; set; } = Array.Empty<ChatModel> ();
    public bool EnableChatDecorations { get; set; } = true;
}

public sealed class ChatModel {
    public string? Name { get; set; }
    public required string EndpointUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? SystemMessage { get; set; }
    public ChatModelInterface ApiInterface { get; set; } = ChatModelInterface.OpenAi;
    public SyntaxHighlightingMode SyntaxHighlighting { get; set; } = SyntaxHighlightingMode.Markdown;
    public required ChatModelParameters Model { get; set; }

    public ISyntaxHighlighter GetSyntaxHighlighter () {
        return this.SyntaxHighlighting switch {
            SyntaxHighlightingMode.Markdown => new MarkdownSyntaxHighlighter (),
            _ => new PlainSyntaxHighlighter ()
        };
    }
}

public sealed class ChatModelParameters {
    public required string Name { get; set; }
    public int MaxTokens { get; set; } = 1024;
    public float TopP { get; set; } = 0.5f;
    public float Temperature { get; set; } = 1f;
}

public enum ChatModelInterface {
    OpenAi = 0
}

public enum SyntaxHighlightingMode {
    Markdown = 0,
    None = 99
}