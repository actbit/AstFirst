# AstFirst

[日本語](README.md) / English

A parser generator where you write the grammar in **plain C# classes and attributes**, and a Source Generator emits a Lexer and an LALR(1) Parser at compile time. The generated Parser returns an AST you can layer semantic analysis on (scoped symbol table, listener, type checking).

## Features

- **Grammar in C# code**: the inheritance tree expresses syntax, constructor parameters with `[Pattern]` express lexical rules. No special DSL files.
- **Source Generator (`IIncrementalGenerator`)**: emits Lexer / Parser / **Listener** C# code at compile time. No runtime code generation.
- **Regex-based lexer**: character-class compaction, longest-match + priority-driven, `{m,n}` quantifiers, Unicode supplementary planes. Computes **line/column** of each token.
- **LALR(1) parsing**: resolves shift-reduce conflicts with precedence/associativity (`[Precedence]`) (e.g. `*` > `+`, right-associative assignment).
- **AST construction**: calls your class constructor at reduce time to store values; the constructor body is the node's semantic action.
- **Semantic analysis**: scoped symbol table (`ScopedSymbolTable`), generator-emitted **Listener**, symbol resolution (`ResolveOrError`), type checking (`TypeSymbol`/`TypeContext`), binding (`AstNode.SetAnnotation`), diagnostics (`ParseResult.Diagnostics`).
- **Error recovery**: continues after syntax errors via panic mode; `ParseResult` carries the AST + error list.

## Quick start

### 1. Write the grammar

```csharp
using AstFirst;

[Grammar]                              // start symbol
[Skip(@"\s+")]                         // skip whitespace
public abstract class Expr : AstNode { }

public sealed class NumExpr : Expr     // rule: Expr -> [0-9]+
{
    public int Value { get; }
    public NumExpr([Pattern(@"[0-9]+")] Token num)
    {
        Value = int.Parse(num.Text);
        Span = num.Span;               // set the node's source span
    }
}

[Precedence(1)]                        // priority 1, left-assoc (default)
public sealed class AddExpr : Expr     // rule: Expr -> Expr + Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public AddExpr(Expr left, [Pattern(@"\+")] Token op, Expr right)
    {
        Left = left;
        Right = right;
        Span = SourceSpan.Merge(left.Span, right.Span);
    }
}

[Precedence(2)]                        // priority 2 (higher), left-assoc
public sealed class MulExpr : Expr     // rule: Expr -> Expr * Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public MulExpr(Expr left, [Pattern(@"\*")] Token op, Expr right)
    {
        Left = left;
        Right = right;
        Span = SourceSpan.Merge(left.Span, right.Span);
    }
}
```

### 2. Generator emits Lexer / Parser / Listener

`ExprLexer` / `ExprParser` / `ExprListener` are generated automatically at compile time.

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

AstFirst provides standard helpers for semantic analysis on top of parsing. See [docs/en/semantic-analysis.md](docs/en/semantic-analysis.md) for details.

- **Scoped symbol table** (`ScopedSymbolTable`) — lexical scope management
- **Listener** (`XxxListener`) — a type-safe AST walker emitted by the generator
- **Symbol resolution** (`ResolveOrError`) — detect undeclared references
- **Type checking** (`TypeSymbol` / `TypeContext`) — represent and check types
- **Binding** (`AstNode.SetAnnotation`) — attach resolved symbols/types to nodes
- **Diagnostics** (`ParseResult.Diagnostics`) — retrieve semantic diagnostics

### Listener (generator-emitted)

For each `[Grammar]`, an `XxxListener` abstract class is generated. It has `EnterXxx`/`ExitXxx` per concrete node and `Walk` (Enter -> recurse children -> Exit). Derive and override, then call `Walk(root)`.

```csharp
// MiniC semantic analysis: derive from ProgramListener
public sealed class SemanticAnalyzer : ProgramListener
{
    private readonly ScopedSymbolTable _symbols = new();
    private readonly DiagnosticBag _diagnostics = new();
    public override void EnterBlockStmt(BlockStmt node) => _symbols.PushScope();
    public override void ExitBlockStmt(BlockStmt node) => _symbols.PopScope();
    public override void EnterDeclStmt(DeclStmt node) { /* declare */ }
    public override void EnterVarExpr(VarExpr node) { /* resolve */ }
    public IReadOnlyList<Diagnostic> Analyze(Program p) { Walk(p); return _diagnostics.Items; }
}
```

### Scoped symbol table

`ScopedSymbolTable` is a stack of lexical scopes. It records declaration spans and resolves names innermost-first.

- `PushScope()` / `PopScope()` — open/close a scope
- `Lookup(name)` — from current scope outward (null if undeclared)
- `TryDeclare(name, span, value, out existing)` — declare; rejects same-scope duplicates, allows shadowing of outer
- `ResolveOrError(name, span, bag)` — resolve, or add an Error to `bag` and return null

### Type checking

`TypeSymbol` (type representation, inheritance / `IsAssignableFrom`) and `TypeContext` (node -> type). A language-agnostic skeleton; you define concrete types.

```csharp
var Int = new TypeSymbol("int");
var Bool = new TypeSymbol("bool");
// Propagate expression types on Listener Exit
_types.SetType(node, Int);
// Check a condition's type
if (_types.TypeOf(cond) is { } t && !Bool.IsAssignableFrom(t))
    diag.Error("if condition must be bool", cond.Span);
```

### Binding

`AstNode.SetAnnotation/GetAnnotation<T>` attach a resolved symbol or type to a node.

