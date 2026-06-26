# AstFirst

[日本語](README.md) / English

A parser generator where you write the grammar in **plain C# classes and attributes**, and a Source Generator emits a Lexer and an LALR(1) Parser at compile time. The generated Parser returns an AST you can layer semantic analysis on (scoped symbol table, two-pass walk, type checking, and `Accept`/`Reject` to resolve semantic ambiguity).

## Features

- **Grammar in C# code**: the inheritance tree expresses syntax; the parameters of a `[Rule]` static method express the RHS and lexical rules. No special DSL files.
- **Source Generator (`IIncrementalGenerator`)**: emits Lexer / Parser / partial properties C# code at compile time. No runtime code generation.
- **Regex-based lexer**: character-class compaction, longest-match + priority-driven, `{m,n}` quantifiers, Unicode supplementary planes. Computes **line/column** of each token.
- **LALR(1) parsing**: resolves shift-reduce conflicts with precedence/associativity (`[Precedence]`) (e.g. `*` > `+`, right-associative assignment).
- **Semantic ambiguity resolution (Accept/Reject)**: call `Reject()` in the reduce-time `OnReduce` to fall back to the next candidate (another rule / shift) in priority order. Resolves meaning-dependent ambiguity like cast vs. parenthesized expression during parsing.
- **AST construction + automatic child retention**: at reduce time a generator-emitted partial constructor sets children/terminals into properties automatically and calls `OnReduce`. No manual child assignment.
- **Two-pass semantic analysis**: after `Parse`, each node's `OnSecondPassEnter`/`OnSecondPassExit` (top-down) is called automatically. Accurate semantic analysis like scope Push/Pop is straightforward.
- **Semantic analysis helpers**: scoped symbol table (`ScopedSymbolTable`), symbol resolution (`ResolveOrError`), type checking (`TypeSymbol`/`TypeContext`), binding (`AstNode.SetAnnotation`), diagnostics (`ParseResult.Diagnostics`).
- **Error recovery**: continues after syntax errors via panic mode; `ParseResult` carries the AST + error list.

## Quick start

### 1. Write the grammar

```csharp
using AstFirst;

[Grammar]                              // start symbol
[Skip(@"\s+")]                         // skip whitespace
public abstract partial class Expr : AstNode { }

public sealed partial class NumExpr : Expr     // rule: Expr -> [0-9]+
{
    public int Value { get; private set; }
    [Rule]
    public static void NumToken([Token(@"[0-9]+")] Token num) { }   // RHS = parameters
    partial void OnReduce()                    // at reduce, bottom-up
    {
        Value = int.Parse(Num.Text);
        Span = Num.Span;                       // set the node's source span
    }
}

[Precedence(1)]                        // priority 1, left-assoc (default)
public sealed partial class AddExpr : Expr     // rule: Expr -> Expr + Expr
{
    [Rule]
    public static void Add(Expr left, [Token(@"\+")] Token op, Expr right) { }
    partial void OnReduce() => Span = SourceSpan.Merge(Left.Span, Right.Span);
}

[Precedence(2)]                        // priority 2 (higher), left-assoc
public sealed partial class MulExpr : Expr     // rule: Expr -> Expr * Expr
{
    [Rule]
    public static void Mul(Expr left, [Token(@"\*")] Token op, Expr right) { }
    partial void OnReduce() => Span = SourceSpan.Merge(Left.Span, Right.Span);
}
```

- The **parameters** of the `[Rule]` static method (one per class, void, empty body) are the RHS. `Token` + `[Token]`/`[Pattern]` is a terminal; an `AstNode`-derived type is a child; a `SemanticContext`-derived type is the ctx (injected by the parser).
- `partial` is required. The generator emits child/terminal properties (PascalCase of the parameter name, e.g. `Num`/`Left`/`Right`) and a partial constructor, and calls `OnReduce`.

### 2. Generator emits Lexer / Parser / partials

