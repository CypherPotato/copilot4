using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Jint;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Copilot4;

public sealed class CodeHighlighter : Renderable {
    static Engine JsEngine;

    static CodeHighlighter () {
        JsEngine = new Engine ().Execute ( Prism.PrismJsBundle );
    }

    public string Code { get; }
    public string? Language { get; }

    public CodeHighlighter ( string code, string? language ) {
        Code = code;
        Language = language;
    }

    protected override IEnumerable<Segment> Render ( RenderOptions options, int maxWidth ) {


        bool ContainsAny ( string [] items, params string [] search ) {
            foreach (var searchTerm in search) {
                if (items.Contains ( searchTerm ))
                    return true;
            }
            return false;
        }


        if (string.IsNullOrEmpty ( Language )) {
            goto il_return_raw;
        }
        else {
            var evaluatedSyntax = JsEngine
                .SetValue ( "codeContents", Code )
                .SetValue ( "codeLanguage", Language )
                .Evaluate ( """
                if (Prism.languages[codeLanguage]) {
                    return Prism.highlight(codeContents, Prism.languages[codeLanguage], codeLanguage);
                } else {
                    return codeContents; // no grammar
                }
                """ );

            var html = new HtmlParser ();
            var document = html.ParseDocument ( evaluatedSyntax.AsString () );

            if (document.Body is null)
                goto il_return_raw;

            foreach (var children in document.Body.ChildNodes) {
                if (children is IHtmlSpanElement span) {
                    var classes = span.ClassList.ToArray ();

                    if (ContainsAny ( classes, "doctype", "doctype-tag" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "569CD6" ) ) );
                    }
                    else if (ContainsAny ( classes, "name" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "9cdcfe" ) ) );
                    }
                    else if (ContainsAny ( classes, "comment", "prolog" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "6a9955" ) ) );
                    }
                    else if (ContainsAny ( classes, "comment", "prolog" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "6a9955" ) ) );
                    }
                    else if (ContainsAny ( classes, "property", "number", "constant", "symbol", "inserted", "unit" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "b5cea8" ) ) );
                    }
                    else if (ContainsAny ( classes, "selector", "attr-value", "string", "interpolation-string", "char", "builtin", "deleted" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "ce9178" ) ) );
                    }
                    else if (ContainsAny ( classes, "operator", "entity" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "d4d4d4" ) ) );
                    }
                    else if (ContainsAny ( classes, "atrule" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "ce9178" ) ) );
                    }
                    else if (ContainsAny ( classes, "keyword", "boolean" )) {
                        if (ContainsAny ( classes, "module", "control-flow" )) {
                            yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "c586c0" ) ) );
                        }
                        else {
                            yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "569CD6" ) ) );
                        }
                    }
                    else if (ContainsAny ( classes, "function" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "dcdcaa" ) ) );
                    }
                    else if (ContainsAny ( classes, "regex" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "d16969" ) ) );
                    }
                    else if (ContainsAny ( classes, "important" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "569cd6" ) ) );
                    }
                    else if (ContainsAny ( classes, "class-name", "maybe-class-name" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "4ec9b0" ) ) );
                    }
                    else if (ContainsAny ( classes, "parameter", "attr-name", "interpolation", "console", "variable" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "9cdcfe" ) ) );
                    }
                    else if (ContainsAny ( classes, "punctuation", "tag" )) {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "808080" ) ) );
                    }
                    else {
                        yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "d4d4d4" ) ) );
                    }
                }
                else {
                    yield return new Segment ( children.TextContent, new Style ( foreground: Color.FromHex ( "d4d4d4" ) ) );
                }
            }
        }

        yield return new Segment ( "\n" );
        yield break;

il_return_raw:
        yield return new Segment ( Code, new Style ( foreground: Color.FromHex ( "d4d4d4" ) ) );
        yield return new Segment ( "\n" );
    }
}
