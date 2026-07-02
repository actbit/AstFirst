# Grammar reference

English / [日本語](../ja/grammar-reference.md)

AstFirst grammars are written with C# classes and attributes. The generator emits the Lexer / Parser / per-node partials at compile time. This document describes the current `[Rule]` static model (`OnReduce` / `OnSecondPass` / `Accept`/`Reject` / partial child storage). See the [README](../../README.en.md) for the overview.

## Attribute reference

| Attribute | Target | Role |
|---|---|---|
| `[Grammar]` | class | Start symbol (root nonterminal). Generator's extraction entry point. `Mode` switches dialects. |
| `[Rule]` | static method | A production. The method's **parameters** are the RHS. Multiple per class allowed (see below). |
| `[Token(@"regex")]` / `[Pattern(@"regex")]` | `Token` parameter of a `[Rule]` method | Lexical rule (regex). `Priority` sets lexer priority. |
| `[Precedence(n)]` | class (operator node) | Operator precedence/associativity. Higher `n` binds tighter. |
| `[Repeat]` / `[Repeat(Min=0)]` | `AstNode`-derived parameter of a `[Rule]` method | List (repetition). `Min=1` (default) = one or more, `Min=0` = zero or more. Expands to `IReadOnlyList<T>`. |
| `[Skip(@"regex")]` | class (same as `[Grammar]`) | Skip pattern (whitespace, comments). |

## `[Grammar]`

Attach to the abstract class that is the start symbol (root nonterminal). The generator starts extraction here.

```csharp
[Grammar]
[Skip(@"\s+")]
public abstract partial class Expr : AstNode { }
```

The `Mode` named property switches dialects (see below).

## `[Rule]`

Attach to a static method that defines a production. Multiple per class allowed. The body is empty (semantic actions go in `OnReduce`).

```csharp
public sealed partial class NumExpr : Expr
{
    [Rule]
    public static void NumToken([Token(@"[0-9]+")] Token num) { }
    partial void OnReduce() { Value = int.Parse(Num.Text); }
}
```

- **Parameters = RHS**: types and order express the RHS. `Token` + `[Token]`/`[Pattern]` is a terminal, an `AstNode`-derived type is a child, a `SemanticContext`-derived type is the ctx (injected by the parser).
- **`partial` is required**: the generator emits child/terminal properties (PascalCase of the parameter name, e.g. `Num`/`Left`/`Right`) and a partial constructor that calls `OnReduce`.
- **Multiple `[Rule]`s**: several `[Rule]` methods on one class each become an independent production. Which rule reduced is exposed via the `RuleName` property (method name); branch on it in `OnReduce` with a `switch`.

```csharp
public sealed partial class BinaryExpr : Expr
{
    [Rule] public static void Add(Expr left, [Token(@"\+")] Token op, Expr right) { }
    [Rule] public static void Sub(Expr left, [Token(@"-")]  Token op, Expr right) { }
    partial void OnReduce() { /* use this.RuleName to distinguish "Add"/"Sub" */ }
}
```

## `[Token]` / `[Pattern]`

Attach to a `Token` parameter of a `[Rule]` method to specify the lexical rule (regex). `[Token]` and `[Pattern]` are aliases (`[Token]` is more explicit about intent).

```csharp
[Rule] public static void Num([Token(@"[0-9]+")] Token num) { }
```

Named properties:

- `Priority` — lexer priority (higher wins). Used to resolve when several tokens match the same input.

```csharp
[Token(@"[A-Za-z_]\w*", Priority = 0)]    // identifier (low priority)
[Token(@"if", Priority = 1)]               // keyword if (high priority, beats identifier)
```

## `[Precedence]`

Attach to an operator node (a binary/unary AST class) to set precedence/associativity. Resolves shift-reduce conflicts. Higher `n` binds tighter.

```csharp
[Precedence(1)]                              // priority 1, left-assoc (default)
public sealed partial class AddExpr : Expr { ... }

[Precedence(2)]                              // priority 2 (* binds tighter than +)
public sealed partial class MulExpr : Expr { ... }
```

Named properties:

- `IsRightAssociative` — right associative (`=`, `**`).
- `IsNonAssociative` — non-associative (`<`, `>`). Takes precedence over `IsRightAssociative`.