`ExprLexer` / `ExprParser` and each node's partial properties/constructor are generated automatically at compile time.

### 3. Just call it

```csharp
var result = ExprParser.Parse("1+2*3");
// result.Ast      -> AddExpr(NumExpr(1), +, MulExpr(NumExpr(2), *, NumExpr(3)))
//                  (* binds tighter than +)
// result.Errors   -> [] (no syntax errors)
// result.HasErrors -> false

var result2 = ExprParser.Parse("1+");
// result2.HasErrors -> true (recovered via panic mode)
```

## Semantic analysis

AstFirst provides standard helpers and a two-pass framework for semantic analysis on top of parsing. See [docs/en/semantic-analysis.md](docs/en/semantic-analysis.md) for details.

- **First pass `OnReduce` (bottom-up)**: called at reduce time. `Accept()`/`Reject()` decides whether to accept this interpretation (default Accept). `Reject` falls back to the next candidate.
- **Second pass `OnSecondPassEnter`/`Exit` (top-down)**: called automatically from the AST root after `Parse` (Enter -> recurse children -> Exit). Accurate semantic analysis like scope Push/Pop fits here.
- **Scoped symbol table** (`ScopedSymbolTable`) — lexical scope management
- **Symbol resolution** (`ResolveOrError`) — detect undeclared references
- **Type checking** (`TypeSymbol` / `TypeContext`) — represent and check types
- **Binding** (`AstNode.SetAnnotation`) — attach resolved symbols/types to nodes
- **Diagnostics** (`ParseResult.Diagnostics`) — retrieve semantic diagnostics

### Accept/Reject and fallback

Calling `Reject()` in the reduce-time `OnReduce` discards that interpretation and falls back to the **next candidate in priority order** (another rule / shift). This resolves **meaning-dependent ambiguity** like cast `(Type)e` vs. parenthesized expression `(e)` during parsing.

```csharp
public sealed partial class CastExpr : Expr
{
    [Rule] public static void Cast(Type t, [Token(@"\)")] Token rp, Expr e, SemanticContext ctx) { }
    partial void OnReduce(SemanticContext ctx)
    {
        // If Type is not a known type, Reject -> fall back to the parenthesized-expression rule
        if (!IsKnownType(T.Name)) Reject();
    }
}
```

### Second pass (OnSecondPass)

Write `public override void OnSecondPassEnter(SemanticContext ctx)` / `OnSecondPassExit` on a node; they are called top-down (before/after children) after `Parse`. Ideal for block-scope Push/Pop.

```csharp
// MiniC: open/close a scope on BlockStmt
public sealed partial class BlockStmt : Stmt
{
    [Rule] public static void Block([Token(@"\{")] Token lb, Program body, [Token(@"\}")] Token rb, MiniCContext ctx) { }
    public override void OnSecondPassEnter(SemanticContext ctx) => ((MiniCContext)ctx).Symbols.PushScope();
    public override void OnSecondPassExit(SemanticContext ctx) => ((MiniCContext)ctx).Symbols.PopScope();
}
```

### Scoped symbol table

`ScopedSymbolTable` is a stack of lexical scopes. It records declaration spans and resolves names innermost-first.

- `PushScope(key, kind)` / `PopScope(key)` — open/close a scope (keyed). Argument-less variants remain for back-compat.
- `Lookup(name)` — from current scope outward (null if undeclared)
- `TryDeclare(name, span, value, out existing)` — declare; rejects same-scope duplicates, allows shadowing of outer
- `ResolveOrError(name, span, bag)` — resolve, or add an Error to `bag` and return null

### Type checking

`TypeSymbol` (type representation, inheritance / `IsAssignableFrom`) and `TypeContext` (node -> type). A language-agnostic skeleton; you define concrete types.