```csharp
node.SetAnnotation("symbol", resolvedSymbol);
var sym = node.GetAnnotation<SymbolEntry>("symbol");
```

### One-pass vs two-pass (important)

LALR reduction is **bottom-up**. A parent node's constructor (e.g. a block) runs **after** its children, so you cannot "open a scope before entering a block" from a constructor.

- **One-pass (in constructor)**: receive `ctx` via a `SemanticContext`-derived parameter, use `ctx.Symbols` / `ctx.Diagnostics`. Works for declaration-order visibility and duplicate-declaration detection, but **block-scope Push/Pop is not accurate**.
- **Two-pass (AST walk) ★ recommended**: after `Parse`, walk the AST via a Listener and manage block scopes with `PushScope` / `PopScope`.

### Retrieving diagnostics

Semantic diagnostics (added to `ctx.Diagnostics` in a constructor, or collected during a Listener walk) are available via `ParseResult.Diagnostics`.

```csharp
var result = ProgramParser.Parse(code);
// result.Errors      -> syntax errors (ParseError)
// result.Diagnostics -> semantic diagnostics (Diagnostic)
// result.HasErrors   -> true if any syntax error or semantic Error
```

### Injecting a custom context

```csharp
var ctx = new MySemanticContext();         // SemanticContext-derived
var result = ProgramParser.Parse(code, ctx);
```

`Parse(string)` forwards to `Parse(string, SemanticContext?)` and uses `BasicSemanticContext` when omitted. You can swap in your own symbol table or diagnostic collection.

### Source positions (line/column)

`SourceSpan` carries accurate 1-based line/column. The lexer computes line/column while tokenizing; it propagates through `Token.Span` to AST node `Span`. Diagnostics report accurate positions.

### Example: MiniC semantic analysis

`samples/MiniC/SemanticAnalyzer.cs` derives from `ProgramListener` and performs scope management, symbol resolution, and type checking (int/bool). Run with `dotnet run --project samples/MiniC`.

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
| `[Pattern(@"regex")]` | ctor param | Lexical rule (regex). `Priority` sets lexer priority (higher wins). |
| `[Precedence(n)]` | class (operator node) | Operator precedence/associativity. Higher `n` binds tighter. `IsRightAssociative`/`IsNonAssociative`. |
| `[Skip(@"regex")]` | class (same as `[Grammar]`) | Skip pattern (whitespace, comments). |

### Special constructor parameter types

- **`Token` type** (with `[Pattern]`): terminal. Carries `Text` and `Span`.
- **`SemanticContext`-derived type**: not a RHS child; the parser injects the semantic context (determined by **type**, not attribute).

### `[Pattern]` / `[Precedence]` named properties

```csharp
[Pattern(@"[A-Za-z_]\w*", Priority = 0)]    // identifier (low priority)
[Pattern(@"if", Priority = 1)]               // keyword if (beats identifier)

[Precedence(1)]                              // priority 1, left-assoc (e.g. + -)
[Precedence(2)]                              // priority 2 (binds tighter than +; e.g. * /)
[Precedence(3, IsRightAssociative = true)]    // priority 3, right-assoc (tighter than *; e.g. power **)
[Precedence(1, IsNonAssociative = true)]      // priority 1, non-assoc (e.g. comparison < >; a<b<c is an error)
```

### Writing grammar

- **Inheritance tree = syntax**: `[Grammar] public abstract class Expr` is a nonterminal. `sealed class NumExpr : Expr` is the rule `Expr -> [0-9]+`.
- **Constructor params = RHS**: types and order express the RHS. `AddExpr(Expr left, [Pattern(@"\+")] Token op, Expr right)` is `Expr -> Expr + Expr`.
- **Multiple constructors = multiple rules**: each constructor in a class is an independent production.

## Samples

- **Calculator** (`src/AstFirst/Calc/`) — arithmetic with precedence.
- **MiniLang** (`src/AstFirst/MiniLang/`) — `let`/`print`/arithmetic.
- **JSON parser** (`samples/JsonParser/`) — JSON primitive types.
- **MiniC** (`samples/MiniC/`) — variables, assignment, `if`/`while`, blocks, bool. **Semantic analysis (Listener + type checking) demo**.
- **MiniBASIC** (`samples/MiniBasic/`) — line-numbered BASIC.

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
├── samples/                   net10.0         JsonParser / MiniC / MiniBasic
└── tests/                     net10.0         AstFirst.Tests (Core/Runtime + EndToEnd) / AstFirst.Generator.Tests
```

- The generator reads C# via Roslyn, converts it to an equality-comparable POCO model (the lifeline of caching), builds DFA/LALR tables via Core's pure logic, and emits Lexer/Parser/Listener C# code.
- Generated code depends on Runtime. Lexer/Parser embed DFA/LALR tables in `static readonly` arrays and drive shift/reduce. Listener is an abstract class with `Enter`/`Exit`/`Walk`.
- The generator Compile-Includes Core sources into a single assembly (avoids dependency loading issues for analyzers).

## Documentation

- [Architecture](docs/en/architecture.md)
- [Semantic analysis guide](docs/en/semantic-analysis.md)
- [Grammar reference](docs/en/grammar-reference.md)

Japanese versions are under `docs/ja/` and [README.md](README.md).

## Tests

213 tests (AstFirst.Tests 189 + Generator.Tests 24). Covers lexer/DFA/LALR stages, end-to-end, error recovery, semantic analysis (scopes, Listener, type checking, ctx -> ParseResult.Diagnostics integration), and positions (line/column).

## License

MIT (LICENSE.txt)
