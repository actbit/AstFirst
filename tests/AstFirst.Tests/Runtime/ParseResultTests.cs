using AstFirst;

namespace AstFirst.Tests.Runtime;

/// <summary>
/// ParseResult の HasErrors (構文エラー + 意味解析 Severity) と Diagnostics のテスト。
/// </summary>
public class ParseResultTests
{
    private static readonly SourceSpan Span = new(new Position(0, 0, 0), new Position(0, 0, 0));

    private static IReadOnlyList<ParseError> NoErrors => System.Array.Empty<ParseError>();
    private static IReadOnlyList<ParseError> OneSyntaxError => new[] { new ParseError("syntax", 0) };

    [Fact]
    public void HasErrors_Empty_False()
    {
        var r = new ParseResult(null, NoErrors);
        Assert.False(r.HasErrors);
    }

    [Fact]
    public void HasErrors_SyntaxErrorOnly_True()
    {
        var r = new ParseResult(null, OneSyntaxError);
        Assert.True(r.HasErrors);
    }

    [Fact]
    public void HasErrors_DiagnosticsError_True()
    {
        var diags = new[] { new Diagnostic("意味エラー", Span, Severity.Error) };
        var r = new ParseResult(null, NoErrors, diags);
        Assert.True(r.HasErrors);
    }

    [Fact]
    public void HasErrors_DiagnosticsWarningOnly_False()
    {
        // 意味解析が Warning だけなら、構文エラーが無ければ HasErrors は false
        var diags = new[] { new Diagnostic("注意", Span, Severity.Warning) };
        var r = new ParseResult(null, NoErrors, diags);
        Assert.False(r.HasErrors);
    }

    [Fact]
    public void Diagnostics_DefaultEmpty_WhenOmitted()
    {
        var r = new ParseResult(null, NoErrors);
        Assert.NotNull(r.Diagnostics);
        Assert.Empty(r.Diagnostics);
    }
}