```csharp
var Int = new TypeSymbol("int");
var Bool = new TypeSymbol("bool");
// Propagate expression types on OnSecondPassExit
ctx.Types.SetType(node, Int);
// Check a condition's type
if (ctx.Types.TypeOf(cond) is { } t && !Bool.IsAssignableFrom(t))
    ctx.Diagnostics.Error("if condition must be bool", cond.Span);
```

### Binding

`AstNode.SetAnnotation/GetAnnotation<T>` attach a resolved symbol or type to a node.

```csharp
node.SetAnnotation("symbol", resolvedSymbol);
var sym = node.GetAnnotation<SymbolEntry>("symbol");
```

### Retrieving diagnostics

Semantic diagnostics (added to `ctx.Diagnostics` in `OnReduce`/`OnSecondPass`) are available via `ParseResult.Diagnostics`.

```csharp
var result = ProgramParser.Parse(code, new MiniCContext());
// result.Errors      -> syntax errors (ParseError)
// result.Diagnostics -> semantic diagnostics (Diagnostic)
// result.HasErrors   -> true if any syntax error or semantic Error
```

### Injecting a custom context

```csharp
public sealed class MiniCContext : BasicSemanticContext
{
    public TypeContext Types { get; } = new();
}
var result = ProgramParser.Parse(code, new MiniCContext());
```

`Parse(string)` forwards to `Parse(string, SemanticContext?)` and uses `BasicSemanticContext` when omitted. Derive from `BasicSemanticContext` to add your own state (e.g. a type context).

### Source positions (line/column)

`SourceSpan` carries accurate 1-based line/column. The lexer computes line/column while tokenizing; it propagates through `Token.Span` to AST node `Span`. Diagnostics report accurate positions.

### Example: MiniC semantic analysis

`samples/MiniC/SemanticAnalyzer.cs` (a static helper) plus each node's `OnSecondPass` perform scope management, symbol resolution, and type checking (int/bool). Run with `dotnet run --project samples/MiniC`.

```
--- Undeclared reference ---
  Semantic diagnostics:
    Error: 'x' is not declared @ (1,7)-(1,8)

--- Type error: if condition is int ---
  Semantic diagnostics:
    Error: if condition must be bool (got: int) @ (1,5)-(1,6)

--- Shadowing (allowed) ---
  Semantic analysis: no diagnostics (OK)
```

## Attribute reference

See [docs/en/grammar-reference.md](docs/en/grammar-reference.md) for details.

| Attribute | Target | Role |
|---|---|---|
| `[Grammar]` | class | Start symbol (root nonterminal). Generator's extraction entry point. `Mode` switches dialects. |
| `[Rule]` | static method | A production (one per class). The method's **parameters** are the RHS. |
| `[Token(@"regex")]` / `[Pattern(@"regex")]` | `Token` parameter of a `[Rule]` method | Lexical rule (regex). `Priority` sets lexer priority (higher wins). |
| `[Precedence(n)]` | class (operator node) | Operator precedence/associativity. Higher `n` binds tighter. `IsRightAssociative`/`IsNonAssociative`. |
| `[Skip(@"regex")]` | class (same as `[Grammar]`) | Skip pattern (whitespace, comments). |

### `[Rule]` method parameters (type-based classification)

- **`Token` type** (with `[Token]`/`[Pattern]`): terminal. Carries `Text` and `Span`.
- **`AstNode`-derived type**: a RHS child. The generator emits a partial property (PascalCase of the parameter name).
- **`SemanticContext`-derived type**: not a RHS child; the parser injects the semantic context (determined by **type**, not attribute).

### `[Token]` / `[Precedence]` named properties

```csharp
[Token(@"[A-Za-z_]\w*", Priority = 0)]    // identifier (low priority)
[Token(@"if", Priority = 1)]               // keyword if (beats identifier)

[Precedence(1)]                              // priority 1, left-assoc (e.g. + -)
[Precedence(2)]                              // priority 2 (binds tighter than +; e.g. * /)
[Precedence(3, IsRightAssociative = true)]    // priority 3, right-assoc (tighter than *; e.g. power **)
[Precedence(1, IsNonAssociative = true)]      // priority 1, non-assoc (e.g. comparison < >; a<b<c is an error)
```

