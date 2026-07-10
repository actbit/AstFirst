# Calc (電卓 / Calculator)

**JA:** 四則演算のサンプル。`[Precedence]` で `*` を `+` より強く結合させるデモ。README のクイックスタートの題材。
**EN:** Arithmetic sample. Demonstrates `[Precedence]` binding `*` tighter than `+`. The subject of the README quick start.

## 文法 / Grammar

```csharp
[Grammar]
public abstract class Expr : AstNode { }

public sealed class NumExpr : Expr { ... }              // Expr -> [0-9]+
[Precedence(1)] public sealed class AddExpr : Expr { ... }  // Expr -> Expr + Expr
[Precedence(2)] public sealed class MulExpr : Expr { ... }  // Expr -> Expr * Expr
```

## 使い方 / Usage

```csharp
var result = ExprParser.Parse("1+2*3");
// result.Ast -> AddExpr(1, +, MulExpr(2, *, 3))
```

`ExprWalker`（Generator 生成）も使える。AST のウォークは [docs/ja/semantic-analysis.md](../../../docs/ja/semantic-analysis.md) 参照。
