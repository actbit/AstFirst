using AstFirst;

namespace MiniC;

/// <summary>軽量C言語パーサのサンプル。変数・代入・if/while・式・print ([Rule] static モデル)。</summary>

/// <summary>MiniC の意味解析コンテキスト。シンボル表・診断・型コンテキストを保持。</summary>
public sealed class MiniCContext : BasicSemanticContext
{
    public TypeContext Types { get; } = new();
}

[Grammar]
[Skip(@"(\s|//[^\n]*)+")]
public abstract partial class Program : AstNode { }

// --- 文リスト ([Repeat(Min=0)] で0回以上の Stmt を IReadOnlyList<Stmt> に。旧 Cons セル廃止) ---
public sealed partial class ProgramBody : Program
{
    [Rule]
    public static void Body([Repeat(Min = 0)] Stmt statements, MiniCContext ctx) { }
}

public abstract partial class Stmt : AstNode { }

// int x; (宣言のみ)
public sealed partial class DeclStmt : Stmt, IOnSecondPassEnter
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Decl([Token(@"int", Priority = 1)] Token kw, [Token(@"[A-Za-z_]\w*")] Token nameTok, [Token(@";")] Token semi, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Name = NameTok.Text; Span = NameTok.Span; }
    public void OnSecondPassEnter(SemanticContext ctx) => SemanticAnalyzer.EnterDecl(this, (MiniCContext)ctx);
}

// int x = expr; (初期化付き)
public sealed partial class DeclStmtInit : Stmt, IOnSecondPassEnter, IOnSecondPassExit
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void DeclInit([Token(@"int", Priority = 1)] Token kw, [Token(@"[A-Za-z_]\w*")] Token nameTok, [Token(@"=")] Token eq, Expr init, [Token(@";")] Token semi, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Name = NameTok.Text; Span = NameTok.Span; }
    public void OnSecondPassEnter(SemanticContext ctx) => SemanticAnalyzer.EnterDeclInit(this, (MiniCContext)ctx);
    public void OnSecondPassExit(SemanticContext ctx) => SemanticAnalyzer.ExitDeclInit(this, (MiniCContext)ctx);
}

// x = expr;
public sealed partial class AssignStmt : Stmt, IOnSecondPassEnter, IOnSecondPassExit
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Assign([Token(@"[A-Za-z_]\w*")] Token nameTok, [Token(@"=")] Token eq, Expr value, [Token(@";")] Token semi, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Name = NameTok.Text; Span = NameTok.Span; }
    public void OnSecondPassEnter(SemanticContext ctx) => SemanticAnalyzer.EnterAssign(this, (MiniCContext)ctx);
    public void OnSecondPassExit(SemanticContext ctx) => SemanticAnalyzer.ExitAssign(this, (MiniCContext)ctx);
}

// print(expr);
public sealed partial class PrintStmt : Stmt
{
    [Rule]
    public static void Print([Token(@"print", Priority = 1)] Token kw, [Token(@"\(")] Token lp, Expr value, [Token(@"\)")] Token rp, [Token(@";")] Token semi, MiniCContext ctx) { }
}

// if (expr) stmt
public sealed partial class IfStmt : Stmt, IOnSecondPassExit
{
    [Rule]
    public static void If([Token(@"if", Priority = 1)] Token kw, [Token(@"\(")] Token lp, Expr cond, [Token(@"\)")] Token rp, Stmt body, MiniCContext ctx) { }
    public void OnSecondPassExit(SemanticContext ctx) => SemanticAnalyzer.ExitCondition(this.Cond, this.Cond.Span, "if", (MiniCContext)ctx);
}

// while (expr) stmt
public sealed partial class WhileStmt : Stmt, IOnSecondPassExit
{
    [Rule]
    public static void While([Token(@"while", Priority = 1)] Token kw, [Token(@"\(")] Token lp, Expr cond, [Token(@"\)")] Token rp, Stmt body, MiniCContext ctx) { }
    public void OnSecondPassExit(SemanticContext ctx) => SemanticAnalyzer.ExitCondition(this.Cond, this.Cond.Span, "while", (MiniCContext)ctx);
}

