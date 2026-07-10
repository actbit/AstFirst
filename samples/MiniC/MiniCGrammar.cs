using AstFirst;

namespace MiniC;

/// <summary>軽量C言語パーサのサンプル。変数・代入・if/while・式・print ([Rule] static + [Enter]/[Exit] 属性モデル)。</summary>

/// <summary>MiniC の意味解析コンテキスト。シンボル表・診断・型コンテキスト (基底 BasicSemanticContext.Types を使用)。</summary>
public sealed class MiniCContext : BasicSemanticContext { }

[Grammar]
[Skip(@"(\s|//[^\n]*)+")]
public abstract partial class Program : AstNode
{
    // --- 意味解析ルール ([Enter]/[Exit] 属性)。ルートクラスに集約。
    //     Generator が Walker の Enter/Exit フェーズで自動呼出し、ctx のキャストも自動挿入する (ボイラープレート不要)。 ---
    [Enter] public static void EnterDecl(DeclStmt n, MiniCContext ctx) => SemanticAnalyzer.EnterDecl(n, ctx);
    [Enter] public static void EnterDeclInit(DeclStmtInit n, MiniCContext ctx) => SemanticAnalyzer.EnterDeclInit(n, ctx);
    [Exit] public static void ExitDeclInit(DeclStmtInit n, MiniCContext ctx) => SemanticAnalyzer.ExitDeclInit(n, ctx);
    [Enter] public static void EnterAssign(AssignStmt n, MiniCContext ctx) => SemanticAnalyzer.EnterAssign(n, ctx);
    [Exit] public static void ExitAssign(AssignStmt n, MiniCContext ctx) => SemanticAnalyzer.ExitAssign(n, ctx);
    [Exit] public static void ExitIf(IfStmt n, MiniCContext ctx) => SemanticAnalyzer.ExitCondition(n.Cond, n.Cond.Span, "if", ctx);
    [Exit] public static void ExitWhile(WhileStmt n, MiniCContext ctx) => SemanticAnalyzer.ExitCondition(n.Cond, n.Cond.Span, "while", ctx);
    [Enter] public static void EnterBlock(BlockStmt n, MiniCContext ctx) => ctx.Symbols.PushScope();
    [Exit] public static void ExitBlock(BlockStmt n, MiniCContext ctx) => ctx.Symbols.PopScope();
    [Enter] public static void EnterVar(VarExpr n, MiniCContext ctx) => SemanticAnalyzer.EnterVar(n, ctx);
    [Exit] public static void ExitNum(NumExpr n, MiniCContext ctx) => SemanticAnalyzer.SetType(n, SemanticAnalyzer.Int, ctx);
    [Exit] public static void ExitBool(BoolExpr n, MiniCContext ctx) => SemanticAnalyzer.SetType(n, SemanticAnalyzer.Bool, ctx);
    [Exit] public static void ExitAdd(AddExpr n, MiniCContext ctx) => SemanticAnalyzer.SetType(n, SemanticAnalyzer.Int, ctx);
    [Exit] public static void ExitSub(SubExpr n, MiniCContext ctx) => SemanticAnalyzer.SetType(n, SemanticAnalyzer.Int, ctx);
    [Exit] public static void ExitMul(MulExpr n, MiniCContext ctx) => SemanticAnalyzer.SetType(n, SemanticAnalyzer.Int, ctx);
    [Exit] public static void ExitDiv(DivExpr n, MiniCContext ctx) => SemanticAnalyzer.SetType(n, SemanticAnalyzer.Int, ctx);
    [Exit] public static void ExitNeg(NegExpr n, MiniCContext ctx) => SemanticAnalyzer.PropagateType(n, n.Inner, ctx);
    [Exit] public static void ExitParen(ParenExpr n, MiniCContext ctx) => SemanticAnalyzer.PropagateType(n, n.Inner, ctx);
}

// --- 文リスト ([Repeat(Min=0)] で0回以上の Stmt を IReadOnlyList<Stmt> に) ---
public sealed partial class ProgramBody : Program
{
    [Rule]
    public static void Body([Repeat(Min = 0)] Stmt statements, MiniCContext ctx) { }
}

public abstract partial class Stmt : AstNode { }

