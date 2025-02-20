using Spectre.Console;
using Spectre.Console.Rendering;

namespace Copilot4;

public sealed class RawText : Renderable {

    public Style Style { get; }
    public string Text { get; }

    public RawText ( string text ) {
        this.Text = text;
        this.Style = new Style ();
    }

    public RawText ( string text, Style style ) {
        this.Text = text;
        this.Style = style;
    }

    protected override IEnumerable<Segment> Render ( RenderOptions options, int maxWidth ) {
        yield return new Segment ( this.Text, this.Style );
    }
}