// { stmt... } (ブロック内の文リストも [Repeat(Min=0)]。空ブロック可)
public sealed partial class BlockStmt : Stmt, IOnSecondPassEnter, IOnSecondPassExit
{
    [Rule]
    public static void Block([Token(@"\{")] Token lb, [Repeat(Min = 0)] Stmt statements, [Token(@"\}")] Token rb, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Span = SourceSpan.Merge(Lb.Span, Rb.Span); }
    public void OnSecondPassEnter(SemanticContext ctx) => ((MiniCContext)ctx).Symbols.PushScope();
    public void OnSecondPassExit(SemanticContext ctx) => ((MiniCContext)ctx).Symbols.PopScope();
}

// --- 式 ---
public abstract partial class Expr : AstNode { }

public sealed partial class NumExpr : Expr, IOnSecondPassExit
{
    public int Value { get; private set; }
    [Rule]
    public static void NumToken([Token(@"[0-9]+")] Token num, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Value = int.Parse(Num.Text); Span = Num.Span; }
    public void OnSecondPassExit(SemanticContext ctx) => SemanticAnalyzer.SetType(this, SemanticAnalyzer.Int, (MiniCContext)ctx);
}

public sealed partial class BoolExpr : Expr, IOnSecondPassExit
{
    public bool Value { get; private set; }
    [Rule]
    public static void Bool([Token(@"true|false", Priority = 1)] Token kw, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Value = Kw.Text == "true"; Span = Kw.Span; }
    public void OnSecondPassExit(SemanticContext ctx) => SemanticAnalyzer.SetType(this, SemanticAnalyzer.Bool, (MiniCContext)ctx);
}

public sealed partial class VarExpr : Expr, IOnSecondPassEnter
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Var([Token(@"[A-Za-z_]\w*")] Token nameTok, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Name = NameTok.Text; Span = NameTok.Span; }
    public void OnSecondPassEnter(SemanticContext ctx) => SemanticAnalyzer.EnterVar(this, (MiniCContext)ctx);
}

[Precedence(1)]
public sealed partial class AddExpr : Expr, IOnSecondPassExit
{
    [Rule]
    public static void Add(Expr left, [Token(@"\+")] Token op, Expr right, MiniCContext ctx) { }
    public void OnSecondPassExit(SemanticContext ctx) => SemanticAnalyzer.SetType(this, SemanticAnalyzer.Int, (MiniCContext)ctx);
}

[Precedence(1)]
public sealed partial class SubExpr : Expr, IOnSecondPassExit
{
    [Rule]
    public static void Sub(Expr left, [Token(@"-")] Token op, Expr right, MiniCContext ctx) { }
    public void OnSecondPassExit(SemanticContext ctx) => SemanticAnalyzer.SetType(this, SemanticAnalyzer.Int, (MiniCContext)ctx);
}

[Precedence(2)]
public sealed partial class MulExpr : Expr, IOnSecondPassExit
{
    [Rule]
    public static void Mul(Expr left, [Token(@"\*")] Token op, Expr right, MiniCContext ctx) { }
    public void OnSecondPassExit(SemanticContext ctx) => SemanticAnalyzer.SetType(this, SemanticAnalyzer.Int, (MiniCContext)ctx);
}

[Precedence(2)]
public sealed partial class DivExpr : Expr, IOnSecondPassExit
{
    [Rule]
    public static void Div(Expr left, [Token(@"/")] Token op, Expr right, MiniCContext ctx) { }
    public void OnSecondPassExit(SemanticContext ctx) => SemanticAnalyzer.SetType(this, SemanticAnalyzer.Int, (MiniCContext)ctx);
}

[Precedence(3)]
public sealed partial class NegExpr : Expr, IOnSecondPassExit
{
    [Rule]
    public static void Neg([Token(@"-")] Token op, Expr inner, MiniCContext ctx) { }
    public void OnSecondPassExit(SemanticContext ctx) => SemanticAnalyzer.PropagateType(this, this.Inner, (MiniCContext)ctx);
}

public sealed partial class ParenExpr : Expr, IOnSecondPassExit
{
    [Rule]
    public static void Paren([Token(@"\(")] Token lp, Expr inner, [Token(@"\)")] Token rp, MiniCContext ctx) { }
    public void OnSecondPassExit(SemanticContext ctx) => SemanticAnalyzer.PropagateType(this, this.Inner, (MiniCContext)ctx);
}