### Writing grammar

- **Inheritance tree = syntax**: `[Grammar] public abstract partial class Expr` is a nonterminal. `sealed partial class NumExpr : Expr` is the rule `Expr -> [0-9]+`.
- **`[Rule]` method parameters = RHS**: types and order express the RHS. `[Rule] static void Add(Expr left, [Token(@"\+")] Token op, Expr right)` is `Expr -> Expr + Expr`.
- **One `[Rule]` per class**: multiple rules go into separate classes (e.g. empty `JsonObjectEmpty` vs non-empty `JsonObject`). Avoid name clashes between the method name and the parameter's PascalCase property (e.g. param `name` -> property `NameTok`; method name `NumToken`).

## Samples

- **Calculator** (`src/AstFirst/Calc/`) — arithmetic with precedence.
- **MiniLang** (`src/AstFirst/MiniLang/`) — `let`/`print`/arithmetic.
- **JSON parser** (`samples/JsonParser/`) — JSON primitive types.
- **MiniC** (`samples/MiniC/`) — variables, assignment, `if`/`while`, blocks, bool. **Semantic analysis (two-pass + type checking) demo**.
- **MiniBASIC** (`samples/MiniBasic/`) — line-numbered BASIC.
- **C# parser** (`samples/CSharpParser/`) — full C# grammar (ECMA-334 Annex A). Parsing + AST construction only (no semantic analysis). Grammar defined in `samples/Perf/Perf.Grammars/CSharpFactory.cs`.

See each sample's README.

## Architecture

See [docs/en/architecture.md](docs/en/architecture.md) for details.

```
AstFirst.slnx
├── src/
│   ├── AstFirst.Core/        netstandard2.0  Pure logic (lexer DFA / LALR). No Roslyn dependency.
│   ├── AstFirst.Runtime/     net10.0         Attributes, base classes, semantic analysis (ScopedSymbolTable / TypeSystem / SemanticContext / AstNode / Token)
│   ├── AstFirst.Generator/   netstandard2.0  IIncrementalGenerator. Includes Core sources into a single assembly.
│   └── AstFirst/             net10.0         User code (calculator / MiniLang samples)
├── samples/                   net10.0         JsonParser / MiniC / MiniBasic / CSharpParser / Perf
└── tests/                     net10.0         AstFirst.Tests (Core/Runtime + EndToEnd) / AstFirst.Generator.Tests
```

- The generator reads C# via Roslyn, converts it to an equality-comparable POCO model (the lifeline of caching), builds DFA/LALR tables via Core's pure logic, and emits Lexer/Parser/partial C# code.
- Generated code depends on Runtime. Lexer/Parser embed DFA/LALR tables in `static readonly` arrays and drive shift/reduce. At reduce, a partial constructor sets the children and calls `OnReduce`; on Reject it falls back to the next candidate. After `Parse`, `OnSecondPassEnter/Exit` are called top-down.
- The generator Compile-Includes Core sources into a single assembly (avoids dependency loading issues for analyzers).

## Documentation

- [Architecture](docs/en/architecture.md)
- [Semantic analysis guide](docs/en/semantic-analysis.md)
- [Grammar reference](docs/en/grammar-reference.md)

Japanese versions are under `docs/ja/` and [README.md](README.md).

## Tests

279 tests (AstFirst.Tests 236 + Generator.Tests 43). Covers lexer/DFA/LALR stages, end-to-end, error recovery, semantic analysis (scopes, two-pass, type checking, ctx -> `ParseResult.Diagnostics` integration), `Accept`/`Reject` fallback, and positions (line/column).

## License

MIT (LICENSE.txt)
