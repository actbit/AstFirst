# Semantic analysis guide

English / [日本語](../ja/semantic-analysis.md)

AstFirst lets you attach semantic analysis on top of syntactic parsing (AST construction). Standard helpers: scoped symbol table, a generic Walker, attribute-based semantic rules, a type system, symbol resolution, type checking, binding, and diagnostics.

## Three ways to write semantic analysis

AstFirst offers three ways to author semantic logic (they compose):

| Approach | Timing | Where to write | Use case |
|---|---|---|---|
| `partial void OnReduce(ctx)` | Pass 1, at reduce (bottom-up) | Inside each node class | Node-local syntactic work (`Name = Tok.Text`, `Span` computation). ctx is read-only |
| `partial void OnAccepted(ctx)` | After reduce, route determined | Inside each node class | Post-determination work. ctx is writable (declarations/diagnostics allowed) |
| `[OnReduce]` / `[Enter]` / `[Exit]` attributes | Pass 1 (OnReduce) / Pass 2 (Enter/Exit) | Inside the `[Grammar]` root class ★ recommended | Grammar-wide semantics (declaration, resolution, type checks). The ctx cast is injected automatically |
| `IOnSecondPassEnter` / `IOnSecondPassExit` | Pass 2 (top-down) | Inside each node class (interface impl) | Backward compatible. Attribute style recommended |

The attribute style is the most concise — the boilerplate (ctx cast, forwarding call) is generated for you.

```csharp
[Grammar]
[Skip(@"\s+")]
public abstract partial class MyLang : AstNode
{
    [OnReduce]  // at reduce, bottom-up
    public static void Declare(Decl d, MyCtx ctx)
    {
        if (!ctx.Symbols.TryDeclare(d.Name, d.Span, null, out _))
            ctx.Diagnostics.Error($"'{d.Name}' is already declared", d.Span);
    }

    [Enter]     // pass 2, top-down enter
    public static void EnterBlock(Block b, MyCtx ctx) => ctx.Symbols.PushScope();

    [Exit]      // pass 2, exit
    public static void ExitBlock(Block b, MyCtx ctx) => ctx.Symbols.PopScope();
}

public sealed partial class Decl : MyLang
{
    public string Name { get; private set; } = "";
    [Rule] public static void DeclRule([Token("[A-Za-z]+")] Token name, MyCtx ctx) { }
    partial void OnReduce(MyCtx ctx) { Name = NameTok.Text; Span = NameTok.Span; }  // syntactic work stays in partial OnReduce
}
```

`[OnReduce]` and partial `OnReduce` **coexist** (partial `OnReduce` runs first, then `[OnReduce]`).

## OnAccepted — route-determined callback

`partial void OnAccepted(ctx)` is called when the interpretation of a node is **determined**.

### Timing

| Mode | When called |
|---|---|
| **LALR** (default) | Right after reduce (single stack = immediately determined = just after `OnReduce`) |
| **LightGlr** | When forked candidates converge to one (may be later than `OnReduce`) |

### Difference from OnReduce

| | `OnReduce` | `OnAccepted` |
|---|---|---|
| **ctx** | `SemanticContext` (read-only) | User's ctx type (writable) |
| **Declarations/diagnostics** | Not allowed | Allowed |
| **Timing** | At reduce (interpretation may be undetermined) | Route determined |
| **LALR** | Per reduce | Right after each reduce |
| **LightGlr** | Once per fork candidate | Only for the surviving candidate |

### Usage

```csharp
public sealed partial class MyDecl : MyLang
{
    public string Name { get; private set; } = "";
    [Rule] public static void DeclRule([Token("[A-Za-z]+")] Token name, MyCtx ctx) { }

    // At reduce: node-local initialization only (ctx is read-only)
    partial void OnReduce(SemanticContext ctx)
    {
        Name = NameTok.Text;
        Span = NameTok.Span;
    }

    // Route determined: ctx writes are allowed
    partial void OnAccepted(MyCtx ctx)
    {
        // Declare symbol only after this interpretation is confirmed
        ctx.WritableSymbols.TryDeclare(Name, Span, null, out _);
    }
}
```

### When to use OnAccepted

- **OnReduce is enough** (most cases): setting `Name`, `Value`, computing `Span` — node-local work.
- **OnAccepted is useful**: when you want declarations/diagnostics only after forked candidates converge in LightGlr mode. In LALR mode, it runs right after `OnReduce`, so `[Enter]`/`[Exit]` (2nd-pass Walker) is generally more appropriate.

## SemanticContext injection

Declare a `SemanticContext`-derived parameter on a `[Rule]` and the generator injects `ctx` from the parser (no attribute needed — decided by **type**). `ctx.Symbols` (`ScopedSymbolTable`) and `ctx.Diagnostics` (`DiagnosticBag`) are available. `BasicSemanticContext` also provides `Types` (`TypeContext`) by default.

