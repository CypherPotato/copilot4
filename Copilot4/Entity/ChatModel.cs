using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Copilot4.SyntaxHighlighter;

namespace Copilot4.Entity;

public sealed class ChatModel {
    public string? Name { get; set; }
    public required string EndpointUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? SystemMessage { get; set; }
    public ChatModelInterface ApiInterface { get; set; } = ChatModelInterface.OpenAi;
    public SyntaxHighlightingMode SyntaxHighlighting { get; set; } = SyntaxHighlightingMode.Markdown;
    public required ChatModelParameters Model { get; set; }

    public string? GetFormattedSystemMessage () {
        return SanitizePrompt ( SystemMessage );
    }

    public ISyntaxHighlighter GetSyntaxHighlighter () {
        return SyntaxHighlighting switch {
            SyntaxHighlightingMode.Markdown => new MarkdownSyntaxHighlighter (),
            _ => new PlainSyntaxHighlighter ()
        };
    }

    [return: NotNullIfNotNull ( nameof ( prompt ) )]
    public static string? SanitizePrompt ( string? prompt ) {
        if (prompt is null)
            return null;

        var matches = Regex.Split ( prompt, @"\\\s*$\s*", RegexOptions.Multiline );
        return string.Join ( "", matches ).Trim ();
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