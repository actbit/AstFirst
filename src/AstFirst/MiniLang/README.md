# MiniLang

**JA:** 変数宣言（`let`）・`print`・四則演算のサンプル言語。文リスト（`ConsStmt`/`NilStmt` 連鎖）とキーワード優先度のデモ。
**EN:** Sample language with `let` declarations, `print`, and arithmetic. Demonstrates statement lists (`ConsStmt`/`NilStmt` chain) and keyword priority.

## 文法 / Grammar

```csharp
[Grammar]
[Skip(@"\s+")]
public abstract class Stmt : AstNode { }

public sealed class LetStmt : Stmt { ... }    // let x = expr;
public sealed class PrintStmt : Stmt { ... }  // print expr;
```

式は `NumExpr` / `VarExpr` / `AddExpr` / `MulExpr`（`[Precedence]` 付き）。

## 使い方 / Usage

```csharp
var result = StmtParser.Parse("let x = 1+2*3;");
// result.Ast -> LetStmt { Name = "x", Value = AddExpr(1, MulExpr(2,3)) }
```
