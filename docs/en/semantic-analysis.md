# Semantic analysis guide

English / [日本語](../ja/semantic-analysis.md)

> **Note: this document still describes the old (constructor / Listener-based) model.** For the current `[Rule]` static model (`OnReduce` / `OnSecondPass` / `Accept`/`Reject` / partial child retention), see [README](../../README.en.md). A full update of this document is planned for a follow-up PR.

AstFirst lets you layer semantic analysis on top of parsing (AST construction). Standard helpers: scoped symbol table, Listener, symbol resolution, type checking, binding, diagnostics.

## SemanticContext injection

Declare a `SemanticContext`-derived parameter in a constructor and the generator injects `ctx` from the parser (no attribute needed; determined by **type**). Use `ctx.Symbols` (`ScopedSymbolTable`) and `ctx.Diagnostics` (`DiagnosticBag`).

```csharp
public sealed class DeclStmt : Stmt
{
    public DeclStmt(... Token name, SemanticContext ctx, ...)
    {
        if (!ctx.Symbols.TryDeclare(name.Text, name.Span, null, out _))
            ctx.Diagnostics.Error($"duplicate '{name.Text}'", name.Span);
    }
}
```

## Scoped symbol table

`ScopedSymbolTable` is a stack of lexical scopes.

- `PushScope()` / `PopScope()` — open/close a scope
- `Lookup(name)` — from current scope outward (null if undeclared)
- `TryDeclare(name, span, value, out existing)` — rejects same-scope duplicates, allows shadowing of outer
- `ResolveOrError(name, span, bag)` — resolve, or add an Error to `bag` and return null

## Listener (generator-emitted)

Each `[Grammar]` gets an `XxxListener` with `EnterXxx` / `ExitXxx` per concrete node and `Walk` (Enter -> recurse children -> Exit). Derive and override.

```csharp
public sealed class SemanticAnalyzer : ProgramListener
{
    public override void EnterBlockStmt(BlockStmt node) => _symbols.PushScope();
    public override void ExitBlockStmt(BlockStmt node) => _symbols.PopScope();
    public override void ExitNumExpr(NumExpr node) => _types.SetType(node, Int);
    public IReadOnlyList<Diagnostic> Analyze(Program p) { Walk(p); return _bag.Items; }
}
```

`Walk` runs in Enter -> recurse children -> Exit order. Expression type propagation (Exit) -> condition type check (Exit) line up correctly.

## Type checking

`TypeSymbol` (type representation, `IsAssignableFrom` for assignability) and `TypeContext` (node -> type). A language-agnostic skeleton; you define concrete types.

```csharp
var Int = new TypeSymbol("int");
var Bool = new TypeSymbol("bool");
// Propagate expression type on Exit
_types.SetType(node, Int);
// Check a condition's type
if (_types.TypeOf(cond) is { } t && !Bool.IsAssignableFrom(t))
    _bag.Error("if condition must be bool", cond.Span);
```

## Binding

`AstNode.SetAnnotation` / `GetAnnotation<T>` attach a resolved result (symbol/type) to a node, for later phases (e.g. codegen) to read.

```csharp
node.SetAnnotation("symbol", resolved);
var sym = node.GetAnnotation<SymbolEntry>("symbol");
```

## One-pass vs two-pass

LALR reduction is **bottom-up**. A parent node's constructor runs **after** its children, so block-scope Push/Pop cannot be done accurately in a constructor.

- **One-pass (in constructor)**: works for declaration-order visibility and duplicate-declaration detection, but **block scopes are inaccurate**.
- **Two-pass (Listener walk) ★ recommended**: after `Parse`, call `Walk` and manage block scopes with `PushScope` / `PopScope`.

## Diagnostic integration

Semantic diagnostics are available via `ParseResult.Diagnostics`. Whatever you add to `ctx.Diagnostics` in a constructor, or collect during a Listener walk, flows through `ctx.Diagnostics.Items` into `ParseResult`.

```csharp
var result = ProgramParser.Parse(code);
foreach (var d in result.Diagnostics) Console.WriteLine(d);
```

## Example: MiniC

`samples/MiniC/SemanticAnalyzer.cs` derives from `ProgramListener` and:

- `EnterBlockStmt` / `ExitBlockStmt` — Push/Pop scope
- `EnterDeclStmt` — `TryDeclare` (duplicate-declaration detection)
- `EnterVarExpr` / `EnterAssignStmt` — `ResolveOrError` (undeclared detection) + bind via annotations
- `ExitXxx` (literals, arithmetic) — expression type propagation
- `ExitIfStmt` / `ExitWhileStmt` — condition type check (must be bool)

```
dotnet run --project samples/MiniC
```

## See also

- [Architecture](architecture.md)
- [Grammar reference](grammar-reference.md)
