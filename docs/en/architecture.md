# Architecture

English / [日本語](../ja/architecture.md)

AstFirst is a parser generator built as three layers plus a generator.

## Layers

- **AstFirst.Core** (`netstandard2.0`): pure logic. Builds/minimizes the lexer DFA and constructs the LALR(1) table. No Roslyn dependency.
- **AstFirst.Runtime** (`net10.0`): attributes, base classes, and semantic-analysis helpers users touch directly. `[Pattern]` / `[Precedence]` / `AstNode` / `Token` / `SourceSpan` / `ScopedSymbolTable` / `TypeSymbol` / `SemanticContext`, etc.
- **AstFirst.Generator** (`netstandard2.0`): `IIncrementalGenerator`. Includes Core sources into a single assembly. Emits Lexer / Parser / Listener at compile time.
- **AstFirst** (`net10.0`): user code (calculator / MiniLang samples).

## Generator pipeline

1. **Extraction** (`ModelExtraction`): traverses AstNode/Token derivatives and `[Pattern]` from the `[Grammar]` root, converting them into an equality-comparable POCO model (`GrammarModel`).
2. **DFA build** (`ModelToDfa`): regex of each rule -> NFA (Thompson) -> DFA (subset construction) -> minimization (Hopcroft).
3. **LALR table** (`ModelToTable`): LR(0) automaton -> FIRST/NULLABLE -> DeRemer-Pennello lookahead propagation -> ACTION/GOTO tables + conflict detection.
4. **Code emission** (`CodeEmitter` / `ParserEmitter` / `ListenerEmitter`): generates C# for Lexer (DFA arrays), Parser (LALR table + shift/reduce driver), and Listener (Enter/Exit/Walk).

## Generated code shape

- **Lexer**: embeds the DFA transition table and accepting rules in `static readonly` arrays; `Tokenize()` runs longest-match + priority-driven. Also computes each token's 1-based line/column.
- **Parser**: embeds ACTION/GOTO tables and Productions in arrays and drives shift/reduce/accept. At reduce it calls the AST class constructor to build the AST. Includes panic-mode error recovery.
- **Listener**: `EnterXxx` / `ExitXxx` (virtual, empty) per concrete node + `Walk` (Enter -> recurse children -> Exit). Children are collected from each node's public properties of AstNode-derived types.

## Caching strategy

`IIncrementalGenerator` caches outputs as long as inputs are unchanged. Therefore `GrammarModel` and its members (`NodeModel` / `CtorModel` / `ParamModel` / `TokenDefModel` / `ChildModel`) implement `IEquatable` with field-by-field equality. Lowering Roslyn symbols to string/int POCOs keeps the cache efficient.

The generator Compile-Includes Core sources into a single assembly, to avoid dependency-assembly loading issues when running as an analyzer.

## See also

- [Semantic analysis guide](semantic-analysis.md)
- [Grammar reference](grammar-reference.md)
