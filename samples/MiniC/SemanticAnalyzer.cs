using System.Collections.Generic;
using AstFirst;

namespace MiniC;

/// <summary>
/// MiniC の意味解析 (2パス目)。Parse 済みの AST をウォークし、スコープ付きシンボル表で
/// 未宣言参照・二重宣言・スコープ外参照を検出する。
/// <para>
/// LALR のボトムアップ reduce では親スコープを子ノードに伝えられない (親のコンストラクタは
/// 子の後に呼ばれる) ため、正確なブロックスコープには Parse 後のこのウォークが必要。
/// </para>
/// </summary>
public sealed class SemanticAnalyzer
{
    private readonly ScopedSymbolTable _symbols = new();
    private readonly List<Diagnostic> _diagnostics = new();

    /// <summary>AST を解析し、検出した診断を返す。</summary>
    public IReadOnlyList<Diagnostic> Analyze(MiniC.Program? program)
    {
        if (program is not null) WalkProgram(program);
        return _diagnostics;
    }

    private void WalkProgram(MiniC.Program p)
    {
        while (p is ConsStmt cons)
        {
            WalkStmt(cons.First);
            p = cons.Rest;
        }
    }

    private void WalkStmt(Stmt s)
    {
        switch (s)
        {
            case DeclStmt d:
                // 宣言を先に登録してから初期化式を評価する (C 風: 右辺には自分を含む宣言が見える)。
                if (!_symbols.TryDeclare(d.Name, d.Span, null, out _))
                    Error($"変数 '{d.Name}' は既に宣言されています", d.Span);
                if (d.Init is not null) WalkExpr(d.Init);
                break;
            case AssignStmt a:
                if (_symbols.Lookup(a.Name) is null)
                    Error($"変数 '{a.Name}' は宣言されていません", a.Span);
                WalkExpr(a.Value);
                break;
            case PrintStmt pr:
                WalkExpr(pr.Value);
                break;
            case IfStmt i:
                WalkExpr(i.Cond);
                WalkStmt(i.Body);
                break;
            case WhileStmt w:
                WalkExpr(w.Cond);
                WalkStmt(w.Body);
                break;
            case BlockStmt b:
                _symbols.PushScope();
                WalkProgram(b.Body);
                _symbols.PopScope();
                break;
        }
    }

    private void WalkExpr(Expr e)
    {
        switch (e)
        {
            case VarExpr v:
                if (_symbols.Lookup(v.Name) is null)
                    Error($"変数 '{v.Name}' は宣言されていません", v.Span);
                break;
            case AddExpr a: WalkExpr(a.Left); WalkExpr(a.Right); break;
            case SubExpr a: WalkExpr(a.Left); WalkExpr(a.Right); break;
            case MulExpr a: WalkExpr(a.Left); WalkExpr(a.Right); break;
            case DivExpr a: WalkExpr(a.Left); WalkExpr(a.Right); break;
            case NegExpr n: WalkExpr(n.Inner); break;
            case ParenExpr pe: WalkExpr(pe.Inner); break;
            case NumExpr: break;
        }
    }

    private void Error(string message, SourceSpan span)
        => _diagnostics.Add(new Diagnostic(message, span, Severity.Error));
}
