# Architecture

English / [日本語](../ja/architecture.md)

AstFirst is a parser generator built as three layers plus a generator.

## Layers

- **AstFirst.Core** (`netstandard2.0`): pure logic. Builds/minimizes the lexer DFA and constructs the LALR(1) table. No Roslyn dependency.
- **AstFirst.Runtime** (`netstandard2.0`): attributes, base classes, and semantic-analysis helpers users touch directly. `[Pattern]` / `[Precedence]` / `[Enter]` / `[Exit]` / `[OnReduce]` / `AstNode` / `Token` / `SourceSpan` / `ScopedSymbolTable` / `TypeSymbol` / `FunctionTypeSymbol` / `ArrayTypeSymbol` / `ISymbol` / `SemanticContext`, etc.
- **AstFirst.Generator** (`netstandard2.0`): `IIncrementalGenerator`. Includes Core sources into a single assembly. Emits Lexer / Parser / Walker / per-node partials at compile time.
- **AstFirst** (`net10.0`): user code (calculator / MiniLang samples).

## Generator pipeline

1. **Extraction** (`ModelExtraction`): traverses AstNode/Token derivatives and `[Pattern]` from the `[Grammar]` root, collects `[OnReduce]`/`[Enter]`/`[Exit]` attribute semantic rules, and converts them into an equality-comparable POCO model (`GrammarModel`/`AnalyzeRuleModel`).
2. **DFA build** (`ModelToDfa`): regex of each rule -> NFA (Thompson) -> DFA (subset construction) -> minimization (Hopcroft).
3. **LALR table** (`ModelToTable`): LR(0) automaton -> FIRST/NULLABLE -> DeRemer-Pennello lookahead propagation -> ACTION/GOTO tables + conflict detection.
4. **Code emission** (`CodeEmitter` / `ParserEmitter` / `WalkerEmitter`): generates C# for Lexer (DFA arrays), Parser (LALR table + shift/reduce driver), Walker (Enter/Exit/Walk + `[Enter]`/`[Exit]` dispatch), and per-node partials (including `[OnReduce]` dispatch).

## Generated code shape

- **Lexer**: embeds the DFA transition table and accepting rules in `static readonly` arrays; `Tokenize()` runs longest-match + priority-driven. Also computes each token's 1-based line/column.
- **Parser**: embeds ACTION/GOTO tables and Productions in arrays and drives shift/reduce/accept. At reduce it calls the AST class constructor to build the AST (`[OnReduce]` attribute methods are called right after partial `OnReduce`). Includes panic-mode error recovery.
- **Walker**: `EnterXxx` / `ExitXxx` (virtual, empty) per concrete node + `Walk` (iterative stack: Enter -> children -> Exit). Also invokes `IOnSecondPassEnter`/`Exit` and `[Enter]`/`[Exit]` attribute methods. Children are collected from each node's public properties of AstNode-derived types. If a grammar has no semantic hook at all, the Walker is not emitted (zero-cost).

## Caching strategy

`IIncrementalGenerator` caches outputs as long as inputs are unchanged. Therefore `GrammarModel` and its members (`NodeModel` / `RuleModel` / `ParamModel` / `TokenDefModel` / `ChildModel` / `AnalyzeRuleModel`) implement `IEquatable` with field-by-field equality. Lowering Roslyn symbols to string/int POCOs keeps the cache efficient.

The generator Compile-Includes Core sources into a single assembly, to avoid dependency-assembly loading issues when running as an analyzer.

## See also

- [Semantic analysis guide](semantic-analysis.md)
- [Grammar reference](grammar-reference.md)
