using System.Collections.Generic;

namespace AstFirst;

/// <summary>構文エラー（位置付きメッセージ）。</summary>
public sealed class ParseError(string message, int position)
{
    public string Message { get; } = message;
    public int Position { get; } = position;
    public override string ToString() => Position + ": " + Message;
}

/// <summary>パーサの解析結果。AST と蓄積したエラー。</summary>
public sealed class ParseResult
{
    public object? Ast { get; }

    /// <summary>構文エラー (位置付きメッセージ)。</summary>
    public IReadOnlyList<ParseError> Errors { get; }

    /// <summary>意味解析の診断 (<see cref="SemanticContext"/> から生成)。既定は空。</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>LightGlr モードで複数解釈が accept まで残った場合の候補 AST (先頭 = Ast と同じ)。LALR モード・非曖昧入力では空。</summary>
    public IReadOnlyList<object?> AmbiguousCandidates { get; }

    /// <summary>構文エラー、または意味解析の Error 診断が 1 つでもあれば true。</summary>
    public bool HasErrors => Errors.Count > 0 || Diagnostics.Any(d => d.Severity == Severity.Error);

    public ParseResult(object? ast, IReadOnlyList<ParseError> errors, IReadOnlyList<Diagnostic>? diagnostics = null,
        IReadOnlyList<object?>? ambiguousCandidates = null)
    {
        Ast = ast;
        Errors = errors;
        Diagnostics = diagnostics ?? System.Array.Empty<Diagnostic>();
        AmbiguousCandidates = ambiguousCandidates ?? System.Array.Empty<object?>();
    }
}
