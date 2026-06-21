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
    public IReadOnlyList<ParseError> Errors { get; }
    public bool HasErrors => Errors.Count > 0;

    public ParseResult(object? ast, IReadOnlyList<ParseError> errors)
    {
        Ast = ast;
        Errors = errors;
    }
}