```csharp
public sealed class MyCtx : BasicSemanticContext { /* add your own state */ }
```

## Scoped symbol table

`ScopedSymbolTable` is a stack of lexical scopes.

- `PushScope(key, kind)` / `PopScope(key)` — open/close a scope (key verifies the pairing)
- `Lookup(name)` — from the current scope outward (`null` if undeclared)
- `TryDeclare(name, span, value, out existing)` — rejects duplicates in the same scope; outer same-name (shadowing) is allowed
- `ResolveOrError(name, span, bag)` — resolves; on undeclared, adds an Error to `bag` and returns `null`

A typed symbol can be stored in `Value` (`object?`):

```csharp
var varSym = new VariableSymbol("x", span, depth, Int);
ctx.Symbols.TryDeclare("x", span, varSym, out _);
var entry = ctx.Symbols.Lookup("x");
var sym = entry?.AsVariable();  // VariableSymbol?
```

## Type system

`TypeSymbol` represents a type. It is not `sealed` (inheritable); `FunctionTypeSymbol` and `ArrayTypeSymbol` are built in. `IsAssignableFrom` is `virtual` (derived types override it with structural rules).

```csharp
var Int = new TypeSymbol("int");
var Bool = new TypeSymbol("bool");
var fnType = new FunctionTypeSymbol(Int, new[] { Int });   // (int) => int
var arrType = new ArrayTypeSymbol(Int);                    // int[]

Int.IsAssignableFrom(Int);                                  // true
fnType.IsAssignableFrom(new FunctionTypeSymbol(Int, new[] { Int }));  // true (structural)
```

- `TypeContext` — node → type map (`SetType` / `TypeOf` / `HasType`). Available as `BasicSemanticContext.Types`.
- `ClassifyConversion(to)` / `IsImplicitlyConvertible(to)` — implicit conversions (derived→base, widening).
- `OverloadResolver.Resolve(candidates, argTypes, bag, span, name)` — function overload resolution (exact match → implicit conversion → absent/ambiguous).

A symbol hierarchy is provided via `ISymbol` / `VariableSymbol` / `FunctionSymbol` / `FunctionParam` (stored in `SymbolEntry.Value`).

## Generic Walker (generated)

Each `[Grammar]` gets a `{Root}Walker` (`public abstract class`). It drives `Enter → children → Exit` with an iterative stack, invoking per-concrete-node `EnterXxx` / `ExitXxx` (virtual, overridable) along with `IOnSecondPassEnter` / `Exit` and `[Enter]` / `[Exit]` attribute methods.

```csharp
// Reuse for any AST traversal (code generation, not just semantic analysis)
public sealed class CountWalker : ProgramWalker
{
    public int Nodes;
    protected override void EnterEach(AstNode node, SemanticContext ctx) { Nodes++; base.EnterEach(node, ctx); }
}
```

`Parser.Parse()` auto-drives a default instance (`_Default`) at the end. If no semantic hook exists, the Walker is not generated at all (zero-cost).

## Binding

`AstNode.SetAnnotation` / `GetAnnotation<T>` attach resolved results (symbols, types) to a node for later phases (e.g. code generation).

```csharp
node.SetAnnotation("symbol", resolved);
var sym = node.GetAnnotation<SymbolEntry>("symbol");
```

## One pass vs two passes

LALR reduce is **bottom-up**. A parent's `OnReduce` runs **after** its children, so block-scope Push/Pop cannot be accurate at reduce time.

- **One pass (`OnReduce` / `[OnReduce]`)**: works for declaration-order visibility and duplicate-declaration detection, but **block scope is inaccurate**.
- **Two passes (`[Enter]` / `[Exit]`, Walker) ★ recommended**: after `Parse`, the Walker runs `Enter → children → Exit` and manages block scope precisely with `PushScope` / `PopScope`.

## Diagnostics

Semantic diagnostics come out via `ParseResult.Diagnostics`. Anything added to `ctx.Diagnostics` flows into `ParseResult`.

```csharp
var result = MyParser.Parse(code);
foreach (var d in result.Diagnostics) Console.WriteLine(d);
result.HasErrors  // syntactic errors OR semantic Severity.Error
```

## Example: MiniC

`samples/MiniC/` is a reference implementation using the `[Enter]` / `[Exit]` attribute style. Semantic rules are aggregated in the root `Program` class:

- `[Enter] EnterBlock` / `[Exit] ExitBlock` — scope Push/Pop
- `[Enter] EnterDecl` / `EnterDeclInit` — `TryDeclare` (duplicate detection)
- `[Enter] EnterVar` / `EnterAssign` — `ResolveOrError` (undeclared detection) + binding to annotations
- `[Exit] ExitNum` / `ExitAdd`, etc. — expression type propagation
- `[Exit] ExitIf` / `ExitWhile` — condition type check (bool required)

```
dotnet run --project samples/MiniC
```

## See also

- [Architecture](architecture.md)
- [Grammar reference](grammar-reference.md)
