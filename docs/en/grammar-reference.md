# Grammar reference

English / [日本語](../ja/grammar-reference.md)

AstFirst grammars are written with C# classes and attributes. The generator emits the Lexer / Parser / per-node partials at compile time. This document describes the current `[Rule]` static model (`OnReduce` / Walker / `[Enter]`/`[Exit]`/`[OnReduce]` attributes / `OnSecondPass` / `Accept`/`Reject` / partial child storage). See the [README](../../README.md) for the overview.

## Attribute reference

| Attribute | Target | Role |
|---|---|---|
| `[Grammar]` | class | Start symbol (root nonterminal). Generator's extraction entry point. `Mode` switches dialects. |
| `[Rule]` | static method | A production. The method's **parameters** are the RHS. Multiple per class allowed (see below). |
| `[Token(@"regex")]` / `[Pattern(@"regex")]` | `Token` parameter of a `[Rule]` method | Lexical rule (regex). `Priority` sets lexer priority. |
| `[Precedence(n)]` | class (operator node) | Operator precedence/associativity. Higher `n` binds tighter. |
| `[Repeat]` / `[Repeat(Min=0)]` | `AstNode`-derived parameter of a `[Rule]` method | List (repetition). `Min=1` (default) = one or more, `Min=0` = zero or more. Expands to `IReadOnlyList<T>`. |
| `[Skip(@"regex")]` | class (same as `[Grammar]`) | Skip pattern (whitespace, comments). |
| `[OnReduce]` / `[Enter]` / `[Exit]` | static method (on the `[Grammar]` root class) | Semantic rule. The generator dispatches it from the constructor (`[OnReduce]`) / Walker (`[Enter]`/`[Exit]`); the ctx cast is injected. |

## `[Grammar]`

Attach to the abstract class that is the start symbol (root nonterminal). The generator starts extraction here.

```csharp
[Grammar]
[Skip(@"\s+")]
public abstract partial class Expr : AstNode { }
```

The `Mode` named property switches dialects (see below).

### ParseMode (parser execution mode)

The `ParseMode` named property selects the parser execution mode. Default is `Lalr` (deterministic LALR(1)).

| Value | Behavior |
|---|---|
| `ParseMode.Lalr` (default) | Deterministic LALR(1). Conflicts resolved by precedence/associativity; unresolved ones are warnings (ASTF001). |
| `ParseMode.LightGlr` | Lightweight GLR. Forks in parallel at conflict cells, merges on convergence. Handles **inherent ambiguity** such as cast/paren or generic type/expression. Conflicts are resolved by forking, so no ASTF001. Result is a single AST (`ParseResult.Ast`); if multiple interpretations survive, observe via `ParseResult.AmbiguousCandidates`. |

```csharp
[Grammar(ParseMode = ParseMode.LightGlr)]
```

- **One mode per class**: `Lalr` and `LightGlr` cannot be specified simultaneously on the same `[Grammar]` root (choose one). Combinable with `Mode` (dialect).
- **OnReduce constraint**: In LightGlr, `OnReduce` is invoked at reduce time even for undetermined branches. Therefore `OnReduce` (partial) must only set the node's own properties (`Name`/`Value`/`Span`, etc.) and must **not** mutate external state (`ScopedSymbolTable` / `DiagnosticBag`, etc.). Do semantic analysis in the second pass via `[Enter]`/`[Exit]` (Walker) to avoid leftover side effects from discarded branches.
- **Error repair (Corchuelo et al. ER1/ER2/ER3) known limitations**:
  - **Inserted tokens have empty text + estimated Span**: Tokens inserted by ER1 are `BasicToken("", ...)` (empty text) since the user did not write them. The Span is interpolated from surrounding tokens (prev token's End ~ next token's Start). `Token.Text` is empty string `""`, so `int.Parse("")` throws `FormatException`, but ER3 SimulateForward validates with real reduce + try/catch to reject such candidates.
  - **N=3 and costs are fixed**: Forward move symbols `N=3`, insert cost=1/delete cost=2 are hardcoded. The Corchuelo paper recommends per-language tuning; not yet supported.
  - **Single-pass repair (no recursion)**: The original Corchuelo applies ER1/ER2/ER3 recursively; this implementation applies one round only. Consecutive errors are repaired one at a time on subsequent dead states.
  - **SimulateForward checks the first path only**: Does not fork at conflict cells during simulation, so full agreement with production fork paths is not guaranteed.