## `[Repeat]`

Attach to an `AstNode`-derived parameter of a `[Rule]` method to express a list (repetition). The generator expands it into LALR productions (`List_T → item | List_T item | ε`) and the partial property becomes `IReadOnlyList<T>`.

Named properties:

- `Min` — minimum repetition count. `1` (default) = one or more (Plus), `0` = zero or more (Star, empty list allowed).

```csharp
public sealed partial class ProgramBody : Program
{
    [Rule] public static void Body([Repeat(Min = 0)] Stmt statements) { }
    // → Program → Stmt* (Statements is IReadOnlyList<Stmt>, empty allowed)
}

public sealed partial class NonEmpty : Program
{
    [Rule] public static void Body([Repeat] Stmt statements) { }   // Min=1 (default) → Stmt+
}
```

## `[Skip]`

Skip pattern (whitespace, comments). Attach to the same class as `[Grammar]`. Matched spans are removed from the token stream.

```csharp
[Skip(@"(\s|//[^\n]*)+")]   // whitespace and line comments
```

## Writing grammar

- **Inheritance tree = syntax**: `[Grammar] public abstract partial class Expr` is a nonterminal. `sealed partial class NumExpr : Expr` is the rule `Expr -> [0-9]+`.
- **`[Rule]` parameters = RHS**: `AddExpr`'s `[Rule] static void Add(Expr left, [Token(@"\+")] Token op, Expr right)` is `Expr -> Expr + Expr`.
- **Abstract classes**: an `abstract class` is a nonterminal base. Concrete rules are `sealed class` (or concrete classes).

### Intermediate abstract classes

Inheritance hierarchies with abstract classes in between (`Root → Mid → Leaf`) are supported. The abstract class also acts as a nonterminal; the generator emits unit productions (passing the value through) so it is reachable from the start symbol.

Declare shared properties on the abstract base with a `[Rule]`; concrete subclasses initialize them via `: base(...)`, inheriting the properties while keeping them `readonly`.

```csharp
public abstract partial class ABinary : ANode
{
    [Rule] public static void Base(ANode left, ANode right) { }   // declare shared Left/Right
}

public sealed partial class AAdd : ABinary
{
    [Rule] public static void Add(ANode left, [Token(@"\+")] Token op, ANode right) { }
    // → internal AAdd(...) : base(ruleName, left, right) { Op = op; }
    // Left/Right are inherited readonly properties of ABinary (not redefined)
}
```

## Token and SemanticContext

Special parameter types of a `[Rule]` method:

- **`Token` type** (with `[Token]`/`[Pattern]`): terminal. The `Token` base type or a derived class. Carries `Text` and `Span`.
- **`AstNode`-derived type**: a RHS child. The generator emits a partial property (PascalCase of the parameter name).
- **`SemanticContext`-derived type**: not a RHS child; the parser injects the semantic context (determined by **type**, not attribute).

## `OnReduce` / Accept/Reject / OnSecondPass

- **`OnReduce(ctx)`**: a partial method called when a rule is reduced (bottom-up). Child properties and `Span` (auto-computed from children) are already set. Use `this.RuleName` to branch on the rule, override `Span`, etc.
- **Accept/Reject**: override `IsAccepted` to return `false` to reject a reduce and try fallback candidates. See the [README](../../README.en.md) "Accept/Reject and fallback" section.
- **`OnSecondPass`**: the second-pass traversal (top-down). For nodes implementing `IOnSecondPassEnter`/`IOnSecondPassExit`, the generator calls `OnSecondPassEnter` (before children) → recurse children → `OnSecondPassExit` (after children). See the [semantic analysis guide](semantic-analysis.md).

## Dialects (Mode)

`[Grammar(Mode = "...")]` emits multiple Parsers/Listeners from the same root (format/dialect switching). Generated class names are `<Root>_<Mode>Parser`, etc.

## Regular expressions

`[Token]`/`[Pattern]` regexes are processed via Thompson construction (NFA) → subset construction (DFA) → Hopcroft minimization. Character classes, `{m,n}` quantifiers, and Unicode supplementary planes are supported.

## See also

- [Architecture](architecture.md)
- [Semantic analysis guide](semantic-analysis.md)
- [README](../../README.en.md)
