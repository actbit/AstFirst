# Grammar reference

English / [日本語](../ja/grammar-reference.md)

In AstFirst you write the grammar in C# classes and attributes. The generator emits Lexer / Parser / Listener at compile time.

## Attribute overview

| Attribute | Target | Role |
|---|---|---|
| `[Grammar]` | class | Start symbol (root nonterminal). |
| `[Pattern(@"regex")]` | ctor param | Lexical rule (regex). |
| `[Precedence(n)]` | class (operator node) | Operator precedence/associativity. |
| `[Skip(@"regex")]` | class (same as `[Grammar]`) | Skip pattern. |
| `[Expect(token)]` | ctor param | Narrow token kind. |

## `[Grammar]`

Put on the abstract class that is the start symbol (root nonterminal). The generator uses it as the extraction entry point.

```csharp
[Grammar]
[Skip(@"\s+")]
public abstract class Expr : AstNode { }
```

The `Mode` named property switches dialects (see below).

## `[Pattern]`

Put on a constructor parameter to specify a lexical rule (regex). That parameter becomes a terminal.

```csharp
public NumExpr([Pattern(@"[0-9]+")] Token num) { ... }
```

Named properties:

- `Priority` — lexer priority (higher wins). Used when multiple tokens accept the same input, and to resolve shift-reduce conflicts.
- `IsRightAssociative` — right-associative (assignment `=`, power `**`).
- `IsNonAssociative` — non-associative (comparison `<`; `a<b<c` is an error). Takes precedence over `IsRightAssociative`.

```csharp
[Pattern(@"[A-Za-z_]\w*", Priority = 0)]    // identifier (low priority)
[Pattern(@"if", Priority = 1)]               // keyword if (beats identifier)
[Pattern(@"=", IsRightAssociative = true)]    // right-assoc
```

## `[Precedence]`

Put on an operator node (a binary/unary AST class) to set precedence/associativity. Resolves shift-reduce conflicts. Higher `n` binds tighter.

```csharp
[Precedence(1)]                              // priority 1, left-assoc (default)
public sealed class AddExpr : Expr { ... }

[Precedence(2)]                              // priority 2 (* binds tighter than +)
public sealed class MulExpr : Expr { ... }
```

Named properties:

- `IsRightAssociative` — right-associative (`=`, `**`).
- `IsNonAssociative` — non-associative (`<`, `>`). Takes precedence over `IsRightAssociative`.

## `[Skip]`

A skip pattern (whitespace, comments). Put on the same class as `[Grammar]`. Matched spans are excluded from the token stream.

```csharp
[Skip(@"(\s|//[^\n]*)+")]   // whitespace and line comments
```

## `[Expect]`

Narrows the token kind of a constructor parameter.

## Writing rules

- **Inheritance tree = syntax**: `[Grammar] public abstract class Expr` is a nonterminal. `sealed class NumExpr : Expr` is the rule `Expr -> [0-9]+`.
- **Constructor params = RHS**: types and order express the RHS. `AddExpr(Expr left, [Pattern(@"\+")] Token op, Expr right)` is `Expr -> Expr + Expr`.
- **Multiple constructors = multiple rules**: each constructor in a class is an independent production.
- **Abstract classes**: an `abstract class` is a nonterminal base. Concrete rules are `sealed class` (or concrete classes).

```csharp
public sealed class DeclStmt : Stmt
{
    public DeclStmt([Pattern(@"int", Priority = 1)] Token kw, [Pattern(@"[A-Za-z_]\w*")] Token name, [Pattern(@";")] Token semi) { ... }
    public DeclStmt([Pattern(@"int", Priority = 1)] Token kw, [Pattern(@"[A-Za-z_]\w*")] Token name, [Pattern(@"=")] Token eq, Expr init, [Pattern(@";")] Token semi) { ... }
}
```

## Token and SemanticContext

Special constructor parameter types:

- **`Token` type** (with `[Pattern]`): terminal. Use `Token` base or a derived class. Carries `Text` and `Span`.
- **`SemanticContext`-derived type**: not a RHS child; the parser injects the semantic context (determined by **type**, not attribute). Use `ctx.Symbols` / `ctx.Diagnostics` for semantic analysis.

## Dialects (Mode)

`[Grammar(Mode = "...")]` generates multiple Parser/Listener from the same root (to switch formats/dialects). Generated class names become `<Root>_<Mode>Parser`, etc.

## Regex

The `[Pattern]` regex is processed via Thompson construction (NFA) -> subset construction (DFA) -> Hopcroft minimization. Supports character classes, `{m,n}` quantifiers, and Unicode supplementary planes.

## See also

- [Architecture](architecture.md)
- [Semantic analysis guide](semantic-analysis.md)