- **Error recovery behavior**: LightGlr's Corchuelo repair differs from the panic-mode recovery used in Lalr mode — it inserts/deletes tokens to continue parsing. The same input may produce different error positions/messages depending on the mode.

### ⚠ Breaking Changes (0.4.0)

The following changes are **not backward compatible**. Existing code must be updated.

- **OnReduce ctx is read-only**: `OnReduce(MyCtx ctx)` → `OnReduce(SemanticContext ctx)`. The ctx type is always `SemanticContext` (base class). `OnAccepted` still receives the user's ctx type (writable).
- **SemanticContext.Symbols is read-only**: Returns `IReadOnlySymbolTable` (`Lookup` only). `TryDeclare` / `PushScope` / `PopScope` are not available.
- **SemanticContext.Diagnostics removed**: `Diagnostics` moved to `BasicSemanticContext`. `ctx.Diagnostics.Error(...)` in OnReduce is a compile error.
- **Writes go in [Enter]/[Exit]**: Use `ctx.WritableSymbols.TryDeclare(...)` / `ctx.Diagnostics.Error(...)` inside `[Enter]`/`[Exit]` attribute methods (2nd-pass Walker).

**Migration example**:
```csharp
// ❌ Before (0.3.0): declarations/diagnostics in OnReduce
partial void OnReduce(MyCtx ctx)
{
    if (!ctx.Symbols.TryDeclare(...)) ctx.Diagnostics.Error(...);
}

// ✅ After (0.4.0): OnReduce for node-local only, declarations in [Enter]
partial void OnReduce(SemanticContext ctx) { Name = ...; Span = ...; }
// In [Grammar] root class:
[Enter] static void Declare(MyNode n, BasicSemanticContext ctx)
{
    if (!ctx.WritableSymbols.TryDeclare(...)) ctx.Diagnostics.Error(...);
}
```

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
- **Accept/Reject**: override `IsAccepted` to return `false` to reject a reduce and try fallback candidates. See the [README](../../README.md) "Accept/Reject and fallback" section.
- **`OnSecondPass`**: the second-pass traversal (top-down). For nodes implementing `IOnSecondPassEnter`/`IOnSecondPassExit`, the generator calls `OnSecondPassEnter` (before children) → recurse children → `OnSecondPassExit` (after children).
- **`[OnReduce]` / `[Enter]` / `[Exit]` attributes**: attach to a `static` method on the `[Grammar]` root class and the generator dispatches it from the Walker / constructor (the ctx cast is injected automatically). `[OnReduce]` runs at reduce; `[Enter]`/`[Exit]` run in the second pass. See the [semantic analysis guide](semantic-analysis.md).

## Dialects (Mode)

`[Grammar(Mode = "...")]` emits multiple Lexers/Parsers/Walkers from the same root (format/dialect switching). Generated class names are `<Root>_<Mode>Lexer` / `<Root>_<Mode>Parser` / `<Root>_<Mode>Walker`, etc.

## Regular expressions

`[Token]`/`[Pattern]` regexes are processed via Thompson construction (NFA) → subset construction (DFA) → Hopcroft minimization. Character classes, `{m,n}` quantifiers, and Unicode supplementary planes are supported.

## See also

- [Architecture](architecture.md)
- [Semantic analysis guide](semantic-analysis.md)
- [README](../../README.md)