// int x; (宣言のみ)
public sealed partial class DeclStmt : Stmt
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Decl([Token(@"int", Priority = 1)] Token kw, [Token(@"[A-Za-z_]\w*")] Token nameTok, [Token(@";")] Token semi, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Name = NameTok.Text; Span = NameTok.Span; }
}

// int x = expr; (初期化付き)
public sealed partial class DeclStmtInit : Stmt
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void DeclInit([Token(@"int", Priority = 1)] Token kw, [Token(@"[A-Za-z_]\w*")] Token nameTok, [Token(@"=")] Token eq, Expr init, [Token(@";")] Token semi, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Name = NameTok.Text; Span = NameTok.Span; }
}

// x = expr;
public sealed partial class AssignStmt : Stmt
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Assign([Token(@"[A-Za-z_]\w*")] Token nameTok, [Token(@"=")] Token eq, Expr value, [Token(@";")] Token semi, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Name = NameTok.Text; Span = NameTok.Span; }
}

// print(expr);
public sealed partial class PrintStmt : Stmt
{
    [Rule]
    public static void Print([Token(@"print", Priority = 1)] Token kw, [Token(@"\(")] Token lp, Expr value, [Token(@"\)")] Token rp, [Token(@";")] Token semi, MiniCContext ctx) { }
}

// if (expr) stmt
public sealed partial class IfStmt : Stmt
{
    [Rule]
    public static void If([Token(@"if", Priority = 1)] Token kw, [Token(@"\(")] Token lp, Expr cond, [Token(@"\)")] Token rp, Stmt body, MiniCContext ctx) { }
}

// while (expr) stmt
public sealed partial class WhileStmt : Stmt
{
    [Rule]
    public static void While([Token(@"while", Priority = 1)] Token kw, [Token(@"\(")] Token lp, Expr cond, [Token(@"\)")] Token rp, Stmt body, MiniCContext ctx) { }
}

// { stmt... } (ブロック内の文リストも [Repeat(Min=0)]。空ブロック可)
public sealed partial class BlockStmt : Stmt
{
    [Rule]
    public static void Block([Token(@"\{")] Token lb, [Repeat(Min = 0)] Stmt statements, [Token(@"\}")] Token rb, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Span = SourceSpan.Merge(Lb.Span, Rb.Span); }
}

// --- 式 ---
public abstract partial class Expr : AstNode { }

public sealed partial class NumExpr : Expr
{
    public int Value { get; private set; }
    [Rule]
    public static void NumToken([Token(@"[0-9]+")] Token num, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Value = int.Parse(Num.Text); Span = Num.Span; }
}

public sealed partial class BoolExpr : Expr
{
    public bool Value { get; private set; }
    [Rule]
    public static void Bool([Token(@"true|false", Priority = 1)] Token kw, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Value = Kw.Text == "true"; Span = Kw.Span; }
}

public sealed partial class VarExpr : Expr
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Var([Token(@"[A-Za-z_]\w*")] Token nameTok, MiniCContext ctx) { }
    partial void OnReduce(MiniCContext ctx) { Name = NameTok.Text; Span = NameTok.Span; }
}

[Precedence(1)]
public sealed partial class AddExpr : Expr
{
    [Rule]
    public static void Add(Expr left, [Token(@"\+")] Token op, Expr right, MiniCContext ctx) { }
}

[Precedence(1)]
public sealed partial class SubExpr : Expr
{
    [Rule]
    public static void Sub(Expr left, [Token(@"-")] Token op, Expr right, MiniCContext ctx) { }
}

[Precedence(2)]
public sealed partial class MulExpr : Expr
{
    [Rule]
    public static void Mul(Expr left, [Token(@"\*")] Token op, Expr right, MiniCContext ctx) { }
}

[Precedence(2)]
public sealed partial class DivExpr : Expr
{
    [Rule]
    public static void Div(Expr left, [Token(@"/")] Token op, Expr right, MiniCContext ctx) { }
}

[Precedence(3)]
public sealed partial class NegExpr : Expr
{
    [Rule]
    public static void Neg([Token(@"-")] Token op, Expr inner, MiniCContext ctx) { }
}

public sealed partial class ParenExpr : Expr
{
    [Rule]
    public static void Paren([Token(@"\(")] Token lp, Expr inner, [Token(@"\)")] Token rp, MiniCContext ctx) { }
}
