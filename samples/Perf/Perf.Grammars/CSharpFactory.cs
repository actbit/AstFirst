using AstFirst.Core.Parsing;

namespace Perf.Grammars;

/// <summary>
/// C# 完全文法 (ECMA-334 Annex A) を AstFirst (LALR(1)) で実装。意味解析なし (空コンストラクタ)。
/// </summary>
public static class CSharpFactory
{
    public const string Namespace = "CSharpParser";
    public const string Root = "CSharpCompilationUnit";

    private const int PrecAssignment = 1;
    private const int PrecLambda = 1;
    private const int PrecConditional = 2;
    private const int PrecNullCoalescing = 3;
    private const int PrecConditionalOr = 4;
    private const int PrecConditionalAnd = 5;
    private const int PrecLogicalOr = 6;
    private const int PrecLogicalXor = 7;
    private const int PrecLogicalAnd = 8;
    private const int PrecEquality = 9;
    private const int PrecRelational = 10;
    private const int PrecShift = 11;
    private const int PrecAdditive = 12;
    private const int PrecMultiplicative = 13;
    private const int PrecUnary = 14;
    private const int PrecPostfix = 15;

    public static GrammarSpec Create()
    {
        var spec = new GrammarSpec(Namespace, Root, skipRegex: @"(\s|//[^\n]*)+");
        spec.ParseMode = "LightGlr";   // C# の型/式 (cast/paren, generic) の本質的曖昧性を GLR で解決
        spec.AddAbstract(Root, "AstNode");
        AddLexical(spec);
        AddTypes(spec);
        AddExpressions(spec);
        AddPatterns(spec);
        AddStatements(spec);
        AddDeclarations(spec);
        AddMembers(spec);
        AddAttributes(spec);
        AddPreProcessor(spec);
        AddTopLevelList(spec);
        return spec;
    }

    private static void AddTopLevelList(GrammarSpec spec)
    {
        spec.AddSealed("NilCompilationUnit", Root).Ctor();
        spec.AddSealed("ConsDeclaration", Root).Ctor(
            new ParamSpec("Declaration", "head"),
            new ParamSpec(Root, "rest"));
    }

    private static void AddQualifiedName(GrammarSpec spec)
    {
        spec.AddAbstract("QualifiedName", "AstNode");
        spec.AddSealed("SimpleQualifiedName", "QualifiedName").Ctor(new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0));
        spec.AddSealed("QualifiedQualifiedName", "QualifiedName").Ctor(
            new ParamSpec("QualifiedName", "left"),
            new ParamSpec("Token", "dot", @"\."),
            new ParamSpec("Token", "right", @"[A-Za-z_]\w*", priority: 0));
    }

    private static void AddModifierList(GrammarSpec spec)
    {
        spec.AddAbstract("ModifierList", "AstNode");
        spec.AddSealed("NilModifierList", "ModifierList").Ctor();
        spec.AddSealed("ConsModifier", "ModifierList").Ctor(
            new ParamSpec("Token", "modifier", @"(public|private|protected|internal|static|readonly|abstract|sealed|const|async|virtual|override|extern|partial|unsafe|volatile)", priority: 1),
            new ParamSpec("ModifierList", "rest"));
    }

    private static void AddParameterList(GrammarSpec spec)
    {
        spec.AddAbstract("ParameterList", "AstNode");
        spec.AddSealed("NilParameterList", "ParameterList").Ctor();
        spec.AddSealed("SingleParameter", "ParameterList").Ctor(
            new ParamSpec("Type", "type"),
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0));
        spec.AddSealed("ConsParameter", "ParameterList").Ctor(
            new ParamSpec("ParameterList", "list"),
            new ParamSpec("Token", "comma", @","),
            new ParamSpec("Type", "type"),
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0));
    }

    // === 字例 (文字列リテラル拡張: 共通セクション) ===
    // primary 系リテラル (precedence 属性不要 = 最高)。全具象を Expression に直接ぶら下げる。
    // これらは AddExpressions で定義済みの StringLiteral と排他になるよう、開始シーケンスで弁別する
    // (@" / $" / """ は通常の " と異なるため、DFA 最長一致で reduce-reduce を起こさない)。
    private static void AddLexical(GrammarSpec spec)
    {
        // 文字列リテラル拡張 (Verbatim/Interpolated/Raw) は AddExpressions で定義（重複回避）。
    }

    // === 型 (1段階: 全具象が Type 直接) ===
    private static void AddTypes(GrammarSpec spec)
    {
        spec.AddAbstract("Type", "AstNode");

        // 定義済み型 (キーワード、識別子より高優先)。
        string[] predefined = { "int", "string", "bool", "double", "float", "char",
            "byte", "sbyte", "short", "ushort", "uint", "ulong", "long", "decimal",
            "object", "void", "var", "dynamic", "nint", "nuint" };
        foreach (var kw in predefined)
            spec.AddSealed(Cap(kw) + "Type", "Type").Ctor(new ParamSpec("Token", "kw", kw, priority: 1));

        // 名前付き型 (identifier)。
        spec.AddSealed("NamedType", "Type").Ctor(new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0));

        // 配列型: Type [ ]
        spec.AddSealed("ArrayType", "Type").Ctor(
            new ParamSpec("Type", "element"),
            new ParamSpec("Token", "lb", @"\["),
            new ParamSpec("Token", "rb", @"\]"));

        // ジェネリック型 (技術1): Name < TypeArgumentList >。Member の型 (フィールド/メソッド/継承) でのみ使用。
        // ローカル宣言は var のみ (下記) なので、Statement 文脈に < は現れず、式の比較 < と衝突しない。
        // (ネスト generic A<B<C>> の >> はレクサ最長一致で右シフトと衝突するため、入力では1段階 generic を想定。)
        spec.AddSealed("GenericType", "Type", precedence: PrecPostfix).Ctor(
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
            new ParamSpec("Token", "lt", @"<"),
            new ParamSpec("TypeArgumentList", "args"),
            new ParamSpec("Token", "gt", @">"));

        // 型引数リスト (カンマ区切)。GenericType 専用。
        spec.AddAbstract("TypeArgumentList", "AstNode");
        spec.AddSealed("SingleTypeArg", "TypeArgumentList").Ctor(new ParamSpec("Type", "type"));
        spec.AddSealed("ConsTypeArg", "TypeArgumentList").Ctor(
            new ParamSpec("TypeArgumentList", "list"),
            new ParamSpec("Token", "comma", @","),
            new ParamSpec("Type", "type"));

        // null 許容型: Type ? 。[Precedence(PrecPostfix)] で ? が最後の終端の shift-reduce を解決。
        // 型位置のみの規則 (式の三項 ? : は Expression 文脈なので衝突しない)。
        spec.AddSealed("NullableType", "Type", precedence: PrecPostfix).Ctor(
            new ParamSpec("Type", "element"),
            new ParamSpec("Token", "question", @"\?"));

        // ポインタ型: Type * (unsafe)。[Precedence(PrecPostfix)]。* は乗算と衝突するが型位置のみ。
        spec.AddSealed("PointerType", "Type", precedence: PrecPostfix).Ctor(
            new ParamSpec("Type", "element"),
            new ParamSpec("Token", "star", @"\*"));

        // 関数ポインタ型: delegate* < FpTypeList > 。delegate* を専用終端 (priority:1) にして曖昧さ排除。
        // < > は型引数区切り (Generic/比較の < とは先頭 delegate* で識別)。
        // TypeArgumentList (Generic 用) の base を変えず、独立の FpTypeList を新設して安全に分離。
        spec.AddSealed("FunctionPointerType", "Type").Ctor(
            new ParamSpec("Token", "delegatePtr", @"delegate\*", priority: 1),
            new ParamSpec("Token", "lt", @"<"),
            new ParamSpec("FpTypeList", "args"),
            new ParamSpec("Token", "gt", @">"));
        // 関数ポインタ用 型リスト (カンマ区切。TypeArgumentList とは別物)。
        spec.AddAbstract("FpTypeList", "AstNode");
        spec.AddSealed("SingleFpType", "FpTypeList").Ctor(new ParamSpec("Type", "type"));
        spec.AddSealed("ConsFpType", "FpTypeList").Ctor(
            new ParamSpec("FpTypeList", "list"),
            new ParamSpec("Token", "comma", @","),
            new ParamSpec("Type", "type"));

        // タプル型: ( Type , Type (, Type)* ) 。最低 2 要素。
        // lp Type comma TupleElementList rp。TupleElementList は「第2要素以降」(Single=1個/Cons=追加)。
        spec.AddSealed("TupleType", "Type").Ctor(
            new ParamSpec("Token", "lp", @"\("),
            new ParamSpec("Type", "first"),
            new ParamSpec("Token", "comma", @","),
            new ParamSpec("TupleTypeList", "rest"),
            new ParamSpec("Token", "rp", @"\)"));
        // タプル型要素リスト (第2要素以降、型用。式の TupleElementList と区別)。
        spec.AddAbstract("TupleTypeList", "AstNode");
        spec.AddSealed("SingleTupleElement", "TupleTypeList").Ctor(new ParamSpec("Type", "type"));
        spec.AddSealed("ConsTupleElement", "TupleTypeList").Ctor(
            new ParamSpec("TupleTypeList", "list"),
            new ParamSpec("Token", "comma", @","),
            new ParamSpec("Type", "type"));

        // ref 型: ref Type 。ref は priority:1 (識別子より高)。
        // [Precedence(PrecPostfix)] で ref 規則の優先度を 15 に設定。
        //   これにより ref Type の直後に ? / * (NullableType/PointerType の最後の終端) が続く
        //   shift-reduce は解決する (同優先度 → トークンの左結合で reduce。C# では配列/Nullable/
        //   ポインタの要素型に ref は許されないので ref Type をアトミックに還元するのが正しい)。
        //   注意: [ (配列開始) との競合は残る。AstFirst の precedence 伝播は「規則の最後の終端」
        //   のみに設定され、配列規則の最後の終端は ] であり [ ではないため、[ トークン自体が
        //   precedence を持てず precedence 解決の対象にならない。この [ 競合 (state 102/139) は
        //   benign (デフォルト reduce) として残す。完全解消には要素型の CoreType 分離
        //   (ref 型を要素型位置から除外) が必要だが全セクション横断の改修になるため本セクションでは見送る。
        spec.AddSealed("RefType", "Type", precedence: PrecPostfix).Ctor(
            new ParamSpec("Token", "refKw", "ref", priority: 1),
            new ParamSpec("Type", "element"));
        // ref readonly 型: ref readonly Type 。readonly も priority:1。同上の PrecPostfix 付与。
        spec.AddSealed("RefReadOnlyType", "Type", precedence: PrecPostfix).Ctor(
            new ParamSpec("Token", "refKw", "ref", priority: 1),
            new ParamSpec("Token", "readonlyKw", "readonly", priority: 1),
            new ParamSpec("Type", "element"));

        // 多次元配列型: Type [ , ] / Type [ , , ] / ... (ランク >= 2)。
        // RankCommaList はカンマ 1 個以上 (ランク2以上) なので、ArrayType (Type [ ] カンマ0) と衝突しない。
        // [Precedence(PrecPostfix)]: Type の後に続く後置構築で shift-reduce を安定化。
        spec.AddSealed("MultiDimensionalArrayType", "Type", precedence: PrecPostfix).Ctor(
            new ParamSpec("Type", "element"),
            new ParamSpec("Token", "lb", @"\["),
            new ParamSpec("RankCommaList", "commas"),
            new ParamSpec("Token", "rb", @"\]"));
        // ランク指定カンマリスト (1 個以上)。Single がカンマ1個 (ランク2)、Cons が追加カンマ。
        spec.AddAbstract("RankCommaList", "AstNode");
        spec.AddSealed("SingleRankComma", "RankCommaList").Ctor(new ParamSpec("Token", "comma", @","));
        spec.AddSealed("ConsRankComma", "RankCommaList").Ctor(
            new ParamSpec("RankCommaList", "list"),
            new ParamSpec("Token", "comma", @","));
    }

private static void AddExpressions(GrammarSpec spec)
{
    // =============================================================================
    // 式セクション（C# BNF 相当）。全具象クラスは base = Expression（平坦クラス階層）。
    // DeepPrec パターン: 優先度は [Precedence(n)] で表現し、クラス階層では階層化しない。
    //
    // コンフリクト回避方針:
    //  - 補助リスト/構造（base=AstNode）は Expression の subtype ではない → 1段階抽象制約 OK。
    //  - reduce-reduce を出さないよう、同一位置の代替はコンストラクタ分割で分離。
    //  - postfix / 三項 / 前置 / キャストなど「演算子が最後の終端でない」規則には [Precedence] 付与。
    //  - LINQ キーワード・演算子キーワードは priority:1（識別子 priority:0 に勝つ）。
    //  - 複数文字演算子（?., ?[, =>, ++, --, == 等）は長い順で自動的に最長一致弁別。
    //  - キャスト (Type)expr と括弧 (expr) は CastExpr に [Precedence(PrecUnary)] を付けて
    //    shift-reduce を precedence で解決（LALR 爆発は許容・完全性優先）。
    //  - リストは head + tail 分離 (Statement/Pattern セクションと同構造) に統一。
    //      List = Single(head, Tail),  Tail = Nil | Cons(comma, head, Tail)
    //    これにより「空 (Nil) の還元」path は Tail の Nil 1 箇所に局所化され、head を還元した
    //    直後の状態で次トークン (, / ]) を見て shift/reduce が一意に決まる。旧 Nil + Single(head)
    //    + Cons(comma,head,rest) では Nil と Single の還元 path が同じ先頭状態で重複して
    //    reduce-reduce を起こしていた (InitializerList / LambdaParameterList)。
    //  - [ の直後のリストは呼び出し元ごとに別非終元に分離。
    //      ElementAccess (インデックス) は ArgumentList、CollectionExpr は CollectionElementList。
    //    同じ [ で始まる両者の empty 還元が同一状態にマージして reduce-reduce するのを防ぐ。
    //    { } 内の初期化子は引き続き InitializerList を使う (角括弧とは衝突しない)。
    //  - レンジ演算子 (..) は「両オペランド必須の二項」のみ。片側省略 (a.. / ..b) と
    //    「.. 単独」は二項との区別が LALR(1) でつかず大量の RR/SR を出すため表現しない
    //    (Roslyn はマルチトークン先読み+意味情報で解決)。これで .. 起因の RR/SR を撲滅。
    // =============================================================================

    // 式のルート抽象。
    spec.AddAbstract("Expression", "AstNode");

    const int PrecAssignment = 1;
    const int PrecLambda = 1;
    const int PrecConditional = 2;
    const int PrecNullCoalescing = 3;
    const int PrecConditionalOr = 4;
    const int PrecConditionalAnd = 5;
    const int PrecRelational = 10;
    const int PrecUnary = 14;
    const int PrecPostfix = 15;

    // ---------------------------------------------------------------------------
    // 補助抽象・リスト（base=AstNode）。Expression の subtype ではない。
    // 全リストを head + tail 分離構造 (Statement/Pattern セクションと同様) で定義する。
    //   List  = Single(head, Tail)
    //   Tail  = Nil | Cons(comma, head, Tail)
    // 空リスト (0 要素) を許す位置では List に空 (NilXxxList) 具象を兄弟置きするが、「空の還元」
    // path は各リストの Nil 1 箇所に局所化され、複数リスト間の reduce-reduce を防ぐ。
    // ---------------------------------------------------------------------------

    // --- 引数リスト: ( e ) / ( e, e, ... ) / ( )。ElementAccess の [ args ] でも使用 ---
    // head + tail 分離。空 () は NilArgumentList。要素 1 個以上は SingleArgument + ArgumentTail。
    spec.AddAbstract("ArgumentList", "AstNode");
    spec.AddSealed("NilArgumentList", "ArgumentList").Ctor();
    spec.AddSealed("SingleArgument", "ArgumentList").Ctor(
        new ParamSpec("Expression", "head"),
        new ParamSpec("ArgumentTail", "tail"));
    // 引数のカンマ区切り続き (空可)。空 Nil が「続きなし」= 末尾。
    spec.AddAbstract("ArgumentTail", "AstNode");
    spec.AddSealed("NilArgumentTail", "ArgumentTail").Ctor();
    spec.AddSealed("ConsArgumentTail", "ArgumentTail").Ctor(
        new ParamSpec("Token", "comma", @","),
        new ParamSpec("Expression", "head"),
        new ParamSpec("ArgumentTail", "tail"));

    // --- 基本 primary（ワークフロー生成時に「既存再利用」と省略されたものを補完）---
    spec.AddSealed("IntegerLiteral", "Expression").Ctor(new ParamSpec("Token", "value", @"[0-9]+"));
    spec.AddSealed("RealLiteral", "Expression").Ctor(new ParamSpec("Token", "value", @"[0-9]+\.[0-9]+"));
    spec.AddSealed("StringLiteral", "Expression").Ctor(new ParamSpec("Token", "value", @"""([^""\\]|\\.)*"""));
    spec.AddSealed("CharLiteral", "Expression").Ctor(new ParamSpec("Token", "value", @"'([^'\\]|\\.)'"));
    spec.AddSealed("TrueLiteral", "Expression").Ctor(new ParamSpec("Token", "kw", "true", priority: 1));
    spec.AddSealed("FalseLiteral", "Expression").Ctor(new ParamSpec("Token", "kw", "false", priority: 1));
    spec.AddSealed("NullLiteral", "Expression").Ctor(new ParamSpec("Token", "kw", "null", priority: 1));
    spec.AddSealed("IdentifierExpr", "Expression").Ctor(new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0));
    spec.AddSealed("ThisExpr", "Expression").Ctor(new ParamSpec("Token", "kw", "this", priority: 1));
    // BaseExpr はワークフロー生成版（後述）で定義済みのためここでは追加しない（重複回避）。
    spec.AddSealed("ParenExpr", "Expression").Ctor(
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Expression", "inner"),
        new ParamSpec("Token", "rp", @"\)"));
    spec.AddSealed("MemberAccess", "Expression", precedence: PrecPostfix).Ctor(
        new ParamSpec("Expression", "target"),
        new ParamSpec("Token", "dot", @"\."),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0));
    spec.AddSealed("Invocation", "Expression", precedence: PrecPostfix).Ctor(
        new ParamSpec("Expression", "target"),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("ArgumentList", "args"),
        new ParamSpec("Token", "rp", @"\)"));
    spec.AddSealed("PostIncrement", "Expression", precedence: PrecPostfix).Ctor(
        new ParamSpec("Expression", "operand"),
        new ParamSpec("Token", "op", @"\+\+"));
    spec.AddSealed("PostDecrement", "Expression", precedence: PrecPostfix).Ctor(
        new ParamSpec("Expression", "operand"),
        new ParamSpec("Token", "op", @"--"));
    // ※ generic メソッド参照 Foo<T> (式位置) は表現しない。
    //   式位置の < は比較 (a < b) のみとし、generic の < と衝突させない (memory 指針:
    //   「generic は Member の型のみ」)。Foo<T>(x) のような generic メソッド呼び出しは
    //   制限 (Member の型位置の generic GenericType のみ対応)。

    // --- タプル要素リスト: ( e , e , e ) の「第2要素以降」(カンマ区切) ---
    // TupleExpr が ( first , rest ) で受けるため、rest は「第2要素以降」= 1 個以上。
    // head + tail 分離 (最低 1 要素なので List 側に Nil は持たない)。
    spec.AddAbstract("TupleElementList", "AstNode");
    spec.AddSealed("SingleTupleElementList", "TupleElementList").Ctor(
        new ParamSpec("Expression", "head"),
        new ParamSpec("TupleElementTail", "tail"));
    spec.AddAbstract("TupleElementTail", "AstNode");
    spec.AddSealed("NilTupleElementTail", "TupleElementTail").Ctor();
    spec.AddSealed("ConsTupleElementTail", "TupleElementTail").Ctor(
        new ParamSpec("Token", "sep", @","),
        new ParamSpec("Expression", "head"),
        new ParamSpec("TupleElementTail", "tail"));

    // --- 初期化子リスト: { e, e, ... } / { name = e, ... } ---
    // 要素は Expression か メンバ代入(name = expr)。reduce-reduce を避けるため 1 種類に統一:
    // 各要素を「Expression」とし、{ name = expr } は Expression 位置の代入式(AssignExpr 既存)で表現。
    // head + tail 分離。空 {} は NilInitializerList。{ } 専用なので角括弧 [ ] のリストとは衝突しない。
    spec.AddAbstract("InitializerList", "AstNode");
    spec.AddSealed("NilInitializerList", "InitializerList").Ctor();
    spec.AddSealed("SingleInitializerList", "InitializerList").Ctor(
        new ParamSpec("Expression", "head"),
        new ParamSpec("InitializerTail", "tail"));
    spec.AddAbstract("InitializerTail", "AstNode");
    spec.AddSealed("NilInitializerTail", "InitializerTail").Ctor();
    spec.AddSealed("ConsInitializerTail", "InitializerTail").Ctor(
        new ParamSpec("Token", "sep", ","),
        new ParamSpec("Expression", "head"),
        new ParamSpec("InitializerTail", "tail"));

    // --- コレクション要素リスト: [ e, e, ... ] (CollectionExpr 専用) ---
    // ElementAccess が [ ArgumentList ] を使うため、同じ [ 直後の状態で ArgumentList の空還元と
    // CollectionExpr 用リストの空還元がマージして reduce-reduce していた。
    // CollectionExpr の角括弧リストを専用非終元に分離して状態の混入を防ぐ。
    // head + tail 分離。空 [] は NilCollectionElementList。
    spec.AddAbstract("CollectionElementList", "AstNode");
    spec.AddSealed("NilCollectionElementList", "CollectionElementList").Ctor();
    spec.AddSealed("SingleCollectionElementList", "CollectionElementList").Ctor(
        new ParamSpec("Expression", "head"),
        new ParamSpec("CollectionElementTail", "tail"));
    spec.AddAbstract("CollectionElementTail", "AstNode");
    spec.AddSealed("NilCollectionElementTail", "CollectionElementTail").Ctor();
    spec.AddSealed("ConsCollectionElementTail", "CollectionElementTail").Ctor(
        new ParamSpec("Token", "sep", ","),
        new ParamSpec("Expression", "head"),
        new ParamSpec("CollectionElementTail", "tail"));

    // --- ラムダパラメータリスト: ( a, b, c ) / ( ) ---
    // head + tail 分離。空 () は NilLambdaParameterList。
    spec.AddAbstract("LambdaParameterList", "AstNode");
    spec.AddSealed("NilLambdaParameterList", "LambdaParameterList").Ctor();
    spec.AddSealed("SingleLambdaParameterList", "LambdaParameterList").Ctor(
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("LambdaParameterTail", "tail"));
    spec.AddAbstract("LambdaParameterTail", "AstNode");
    spec.AddSealed("NilLambdaParameterTail", "LambdaParameterTail").Ctor();
    spec.AddSealed("ConsLambdaParameterTail", "LambdaParameterTail").Ctor(
        new ParamSpec("Token", "sep", ","),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("LambdaParameterTail", "tail"));

    // --- switch 式アームリスト: Pattern => expr, ... ---
    spec.AddAbstract("SwitchExprArmList", "AstNode");
    spec.AddSealed("NilSwitchExprArmList", "SwitchExprArmList").Ctor();
    spec.AddSealed("SingleSwitchExprArmList", "SwitchExprArmList").Ctor(
        new ParamSpec("SwitchExprArm", "arm"));
    spec.AddSealed("ConsSwitchExprArmList", "SwitchExprArmList").Ctor(
        new ParamSpec("SwitchExprArm", "head"),
        new ParamSpec("Token", "sep", ","),
        new ParamSpec("SwitchExprArmList", "rest"));
    // switch アーム: Pattern => Expression （末尾は , で区切るため arm 自体にカンマを含めない）
    spec.AddAbstract("SwitchExprArm", "AstNode");
    spec.AddSealed("PatternArm", "SwitchExprArm").Ctor(
        new ParamSpec("Pattern", "pattern"),
        new ParamSpec("Token", "arrow", @"=>"),
        new ParamSpec("Expression", "body"));
    // discard アーム: _ => expr
    spec.AddSealed("DiscardArm", "SwitchExprArm").Ctor(
        new ParamSpec("Token", "discard", "_"),
        new ParamSpec("Token", "arrow", @"=>"),
        new ParamSpec("Expression", "body"));

    // --- LINQ クエリ句リスト ---
    // 最低1節（select/group/where/...）。空規則(Nil)を持たせる "from x in xs（句なし）" の
    // 誤った完成が可能になり、in の後の Expression 完了時の postfix/LINQ節 shift と
    // reduce(Nil) が競合して SR が大量発生する。最低1節の Single/Cons で空を排除。
    spec.AddAbstract("QueryClauseList", "AstNode");
    spec.AddSealed("SingleQueryClause", "QueryClauseList").Ctor(
        new ParamSpec("QueryClause", "head"));
    spec.AddSealed("ConsQueryClauseList", "QueryClauseList").Ctor(
        new ParamSpec("QueryClause", "head"),
        new ParamSpec("QueryClauseList", "rest"));
    spec.AddAbstract("QueryClause", "AstNode");
    // from x in src (precedence:1 → 句式内の後置トークン as/is/[/?./../switch(10-15) が shift 優先)
    spec.AddSealed("FromClause", "QueryClause", precedence: 1).Ctor(
        new ParamSpec("Token", "kwFrom", "from", priority: 1),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "kwIn", "in", priority: 1),
        new ParamSpec("Expression", "source"));
    // select expr
    spec.AddSealed("SelectClause", "QueryClause", precedence: 1).Ctor(
        new ParamSpec("Token", "kwSelect", "select", priority: 1),
        new ParamSpec("Expression", "selector"));
    // where expr
    spec.AddSealed("WhereClause", "QueryClause", precedence: 1).Ctor(
        new ParamSpec("Token", "kwWhere", "where", priority: 1),
        new ParamSpec("Expression", "condition"));
    // orderby expr
    spec.AddSealed("OrderByClause", "QueryClause", precedence: 1).Ctor(
        new ParamSpec("Token", "kwOrderBy", "orderby", priority: 1),
        new ParamSpec("Expression", "key"));
    // group expr by expr
    spec.AddSealed("GroupClause", "QueryClause", precedence: 1).Ctor(
        new ParamSpec("Token", "kwGroup", "group", priority: 1),
        new ParamSpec("Expression", "element"),
        new ParamSpec("Token", "kwBy", "by", priority: 1),
        new ParamSpec("Expression", "key"));
    // let name = expr
    spec.AddSealed("LetClause", "QueryClause", precedence: 1).Ctor(
        new ParamSpec("Token", "kwLet", "let", priority: 1),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "eq", "="),
        new ParamSpec("Expression", "value"));

    // ---------------------------------------------------------------------------
    // primary 系（precedence 属性不要 = 最高）。base = Expression に直接。
    // 既存 primary（Integer/Real/String/Char/True/False/Null/Identifier/This/Paren/
    //   MemberAccess/Invocation/PostInc/PostDec）は再利用（再定義しない）。
    // ---------------------------------------------------------------------------

    spec.AddSealed("BaseExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "base", priority: 1));

    spec.AddSealed("HexLiteral", "Expression").Ctor(
        new ParamSpec("Token", "value", @"0[xX][0-9a-fA-F_]+"));

    spec.AddSealed("BinaryLiteral", "Expression").Ctor(
        new ParamSpec("Token", "value", @"0[bB][01_]+"));

    // 文字列リテラル拡張は AddExpressions 側で定義（AddLexical と重複回避）
    spec.AddSealed("VerbatimStringLiteral", "Expression").Ctor(
        new ParamSpec("Token", "value", @"@""(""""|[^""])*"""));

    spec.AddSealed("InterpolatedStringLiteral", "Expression").Ctor(
        new ParamSpec("Token", "value", @"\$\$(""""|[^""])*"""));

    spec.AddSealed("RawStringLiteral", "Expression").Ctor(
        new ParamSpec("Token", "value", @"\""""[\s\S]*?\"""""));

    // DefaultExpr (式の default 単体、target-typed default) は意図的に除外。
    // default 単体が式 (Expression) になると、switch 文の default ラベル (default :) と
    // SwitchSectionStatementList の Single/Cons で衝突する: break; の後の default が
    // 「次の文 (DefaultExpr)」として Cons shift され、default ラベルが認識されなくなる。
    // default(Type) は DefaultTypeExpr で存続。target-typed default (x = default;) は制限。

    // CheckedExpr/UncheckedExpr の「式版」checked(expr)/unchecked(expr) は意図的に除外。
    // ( Expression ) という形を ParenExpr と共有し、LALR(1) の状態マージで reduce-reduce に
    // なる（keyword(Expr) と (Expr) が (Expr.) 状態で合流。LR(k) でないと分離不可）。
    // ブロック版 checked { } / unchecked { } は Statement (CheckedStatement/UncheckedStatement) で存続。

    // NameofExpr: 引数を Expression でなく QualifiedName にする。
    // nameof(X) / nameof(X.Y) の名前は QualifiedName（identifier 連鎖）で表現でき、
    // ( QualifiedName ) は ( Expression ) と別非終端のため ParenExpr と状態を共有しない。
    spec.AddSealed("NameofExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "nameof", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("QualifiedName", "name"),
        new ParamSpec("Token", "rp", @"\)"));

    spec.AddSealed("TypeofExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "typeof", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "rp", @"\)"));

    spec.AddSealed("SizeofExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "sizeof", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "rp", @"\)"));

    // default(Type) (default 演算子) も意図的に除外。default が式 (Expression) の先頭になると
    // switch 文の default ラベル (default :) と SwitchSectionStatementList の Single/Cons が衝突し、
    // break; の後の default が「次の文 (default 演算子)」として Cons shift されてラベルが認識
    // されなくなる。default はラベル (default :) と goto default; のみとし、式用法は制限。

    spec.AddSealed("StackallocExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "stackalloc", priority: 1),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "lb", @"\["),
        new ParamSpec("Expression", "size"),
        new ParamSpec("Token", "rb", @"\]"));

    // タプル式: ( e ) は既存 ParenExpr と重複 → ( e , ... ) のみ（2要素以上）で定義し reduce-reduce 回避
    spec.AddSealed("TupleExpr", "Expression").Ctor(
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Expression", "first"),
        new ParamSpec("Token", "sep", ","),
        new ParamSpec("TupleElementList", "rest"),
        new ParamSpec("Token", "rp", @"\)"));

    // コレクション式: [ e, e, ... ] （primary 位置。要素アクセス expr[..] は postfix で位置分離）
    // 専用の CollectionElementList を使用 (ElementAccess の ArgumentList とは別非終元)。
    // これで [ 直後の状態が混入せず、ArgumentList の空還元との reduce-reduce を解消。
    spec.AddSealed("CollectionExpr", "Expression").Ctor(
        new ParamSpec("Token", "lb", @"\["),
        new ParamSpec("CollectionElementList", "elements"),
        new ParamSpec("Token", "rb", @"\]"));

    // インデックス式（^expr のような単項 hat）: 前置単項。
    // [Precedence(PrecUnary)] で ^ の後の postfix/is/as/with/switch/.. への shift を確定 (state 264 SR 解決)。
    spec.AddSealed("IndexExpr", "Expression", precedence: PrecUnary).Ctor(
        new ParamSpec("Token", "hat", @"\^"),
        new ParamSpec("Expression", "expr"));

    // ---------------------------------------------------------------------------
    // postfix 系（[Precedence(PrecPostfix)]）。演算子が最後の終端でないため必須。
    // 既存 PostIncrement/PostDecrement は再利用。MemberAccess/Invocation も既存再利用。
    // ---------------------------------------------------------------------------

    spec.AddSealed("ElementAccess", "Expression", precedence: PrecPostfix).Ctor(
        new ParamSpec("Expression", "target"),
        new ParamSpec("Token", "lb", @"\["),
        new ParamSpec("ArgumentList", "args"),
        new ParamSpec("Token", "rb", @"\]"));

    spec.AddSealed("NullConditionalMember", "Expression", precedence: PrecPostfix).Ctor(
        new ParamSpec("Expression", "target"),
        new ParamSpec("Token", "op", @"\?\."),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0));

    spec.AddSealed("NullConditionalIndex", "Expression", precedence: PrecPostfix).Ctor(
        new ParamSpec("Expression", "target"),
        new ParamSpec("Token", "op", @"\?\["),
        new ParamSpec("ArgumentList", "args"),
        new ParamSpec("Token", "rb", @"\]"));

    // レンジ演算子（..）: 片側省略形 (a.. / ..b) と「.. 単独」は二項との区別が
    // LALR(1) でつかず大量の reduce-reduce/shift-reduce を出すため表現しない
    // (Roslyn はマルチトークン先読み+意味情報で解決)。「両オペランド必須の二項」のみ定義:
    //   RangeExpr:  from .. to   ([Precedence(PrecPostfix)] で .. 後の shift を確定)
    // これで .. 起因の RR/SR を撲滅。[..] / [a..] のようなコレクション/スライス内の
    // 省略形は式レベルでなく各構文(ElementAccess/CollectionExpr 側)で扱う前提。
    spec.AddSealed("RangeExpr", "Expression", precedence: PrecPostfix).Ctor(
        new ParamSpec("Expression", "from"),
        new ParamSpec("Token", "op", @"\.\."),
        new ParamSpec("Expression", "to"));

    // ---------------------------------------------------------------------------
    // 前置単項（[Precedence(PrecUnary)]）。
    // 既存 UnaryMinus/UnaryPlus/LogicalNot/BitwiseNot は再利用。
    // ---------------------------------------------------------------------------

    spec.AddSealed("PreIncrementExpr", "Expression", precedence: PrecUnary).Ctor(
        new ParamSpec("Token", "op", @"\+\+"),
        new ParamSpec("Expression", "expr"));

    spec.AddSealed("PreDecrementExpr", "Expression", precedence: PrecUnary).Ctor(
        new ParamSpec("Token", "op", "--"),
        new ParamSpec("Expression", "expr"));

    spec.AddSealed("IndirectionExpr", "Expression", precedence: PrecUnary).Ctor(
        new ParamSpec("Token", "op", @"\*"),
        new ParamSpec("Expression", "expr"));

    spec.AddSealed("AddressOfExpr", "Expression", precedence: PrecUnary).Ctor(
        new ParamSpec("Token", "op", "&"),
        new ParamSpec("Expression", "expr"));

    // キャスト: ( Type ) expr。括弧式 (expr) と競合 → [Precedence(PrecUnary)] で shift-reduce 解決
    spec.AddSealed("CastExpr", "Expression", precedence: PrecUnary).Ctor(
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Expression", "expr"));

    // ---------------------------------------------------------------------------
    // 二項演算子 / 三項 / 代入 (precedence 階層)。全て Expression -> Expression op Expression。
    // 優先度: 代入(1,右) < 三項(2,右) < ??(3) < ||(4) < &&(5) < |(6) < ^(7) < &(8)
    //         < ==/!=(9) < 比较/is/as(10) < <</>>(11) < +/- (12) < *//% (13)
    // [Precedence] + 全終端伝播で shift-reduce を解決。
    // ※ <,> は generic の <,> と意味依存 (cast/paren と同様の本質的曖昧性 → 許容)。
    // ---------------------------------------------------------------------------

    // 代入 = と複合代入 (右結合)。PrecAssignment(1)。
    spec.AddSealed("AssignExpr", "Expression", precedence: PrecAssignment, isRightAssoc: true).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"="), new ParamSpec("Expression", "right"));
    spec.AddSealed("AddAssignExpr", "Expression", precedence: PrecAssignment, isRightAssoc: true).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"\+="), new ParamSpec("Expression", "right"));
    spec.AddSealed("SubAssignExpr", "Expression", precedence: PrecAssignment, isRightAssoc: true).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"-="), new ParamSpec("Expression", "right"));
    spec.AddSealed("MulAssignExpr", "Expression", precedence: PrecAssignment, isRightAssoc: true).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"\*="), new ParamSpec("Expression", "right"));
    spec.AddSealed("DivAssignExpr", "Expression", precedence: PrecAssignment, isRightAssoc: true).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"/="), new ParamSpec("Expression", "right"));
    spec.AddSealed("ModAssignExpr", "Expression", precedence: PrecAssignment, isRightAssoc: true).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"%="), new ParamSpec("Expression", "right"));
    spec.AddSealed("AndAssignExpr", "Expression", precedence: PrecAssignment, isRightAssoc: true).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"&="), new ParamSpec("Expression", "right"));
    spec.AddSealed("OrAssignExpr", "Expression", precedence: PrecAssignment, isRightAssoc: true).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"\|="), new ParamSpec("Expression", "right"));
    spec.AddSealed("XorAssignExpr", "Expression", precedence: PrecAssignment, isRightAssoc: true).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"\^="), new ParamSpec("Expression", "right"));
    spec.AddSealed("LeftShiftAssignExpr", "Expression", precedence: PrecAssignment, isRightAssoc: true).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"<<="), new ParamSpec("Expression", "right"));
    spec.AddSealed("RightShiftAssignExpr", "Expression", precedence: PrecAssignment, isRightAssoc: true).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @">>="), new ParamSpec("Expression", "right"));
    spec.AddSealed("CoalesceAssignExpr", "Expression", precedence: PrecAssignment, isRightAssoc: true).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"\?\?="), new ParamSpec("Expression", "right"));

    // 三項 cond ? then : else (右結合)。PrecConditional(2)。
    spec.AddSealed("ConditionalExpr", "Expression", precedence: PrecConditional, isRightAssoc: true).Ctor(
        new ParamSpec("Expression", "cond"),
        new ParamSpec("Token", "question", @"\?"),
        new ParamSpec("Expression", "then"),
        new ParamSpec("Token", "colon", @":"),
        new ParamSpec("Expression", "elseBranch"));

    // ?? PrecNullCoalescing(3)。
    spec.AddSealed("CoalesceExpr", "Expression", precedence: PrecNullCoalescing).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"\?\?"), new ParamSpec("Expression", "right"));

    // ||  && 。PrecConditionalOr(4) / PrecConditionalAnd(5)。
    spec.AddSealed("OrExpr", "Expression", precedence: PrecConditionalOr).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"\|\|"), new ParamSpec("Expression", "right"));
    spec.AddSealed("AndExpr", "Expression", precedence: PrecConditionalAnd).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"&&"), new ParamSpec("Expression", "right"));

    // |  ^  & (ビット)。PrecLogicalOr(6) / PrecLogicalXor(7) / PrecLogicalAnd(8)。
    spec.AddSealed("BitOrExpr", "Expression", precedence: PrecLogicalOr).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"\|"), new ParamSpec("Expression", "right"));
    spec.AddSealed("BitXorExpr", "Expression", precedence: PrecLogicalXor).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"\^"), new ParamSpec("Expression", "right"));
    spec.AddSealed("BitAndExpr", "Expression", precedence: PrecLogicalAnd).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"&"), new ParamSpec("Expression", "right"));

    // ==  != 。PrecEquality(9)。
    spec.AddSealed("EqualExpr", "Expression", precedence: PrecEquality).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"=="), new ParamSpec("Expression", "right"));
    spec.AddSealed("NotEqualExpr", "Expression", precedence: PrecEquality).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"!="), new ParamSpec("Expression", "right"));

    // <  >  <=  >= 。PrecRelational(10)。<,> は generic と意味依存。
    spec.AddSealed("LessExpr", "Expression", precedence: PrecRelational).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"<"), new ParamSpec("Expression", "right"));
    spec.AddSealed("GreaterExpr", "Expression", precedence: PrecRelational).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @">"), new ParamSpec("Expression", "right"));
    spec.AddSealed("LessEqualExpr", "Expression", precedence: PrecRelational).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"<="), new ParamSpec("Expression", "right"));
    spec.AddSealed("GreaterEqualExpr", "Expression", precedence: PrecRelational).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @">="), new ParamSpec("Expression", "right"));

    // <<  >> 。PrecShift(11)。
    spec.AddSealed("LeftShiftExpr", "Expression", precedence: PrecShift).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"<<"), new ParamSpec("Expression", "right"));
    spec.AddSealed("RightShiftExpr", "Expression", precedence: PrecShift).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @">>"), new ParamSpec("Expression", "right"));

    // +  - 。PrecAdditive(12)。
    spec.AddSealed("AddExpr", "Expression", precedence: PrecAdditive).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"\+"), new ParamSpec("Expression", "right"));
    spec.AddSealed("SubExpr", "Expression", precedence: PrecAdditive).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"-"), new ParamSpec("Expression", "right"));

    // *  /  % 。PrecMultiplicative(13)。
    spec.AddSealed("MulExpr", "Expression", precedence: PrecMultiplicative).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"\*"), new ParamSpec("Expression", "right"));
    spec.AddSealed("DivExpr", "Expression", precedence: PrecMultiplicative).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"/"), new ParamSpec("Expression", "right"));
    spec.AddSealed("ModExpr", "Expression", precedence: PrecMultiplicative).Ctor(
        new ParamSpec("Expression", "left"), new ParamSpec("Token", "op", @"%"), new ParamSpec("Expression", "right"));

    spec.AddSealed("AwaitExpr", "Expression", precedence: PrecUnary).Ctor(
        new ParamSpec("Token", "kw", "await", priority: 1),
        new ParamSpec("Expression", "expr"));

    // ---------------------------------------------------------------------------
    // 二項 追加（relational 系）。既存二項演算子は再利用。
    // is / as は [Precedence(PrecRelational)]。
    // ---------------------------------------------------------------------------

    // is は常に Pattern (is Type は IsPatternExpr + TypePattern(Type) で表現)。
    // IsExpr(expr is Type) を式に置くと is の後の Type が Pattern と式で共有され reduce-reduce するため。
    spec.AddSealed("IsPatternExpr", "Expression", precedence: PrecRelational).Ctor(
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "kw", "is", priority: 1),
        new ParamSpec("Pattern", "pattern"));

    spec.AddSealed("AsExpr", "Expression", precedence: PrecRelational).Ctor(
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "kw", "as", priority: 1),
        new ParamSpec("Type", "type"));

    // ---------------------------------------------------------------------------
    // ラムダ（[Precedence(PrecLambda, IsRightAssoc=true)]）。=> は最も低優先。
    // ---------------------------------------------------------------------------

    // 単純ラムダ: name => expr
    spec.AddSealed("SimpleLambdaExpr", "Expression", precedence: PrecLambda, isRightAssoc: true).Ctor(
        new ParamSpec("Token", "param", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "arrow", @"=>"),
        new ParamSpec("Expression", "body"));

    // ※ 括弧ラムダ ( params ) => expr は method の ( params ) と ( ) が共通で LALR(1) で状態マージし
    //   ) の後 { (method body) vs => (Lambda) の競合を起こすため除外。
    //   多引数ラムダは匿名メソッド delegate ( params ) { } で代替 (delegate で method と区別)。
    //   単純ラムダ name => expr は SimpleLambdaExpr で維持。

    // 匿名メソッド: delegate { ... } / delegate ( params ) { ... } （本体は文ブロックだが簡易に Expression 化）
    spec.AddSealed("AnonymousMethodExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "delegate", priority: 1),
        new ParamSpec("Token", "lb", @"\{"),
        new ParamSpec("Token", "rb", @"\}"));

    spec.AddSealed("AnonymousMethodParamsExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "delegate", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("LambdaParameterList", "parameters"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Token", "lb", @"\{"),
        new ParamSpec("Token", "rb", @"\}"));

    // ---------------------------------------------------------------------------
    // 複合（new / with / switch / query / throw）。base = Expression。
    // ---------------------------------------------------------------------------

    // オブジェクト生成: new Type ( args ) 初期化子? / new Type ( ) 初期化子? / new Type 初期化子
    spec.AddSealed("ObjectCreationExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "new", priority: 1),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("ArgumentList", "args"),
        new ParamSpec("Token", "rp", @"\)"));

    spec.AddSealed("ObjectCreationInitExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "new", priority: 1),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("ArgumentList", "args"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Token", "lb", @"\{"),
        new ParamSpec("InitializerList", "initializers"),
        new ParamSpec("Token", "rb", @"\}"));

    spec.AddSealed("ObjectCreationEmptyInitExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "new", priority: 1),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "lb", @"\{"),
        new ParamSpec("InitializerList", "initializers"),
        new ParamSpec("Token", "rb", @"\}"));

    // 配列生成: new Type [ size ] 初期化子?
    spec.AddSealed("ArrayCreationExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "new", priority: 1),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "lb", @"\["),
        new ParamSpec("Expression", "size"),
        new ParamSpec("Token", "rb", @"\]"));

    spec.AddSealed("ArrayCreationInitExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "new", priority: 1),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "lb", @"\["),
        new ParamSpec("Expression", "size"),
        new ParamSpec("Token", "rb", @"\]"),
        new ParamSpec("Token", "lb2", @"\{"),
        new ParamSpec("InitializerList", "initializers"),
        new ParamSpec("Token", "rb2", @"\}"));

    // 暗黙的配列生成: new[] { ... }
    spec.AddSealed("ImplicitArrayCreationExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "new", priority: 1),
        new ParamSpec("Token", "lb", @"\["),
        new ParamSpec("Token", "rb", @"\]"),
        new ParamSpec("Token", "lb2", @"\{"),
        new ParamSpec("InitializerList", "initializers"),
        new ParamSpec("Token", "rb2", @"\}"));

    // ターゲット型 new: new ( args ) 初期化子?
    spec.AddSealed("TargetTypedNewExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "new", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("ArgumentList", "args"),
        new ParamSpec("Token", "rp", @"\)"));

    spec.AddSealed("TargetTypedNewInitExpr", "Expression").Ctor(
        new ParamSpec("Token", "kw", "new", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("ArgumentList", "args"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Token", "lb", @"\{"),
        new ParamSpec("InitializerList", "initializers"),
        new ParamSpec("Token", "rb", @"\}"));

    // with 式: expr with { ... }
    spec.AddSealed("WithExpr", "Expression", precedence: PrecPostfix).Ctor(
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "kw", "with", priority: 1),
        new ParamSpec("Token", "lb", @"\{"),
        new ParamSpec("InitializerList", "initializers"),
        new ParamSpec("Token", "rb", @"\}"));

    // switch 式: expr switch { arms }
    spec.AddSealed("SwitchExpr", "Expression", precedence: PrecPostfix).Ctor(
        new ParamSpec("Expression", "governor"),
        new ParamSpec("Token", "kw", "switch", priority: 1),
        new ParamSpec("Token", "lb", @"\{"),
        new ParamSpec("SwitchExprArmList", "arms"),
        new ParamSpec("Token", "rb", @"\}"));

    // クエリ式: from x in src clause...
    spec.AddSealed("QueryExpr", "Expression").Ctor(
        new ParamSpec("Token", "kwFrom", "from", priority: 1),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "kwIn", "in", priority: 1),
        new ParamSpec("Expression", "source"),
        new ParamSpec("QueryClauseList", "clauses"));

    // throw 式: throw expr
    spec.AddSealed("ThrowExpr", "Expression", precedence: PrecAssignment).Ctor(
        new ParamSpec("Token", "kw", "throw", priority: 1),
        new ParamSpec("Expression", "expr"));
}

private static void AddPatterns(GrammarSpec spec)
{
    // =============================================================================
    // パターンセクション (is / switch 式アーム / case ラベル)。
    // 全具象パターンは直接 Pattern (base = AstNode 1段階) の下。
    //
    // コンフリクト回避方針 (reduce-reduce を設計で出さない):
    //  - 代替の「先頭終端の字句クラス」で完全に分離する。
    //    * 定数パターン (リテラル)  -> 数字 / 文字列 / 文字 / true / false / null で始まる。
    //      旧 ConstantPattern(Expression) は式全体 (identifier 含む) を受け取り、TypePattern(Type)
    //      と共に identifier 先頭で同一状態になり reduce-reduce が爆発していた (state 313 等)。
    //      C# の定数パターンはリテラルのみ (is 5 / is null) なので、リテラル種別ごとに具象を分離。
    //      これで 定数=リテラル先頭 / 型・宣言=identifier 先頭 が分離され reduce-reduce 解消。
    //    * 型パターン / 宣言パターン -> 識別子/キーワード型 で始まる (同一先頭だが後続で分離可能)。
    //    * 括弧/リスト/プロパティ/関係/前置 -> 各々区切り子・演算子で始まる。
    //  - 二項 and/or と前置 not の shift-reduce は [Precedence] で解決。
    //  - リスト (ListPatternElemList / PositionalRestList / PropPatternList) は空リストの
    //    reduce-reduce を回避するため、各使用位置で別の補助非終端に分離 (空を許す代替を 1 箇所に固定)。
    // =============================================================================

    spec.AddAbstract("Pattern", "AstNode");

    // ---------- 定数パターン (リテラルのみ。先頭終端で Type と排他) ----------
    // リテラルはいずれも数字/"/'/true/false/null で始まり、identifier (Type) と字句衝突しない。
    // priority 指定不要 (リテラルは最長一致で DFA が固有に弁別)。true/false/null は priority:1。

    spec.AddSealed("NullConstantPattern", "Pattern")
        .Ctor(new ParamSpec("Token", "kw", "null", priority: 1));

    spec.AddSealed("TrueConstantPattern", "Pattern")
        .Ctor(new ParamSpec("Token", "kw", "true", priority: 1));

    spec.AddSealed("FalseConstantPattern", "Pattern")
        .Ctor(new ParamSpec("Token", "kw", "false", priority: 1));

    spec.AddSealed("IntConstantPattern", "Pattern")
        .Ctor(new ParamSpec("Token", "value", @"[0-9]+[dDfFlLmMuU]*"));

    spec.AddSealed("RealConstantPattern", "Pattern")
        .Ctor(new ParamSpec("Token", "value", @"[0-9]+\.[0-9]+([eE][+-]?[0-9]+)?[fFdDmM]*"));

    spec.AddSealed("HexConstantPattern", "Pattern")
        .Ctor(new ParamSpec("Token", "value", @"0[xX][0-9a-fA-F_]+"));

    spec.AddSealed("BinaryConstantPattern", "Pattern")
        .Ctor(new ParamSpec("Token", "value", @"0[bB][01_]+"));

    spec.AddSealed("CharConstantPattern", "Pattern")
        .Ctor(new ParamSpec("Token", "value", @"'([^'\\]|\\.)'"));

    spec.AddSealed("StringConstantPattern", "Pattern")
        .Ctor(new ParamSpec("Token", "value", @"""([^""\\]|\\.)*"""));

    // ---------- 型・宣言・var・破棄 (identifier/キーワード型 先頭) ----------
    // これらは全て identifier または定義済み型キーワード (int 等) で始まる。
    // 旧 ConstantPattern(Expression) が無くなったことで、identifier 先頭の代替は全て Type 由来
    // となり、Type を還元した直後の状態で次トークンを見て shift(宣言)/reduce(型) を選べる。
    // 宣言は「Type の直後に identifier が続く」ことで事後に弁別可能。

    // 型パターン: case int: / is Foo など。型1つを受け取る。
    spec.AddSealed("TypePattern", "Pattern")
        .Ctor(new ParamSpec("Type", "type"));

    // 宣言パターン: case Foo x: / is int i など。型 + 識別子。
    spec.AddSealed("DeclarationPattern", "Pattern")
        .Ctor(new ParamSpec("Type", "type"),
              new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0));

    // var パターン: is var x / case var x: 等。var は priority:1 で識別子と弁別。
    spec.AddSealed("VarPattern", "Pattern")
        .Ctor(new ParamSpec("Token", "varKw", @"var", priority: 1),
              new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0));

    // 破棄パターン: is _ / case _:。 "_" は priority:1 で [A-Za-z_]\w* (priority:0) より優先。
    // 1文字 "_" の場合は discard、_foo 等は最長一致で識別子として字句化される。
    spec.AddSealed("DiscardPattern", "Pattern")
        .Ctor(new ParamSpec("Token", "underscore", @"_", priority: 1));

    // ---------- 括弧 / リスト / プロパティ / 位置指定 / スライス ----------
    // 区切り子が規則の先頭終端 → [Precedence] を付与して後続の and/or 等との
    // shift-reduce を解決する (precedence は規則の全終端に伝播)。

    // 括弧パターン: is (P).
    spec.AddSealed("ParenPattern", "Pattern", precedence: 15)
        .Ctor(new ParamSpec("Token", "lp", @"\("),
              new ParamSpec("Pattern", "inner"),
              new ParamSpec("Token", "rp", @"\)"));

    // リストパターン: is [ P, P ] / case []:.
    // 専用の ListPatternElemList を使用 (空 [] を含む)。空を許す代替をここだけに局所化。
    spec.AddSealed("ListPattern", "Pattern", precedence: 15)
        .Ctor(new ParamSpec("Token", "lb", @"\["),
              new ParamSpec("ListPatternElemList", "elements"),
              new ParamSpec("Token", "rb", @"\]"));

    // スライスパターン: is [..] / case [..x]. ".." 単独。
    spec.AddSealed("SlicePattern", "Pattern", precedence: 15)
        .Ctor(new ParamSpec("Token", "lb", @"\["),
              new ParamSpec("Token", "slice", @"\.\."),
              new ParamSpec("Token", "rb", @"\]"));

    // プロパティパターン: is Foo { X : P } / is { Count: 3 }.
    spec.AddSealed("PropertyPattern", "Pattern", precedence: 15)
        .Ctor(new ParamSpec("Token", "lb", @"\{"),
              new ParamSpec("PropPatternList", "properties"),
              new ParamSpec("Token", "rb", @"\}"));

    // 位置指定パターン (多要素必須): is (P, Q) / case (1, 2):.
    // カンマを必須とし、単一 (P) は ParenPattern として位置で分離 → reduce-reduce 回避。
    // 残りは専用 PositionalRestList (1個以上) を使用 (旧 PatternList とは別物で衝突しない)。
    spec.AddSealed("PositionalPattern", "Pattern", precedence: 15)
        .Ctor(new ParamSpec("Token", "lp", @"\("),
              new ParamSpec("Pattern", "first"),
              new ParamSpec("Token", "comma", @","),
              new ParamSpec("PositionalRestList", "rest"),
              new ParamSpec("Token", "rp", @"\)"));

    // ---------- 関係パターン (< > <= >=) ----------
    // 演算子が規則の先頭終端 → [Precedence] 付与で shift-reduce を解決。
    // < > <= >= は式の同名トークンと同一字句だが Pattern 文脈(is/case 直後)のみ使用。
    // 4つを個別具象に分離し、演算子リテラルで DFA が自動弁別。

    spec.AddSealed("LessThanPattern", "Pattern", precedence: 14)
        .Ctor(new ParamSpec("Token", "op", @"<"),
              new ParamSpec("Pattern", "right"));

    spec.AddSealed("GreaterThanPattern", "Pattern", precedence: 14)
        .Ctor(new ParamSpec("Token", "op", @">"),
              new ParamSpec("Pattern", "right"));

    spec.AddSealed("LessThanOrEqualPattern", "Pattern", precedence: 14)
        .Ctor(new ParamSpec("Token", "op", @"<="),
              new ParamSpec("Pattern", "right"));

    spec.AddSealed("GreaterThanOrEqualPattern", "Pattern", precedence: 14)
        .Ctor(new ParamSpec("Token", "op", @">="),
              new ParamSpec("Pattern", "right"));

    // ---------- 前置 not パターン ----------
    // is not P. not は priority:1。[Precedence(PrecUnary)]。
    spec.AddSealed("NotPattern", "Pattern", precedence: 14)
        .Ctor(new ParamSpec("Token", "notKw", @"not", priority: 1),
              new ParamSpec("Pattern", "operand"));

    // ---------- 二項 and パターン ----------
    // P and Q. and は priority:1。[Precedence(PrecLogicalAnd=8)]。
    spec.AddSealed("AndPattern", "Pattern", precedence: 8)
        .Ctor(new ParamSpec("Pattern", "left"),
              new ParamSpec("Token", "andKw", @"and", priority: 1),
              new ParamSpec("Pattern", "right"));

    // ---------- 二項 or パターン ----------
    // P or Q. or は priority:1。[Precedence(PrecLogicalOr=6)] (and より低)。
    spec.AddSealed("OrPattern", "Pattern", precedence: 6)
        .Ctor(new ParamSpec("Pattern", "left"),
              new ParamSpec("Token", "orKw", @"or", priority: 1),
              new ParamSpec("Pattern", "right"));

    // ============================================================
    // 補助非終端 (リスト): base=AstNode の抽象 + Cons/Nil 左再帰具象。
    // 抽象は1段階制約。これらは Pattern 系から参照される。
    // 各リストは「空を許す代替」を 1 箇所 (Nil) に限定し、複数の空規則が同一状態で
    // reduce-reduce を起こさないよう、使用位置ごとに別の補助非終端に分離した。
    // ============================================================

    // ListPatternElemList: ListPattern の要素。[] (空) / P / P, P, ... を許可。
    // 単一の Nil (空) を持ち、ListPattern からのみ参照される。
    spec.AddAbstract("ListPatternElemList", "AstNode");

    spec.AddSealed("NilListPatternElem", "ListPatternElemList")
        .Ctor();

    spec.AddSealed("SingleListPatternElem", "ListPatternElemList")
        .Ctor(new ParamSpec("Pattern", "head"),
              new ParamSpec("ListPatternElemTail", "tail"));

    // ListPatternElemTail: 先頭要素後のカンマ区切り残り (空可)。空 Nil を持つ。
    spec.AddAbstract("ListPatternElemTail", "AstNode");

    spec.AddSealed("NilListPatternElemTail", "ListPatternElemTail")
        .Ctor();

    spec.AddSealed("ConsListPatternElemTail", "ListPatternElemTail")
        .Ctor(new ParamSpec("Token", "comma", @","),
              new ParamSpec("Pattern", "head"),
              new ParamSpec("ListPatternElemTail", "tail"));

    // PositionalRestList: 位置指定パターンの第2要素以降 (1個以上 + カンマ継続)。
    // 空を許さない (カンマの後に最低1個)。末尾は Single1 個、継続は PositionalRestTail。
    spec.AddAbstract("PositionalRestList", "AstNode");

    spec.AddSealed("SinglePositionalRest", "PositionalRestList")
        .Ctor(new ParamSpec("Pattern", "head"),
              new ParamSpec("PositionalRestTail", "tail"));

    spec.AddAbstract("PositionalRestTail", "AstNode");

    spec.AddSealed("NilPositionalRestTail", "PositionalRestTail")
        .Ctor();

    spec.AddSealed("ConsPositionalRestTail", "PositionalRestTail")
        .Ctor(new ParamSpec("Token", "comma", @","),
              new ParamSpec("Pattern", "head"),
              new ParamSpec("PositionalRestTail", "tail"));

    // PropPatternList: PropertyPattern 内の name : Pattern の繰り返し (空可)。Cons/Nil 左再帰。
    spec.AddAbstract("PropPatternList", "AstNode");

    spec.AddSealed("NilPropPatternList", "PropPatternList")
        .Ctor();

    spec.AddSealed("SinglePropPattern", "PropPatternList")
        .Ctor(new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
              new ParamSpec("Token", "colon", @":"),
              new ParamSpec("Pattern", "value"));

    spec.AddSealed("ConsPropPattern", "PropPatternList")
        .Ctor(new ParamSpec("PropPatternList", "list"),
              new ParamSpec("Token", "comma", @","),
              new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
              new ParamSpec("Token", "colon", @":"),
              new ParamSpec("Pattern", "value"));
}

private static void AddStatements(GrammarSpec spec)
{
    spec.AddAbstract("Statement", "AstNode");

    // --- 既存: Empty / Expr / Var / If / IfElse / While / Return / ReturnExpr / Break / Continue / Block ---
    spec.AddSealed("EmptyStatement", "Statement").Ctor(new ParamSpec("Token", "semi", @";"));
    spec.AddSealed("ExpressionStatement", "Statement").Ctor(
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "semi", @";"));

    // ローカル宣言: var name = expr ; のみ (generic < と比較式 < の衝突回避。const は別規則で初終端が異なる)。
    spec.AddSealed("VarDeclaration", "Statement").Ctor(
        new ParamSpec("Token", "varKw", "var", priority: 1),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "eq", @"="),
        new ParamSpec("Expression", "init"),
        new ParamSpec("Token", "semi", @";"));

    // ローカル const: const Type name = expr ;  (初終端 const で VarDeclaration と分離、reduce-reduce 無し)
    spec.AddSealed("LocalConstantDeclaration", "Statement").Ctor(
        new ParamSpec("Token", "constKw", "const", priority: 1),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "eq", @"="),
        new ParamSpec("Expression", "init"),
        new ParamSpec("Token", "semi", @";"));

    // if / if-else (dangling else は shift 優先 = 最内 if)
    spec.AddSealed("IfStatement", "Statement").Ctor(
        new ParamSpec("Token", "ifKw", "if", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Expression", "cond"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "then"));
    spec.AddSealed("IfElseStatement", "Statement").Ctor(
        new ParamSpec("Token", "ifKw", "if", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Expression", "cond"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "then"),
        new ParamSpec("Token", "elseKw", "else", priority: 1),
        new ParamSpec("Statement", "elseBranch"));

    spec.AddSealed("WhileStatement", "Statement").Ctor(
        new ParamSpec("Token", "whileKw", "while", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Expression", "cond"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));

    spec.AddSealed("ReturnStatement", "Statement").Ctor(
        new ParamSpec("Token", "returnKw", "return", priority: 1),
        new ParamSpec("Token", "semi", @";"));
    spec.AddSealed("ReturnExprStatement", "Statement").Ctor(
        new ParamSpec("Token", "returnKw", "return", priority: 1),
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "semi", @";"));

    spec.AddSealed("BreakStatement", "Statement").Ctor(
        new ParamSpec("Token", "breakKw", "break", priority: 1),
        new ParamSpec("Token", "semi", @";"));
    spec.AddSealed("ContinueStatement", "Statement").Ctor(
        new ParamSpec("Token", "continueKw", "continue", priority: 1),
        new ParamSpec("Token", "semi", @";"));

    // { 文リスト } / 空 { }
    // BlockStatement を空と非空の2規則に分け、StatementList は Single/Cons (最低1文) にする。
    // Nil (空規則) を持たせると IfStatement 等の完了後に次の文の shift と Nil reduce が衝突し、
    // 文リストが1文で終了して panic する。Single/Cons で空を排除し、空 {} は BlockStatement 側で別規則化。
    spec.AddSealed("BlockStatement", "Statement").Ctor(
        new ParamSpec("Token", "lb", @"\{"),
        new ParamSpec("Token", "rb", @"\}"));
    spec.AddSealed("BlockStatementWithBody", "Statement").Ctor(
        new ParamSpec("Token", "lb", @"\{"),
        new ParamSpec("StatementList", "body"),
        new ParamSpec("Token", "rb", @"\}"));

    // 文リスト (左再帰 Single/Cons)。最低1文 (空なし)。空 {} は BlockStatement で別規則化済み。
    spec.AddAbstract("StatementList", "AstNode");
    spec.AddSealed("SingleStatement", "StatementList").Ctor(
        new ParamSpec("Statement", "head"));
    spec.AddSealed("ConsStatement", "StatementList").Ctor(
        new ParamSpec("Statement", "head"),
        new ParamSpec("StatementList", "rest"));
    // SwitchSection 専用: 最低1文必須の文リスト (空規則なし)。
    // switch セクション本体は空を許さず、case/default ラベルの次に必ず文が続く。
    // 空規則 (Nil) をこの文脈から排除することで、case/default の shift と StatementList->ε の
    // reduce が衝突する SR (state 477/684) を解消する (BlockStatement の StatementList は空可のまま)。
    spec.AddAbstract("SwitchSectionStatementList", "AstNode");
    spec.AddSealed("SingleSwitchSectionStatement", "SwitchSectionStatementList").Ctor(
        new ParamSpec("Statement", "head"));
    spec.AddSealed("ConsSwitchSectionStatement", "SwitchSectionStatementList").Ctor(
        new ParamSpec("Statement", "head"),
        new ParamSpec("SwitchSectionStatementList", "rest"));

    // === for 文 ===
    // ForInit: 式リスト or var 宣言 (セミコロン無し)。抽象で包み for 文脈に局所化 → 式位置との reduce-reduce 回避。
    spec.AddAbstract("ForInit", "AstNode");
    spec.AddSealed("NilForInit", "ForInit").Ctor();
    spec.AddSealed("ForInitVar", "ForInit").Ctor(
        new ParamSpec("Token", "varKw", "var", priority: 1),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "eq", @"="),
        new ParamSpec("Expression", "init"));
    // ForInitExprList: カンマ区切り式リスト (最低1要素)。for 文の init 部 a, b, c を表現。
    // 【RR 解消】旧 Nil + Single + Cons(head-first) の 3 形式混在が原因:
    //   Single(Expression) と Cons(Expression, rest) が共に Expression 先頭で、Expression 還元後の状態で
    //   ; を見たとき両方還元可能 (state 560 RR)、かつ要素前で Nil/Single が共に ; に還元 (state 485 RR)。
    //   head-first Cons + Tail(ε|, e Tail) の 2 段構成に統一:
    //     - 空リストは持たない (空は外側 ForInit の NilForInit が別状態で担当 → 本リストは最低1要素)。
    //     - 単一/複数の各還元パスが各状態で 1 本化 → reduce-reduce 解消。
    spec.AddSealed("ForInitExprList", "ForInit").Ctor(
        new ParamSpec("Expression", "head"),
        new ParamSpec("ForInitExprTail", "tail"));
    spec.AddAbstract("ForInitExprTail", "AstNode");
    spec.AddSealed("NilForInitExprTail", "ForInitExprTail").Ctor();
    spec.AddSealed("ConsForInitExprTail", "ForInitExprTail").Ctor(
        new ParamSpec("Token", "comma", @","),
        new ParamSpec("Expression", "head"),
        new ParamSpec("ForInitExprTail", "tail"));
    // ForUpdate: 式リスト (省略可)。ForInit と同じ 2 段構成で RR 解消。
    spec.AddAbstract("ForUpdate", "AstNode");
    spec.AddSealed("NilForUpdate", "ForUpdate").Ctor();
    spec.AddSealed("ForUpdateExprList", "ForUpdate").Ctor(
        new ParamSpec("Expression", "head"),
        new ParamSpec("ForUpdateExprTail", "tail"));
    spec.AddAbstract("ForUpdateExprTail", "AstNode");
    spec.AddSealed("NilForUpdateExprTail", "ForUpdateExprTail").Ctor();
    spec.AddSealed("ConsForUpdateExprTail", "ForUpdateExprTail").Ctor(
        new ParamSpec("Token", "comma", @","),
        new ParamSpec("Expression", "head"),
        new ParamSpec("ForUpdateExprTail", "tail"));
    // ForCondition: 式 or 空
    spec.AddAbstract("ForCondition", "AstNode");
    spec.AddSealed("NilForCondition", "ForCondition").Ctor();
    spec.AddSealed("ForConditionExpr", "ForCondition").Ctor(new ParamSpec("Expression", "cond"));

    spec.AddSealed("ForStatement", "Statement").Ctor(
        new ParamSpec("Token", "forKw", "for", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("ForInit", "init"),
        new ParamSpec("Token", "semi1", @";"),
        new ParamSpec("ForCondition", "cond"),
        new ParamSpec("Token", "semi2", @";"),
        new ParamSpec("ForUpdate", "update"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));

    // === foreach 文 ===
    // foreach ( Type name in Expression ) Statement   (in は priority:1)
    spec.AddSealed("ForEachStatement", "Statement").Ctor(
        new ParamSpec("Token", "foreachKw", "foreach", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "inKw", "in", priority: 1),
        new ParamSpec("Expression", "collection"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));
    // foreach ( var name in Expression ) Statement
    spec.AddSealed("ForEachVarStatement", "Statement").Ctor(
        new ParamSpec("Token", "foreachKw", "foreach", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Token", "varKw", "var", priority: 1),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "inKw", "in", priority: 1),
        new ParamSpec("Expression", "collection"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));

    // === do 文 ===
    spec.AddSealed("DoStatement", "Statement").Ctor(
        new ParamSpec("Token", "doKw", "do", priority: 1),
        new ParamSpec("Statement", "body"),
        new ParamSpec("Token", "whileKw", "while", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Expression", "cond"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Token", "semi", @";"));

    // === switch 文 ===
    // CaseLabel: case 式 : / default :
    spec.AddAbstract("CaseLabel", "AstNode");
    spec.AddSealed("CaseExprLabel", "CaseLabel").Ctor(
        new ParamSpec("Token", "caseKw", "case", priority: 1),
        new ParamSpec("Expression", "value"),
        new ParamSpec("Token", "colon", @":"));
    spec.AddSealed("DefaultLabel", "CaseLabel").Ctor(
        new ParamSpec("Token", "defaultKw", "default", priority: 1),
        new ParamSpec("Token", "colon", @":"));
    // ラベルリスト (case は連続可: case 1: case 2:)
    spec.AddAbstract("CaseLabelList", "AstNode");
    spec.AddSealed("SingleCaseLabel", "CaseLabelList").Ctor(new ParamSpec("CaseLabel", "head"));
    spec.AddSealed("ConsCaseLabel", "CaseLabelList").Ctor(
        new ParamSpec("CaseLabelList", "list"),
        new ParamSpec("CaseLabel", "label"));
    // SwitchSection: ラベルリスト + 文リスト (最低1文必須 → SwitchSectionStatementList 使用)
    spec.AddAbstract("SwitchSection", "AstNode");
    spec.AddSealed("LabeledSwitchSection", "SwitchSection").Ctor(
        new ParamSpec("CaseLabelList", "labels"),
        new ParamSpec("SwitchSectionStatementList", "body"));
    // セクションリスト (空可 = switch{})
    spec.AddAbstract("SwitchSectionList", "AstNode");
    spec.AddSealed("NilSwitchSection", "SwitchSectionList").Ctor();
    spec.AddSealed("ConsSwitchSection", "SwitchSectionList").Ctor(
        new ParamSpec("SwitchSection", "head"),
        new ParamSpec("SwitchSectionList", "rest"));

    spec.AddSealed("SwitchStatement", "Statement").Ctor(
        new ParamSpec("Token", "switchKw", "switch", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Token", "lb", @"\{"),
        new ParamSpec("SwitchSectionList", "sections"),
        new ParamSpec("Token", "rb", @"\}"));

    // === try 文 ===
    // CatchClause: catch ( Type name? ) Block / catch Block
    spec.AddAbstract("CatchClause", "AstNode");
    spec.AddSealed("CatchTypeClause", "CatchClause").Ctor(
        new ParamSpec("Token", "catchKw", "catch", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));
    spec.AddSealed("CatchTypeOnlyClause", "CatchClause").Ctor(
        new ParamSpec("Token", "catchKw", "catch", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));
    spec.AddSealed("CatchGeneralClause", "CatchClause").Ctor(
        new ParamSpec("Token", "catchKw", "catch", priority: 1),
        new ParamSpec("Statement", "body"));
    // === try-catch-finally ===
    // catch リスト (空可 = try 単体 / try-finally) と finally 節 (省略可)。
    // try-catch-finally の省略可能な catch/finally の組合せは LALR(1) の本質的曖昧性で、
    //   CatchClauseList->ε と catch の shift、FinallyClause->ε と finally の shift が
    //   benign SR として残る (shift 優先 = catch/finally を続ける、が常に正解。
    //   LINQ QueryClauseList の空規則 SR と同列の benign)。
    spec.AddAbstract("CatchClauseList", "AstNode");
    spec.AddSealed("NilCatchClause", "CatchClauseList").Ctor();
    spec.AddSealed("ConsCatchClause", "CatchClauseList").Ctor(
        new ParamSpec("CatchClause", "head"),
        new ParamSpec("CatchClauseList", "rest"));
    // finally 節 (省略可)
    spec.AddAbstract("FinallyClause", "AstNode");
    spec.AddSealed("NilFinallyClause", "FinallyClause").Ctor();
    spec.AddSealed("PresentFinallyClause", "FinallyClause").Ctor(
        new ParamSpec("Token", "finallyKw", "finally", priority: 1),
        new ParamSpec("Statement", "body"));

    spec.AddSealed("TryStatement", "Statement").Ctor(
        new ParamSpec("Token", "tryKw", "try", priority: 1),
        new ParamSpec("Statement", "body"),
        new ParamSpec("CatchClauseList", "catchClauses"),
        new ParamSpec("FinallyClause", "finallyClause"));

    // === lock / using / fixed ===
    spec.AddSealed("LockStatement", "Statement").Ctor(
        new ParamSpec("Token", "lockKw", "lock", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));

    // UsingResource: 式 or var 宣言 (抽象で局所化)
    spec.AddAbstract("UsingResource", "AstNode");
    spec.AddSealed("UsingExprResource", "UsingResource").Ctor(new ParamSpec("Expression", "expr"));
    spec.AddSealed("UsingVarResource", "UsingResource").Ctor(
        new ParamSpec("Token", "varKw", "var", priority: 1),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "eq", @"="),
        new ParamSpec("Expression", "init"));

    spec.AddSealed("UsingStatement", "Statement").Ctor(
        new ParamSpec("Token", "usingKw", "using", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("UsingResource", "resource"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));

    // fixed ( Type * name = Expression ) Statement  (unsafe)
    spec.AddSealed("FixedStatement", "Statement").Ctor(
        new ParamSpec("Token", "fixedKw", "fixed", priority: 1),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "star", @"\*"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "eq", @"="),
        new ParamSpec("Expression", "init"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));

    // === checked / unchecked / unsafe ===
    spec.AddSealed("CheckedStatement", "Statement").Ctor(
        new ParamSpec("Token", "checkedKw", "checked", priority: 1),
        new ParamSpec("Statement", "body"));
    spec.AddSealed("UncheckedStatement", "Statement").Ctor(
        new ParamSpec("Token", "uncheckedKw", "unchecked", priority: 1),
        new ParamSpec("Statement", "body"));
    spec.AddSealed("UnsafeStatement", "Statement").Ctor(
        new ParamSpec("Token", "unsafeKw", "unsafe", priority: 1),
        new ParamSpec("Statement", "body"));

    // === yield ===
    spec.AddSealed("YieldReturnStatement", "Statement").Ctor(
        new ParamSpec("Token", "yieldKw", "yield", priority: 1),
        new ParamSpec("Token", "returnKw", "return", priority: 1),
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "semi", @";"));
    spec.AddSealed("YieldBreakStatement", "Statement").Ctor(
        new ParamSpec("Token", "yieldKw", "yield", priority: 1),
        new ParamSpec("Token", "breakKw", "break", priority: 1),
        new ParamSpec("Token", "semi", @";"));

    // === goto ===
    spec.AddSealed("GotoStatement", "Statement").Ctor(
        new ParamSpec("Token", "gotoKw", "goto", priority: 1),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "semi", @";"));
    spec.AddSealed("GotoCaseStatement", "Statement").Ctor(
        new ParamSpec("Token", "gotoKw", "goto", priority: 1),
        new ParamSpec("Token", "caseKw", "case", priority: 1),
        new ParamSpec("Expression", "value"),
        new ParamSpec("Token", "semi", @";"));
    spec.AddSealed("GotoDefaultStatement", "Statement").Ctor(
        new ParamSpec("Token", "gotoKw", "goto", priority: 1),
        new ParamSpec("Token", "defaultKw", "default", priority: 1),
        new ParamSpec("Token", "semi", @";"));

    // === ラベル付き文 ===
    // name : Statement  (: は Pattern。三項の : と区別 → 文頭の識別子直後の : は文脈で分離)
    spec.AddSealed("LabeledStatement", "Statement").Ctor(
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "colon", @":"),
        new ParamSpec("Statement", "body"));

    // === throw ===
    spec.AddSealed("ThrowStatement", "Statement").Ctor(
        new ParamSpec("Token", "throwKw", "throw", priority: 1),
        new ParamSpec("Token", "semi", @";"));
    spec.AddSealed("ThrowExprStatement", "Statement").Ctor(
        new ParamSpec("Token", "throwKw", "throw", priority: 1),
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "semi", @";"));
}

    // === 宣言 (using / namespace / 型宣言。1段階: 全具象が Declaration 直接) ===
    private static void AddDeclarations(GrammarSpec spec)
    {
        AddQualifiedName(spec);
        AddModifierList(spec);

        // トップレベル/namespace 内に置ける宣言 (共通の 1 段階抽象)。
        spec.AddAbstract("Declaration", "AstNode");
        spec.AddSealed("UsingDirective", "Declaration").Ctor(
            new ParamSpec("Token", "usingKw", "using", priority: 1),
            new ParamSpec("QualifiedName", "ns"),
            new ParamSpec("Token", "semi", @";"));
        spec.AddSealed("NamespaceDeclaration", "Declaration").Ctor(
            new ParamSpec("Token", "namespaceKw", "namespace", priority: 1),
            new ParamSpec("QualifiedName", "name"),
            new ParamSpec("Token", "lb", @"\{"),
            new ParamSpec("NamespaceBody", "body"),
            new ParamSpec("Token", "rb", @"\}"));
        spec.AddSealed("ClassDeclaration", "Declaration").Ctor(
            new ParamSpec("ModifierList", "modifiers"),
            new ParamSpec("Token", "classKw", "class", priority: 1),
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
            new ParamSpec("OptionalTypeParameterList", "typeParams"),
            new ParamSpec("BaseList", "bases"),
            new ParamSpec("TypeParameterConstraintClauseList", "constraints"),
            new ParamSpec("Token", "lb", @"\{"),
            new ParamSpec("MemberBody", "body"),
            new ParamSpec("Token", "rb", @"\}"));

        // namespace 本体リスト (入れ子の Declaration)
        spec.AddAbstract("NamespaceBody", "AstNode");
        spec.AddSealed("NilNamespaceBody", "NamespaceBody").Ctor();
        spec.AddSealed("ConsNamespaceMember", "NamespaceBody").Ctor(
            new ParamSpec("Declaration", "head"),
            new ParamSpec("NamespaceBody", "rest"));

        // --- 補助非終端: 宣言位置の generic と基底リスト (Member の型 < とは位置分離) ---

        // 型パラメータリスト: < TypeParameter (, TypeParameter)* >。
        // (宣言位置限定: class/struct/interface/record の name 直後。式の < 比較と衝突しない。)
        //
        // 省略可能性は OptionalTypeParameterList (Absent/Present) の 1 箇所に局所化。
        // TypeParameterList 自体は「1要素以上」の空でないリスト (Single/Cons) とし、空規則 (Nil)
        // を持たせない。空規則が複数位置 (Optional の Absent と List の Nil) に散らばると、
        // LALR で両文脈が同一状態にマージして reduce-reduce する (state 66/69 の ',' 上で
        // r0 start ε と Nil の ε が衝突) ため、空は Absent 側のみに集約して重複を除去する。
        spec.AddAbstract("TypeParameter", "AstNode");
        spec.AddSealed("PlainTypeParameter", "TypeParameter").Ctor(
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0));
        spec.AddAbstract("TypeParameterList", "AstNode");
        spec.AddSealed("SingleTypeParameter", "TypeParameterList").Ctor(
            new ParamSpec("Token", "lt", @"<"),
            new ParamSpec("TypeParameter", "head"));
        spec.AddSealed("ConsTypeParameter", "TypeParameterList").Ctor(
            new ParamSpec("TypeParameterList", "list"),
            new ParamSpec("Token", "comma", @","),
            new ParamSpec("TypeParameter", "next"));
        // 型パラメータリストの終端 (>) を独立クラス化: name < T > の > を受理。
        // 末尾の > はリスト要素でなく、リスト全体に > を付与する閉じクラスで受ける。
        // 省略 (ジェネリックなし) は AbsentTypeParameterList の空規則 1 箇所でのみ受理。
        spec.AddAbstract("OptionalTypeParameterList", "AstNode");
        spec.AddSealed("AbsentTypeParameterList", "OptionalTypeParameterList").Ctor();
        spec.AddSealed("PresentTypeParameterList", "OptionalTypeParameterList").Ctor(
            new ParamSpec("TypeParameterList", "list"),
            new ParamSpec("Token", "gt", @">"));

        // 基底リスト: : Type (, Type)*。Nil/Cons で省略可能。
        spec.AddAbstract("BaseList", "AstNode");
        spec.AddSealed("NilBaseList", "BaseList").Ctor();
        spec.AddSealed("SingleBaseType", "BaseList").Ctor(
            new ParamSpec("Token", "colon", @":"),
            new ParamSpec("Type", "head"));
        spec.AddSealed("ConsBaseType", "BaseList").Ctor(
            new ParamSpec("BaseList", "list"),
            new ParamSpec("Token", "comma", @","),
            new ParamSpec("Type", "next"));

        // 型パラメータ制約節 (where T : ...)。class 宣言の { の前に任意個。
        spec.AddAbstract("TypeParameterConstraint", "AstNode");
        spec.AddSealed("ClassTypeConstraint", "TypeParameterConstraint").Ctor(
            new ParamSpec("Token", "kw", "class", priority: 1));
        spec.AddSealed("StructTypeConstraint", "TypeParameterConstraint").Ctor(
            new ParamSpec("Token", "kw", "struct", priority: 1));
        spec.AddSealed("TypeConstraint", "TypeParameterConstraint").Ctor(
            new ParamSpec("Type", "type"));
        spec.AddAbstract("TypeParameterConstraintList", "AstNode");
        spec.AddSealed("SingleTypeParameterConstraint", "TypeParameterConstraintList").Ctor(
            new ParamSpec("TypeParameterConstraint", "head"));
        spec.AddSealed("ConsTypeParameterConstraint", "TypeParameterConstraintList").Ctor(
            new ParamSpec("TypeParameterConstraintList", "list"),
            new ParamSpec("Token", "comma", @","),
            new ParamSpec("TypeParameterConstraint", "next"));
        spec.AddAbstract("TypeParameterConstraintClauseList", "AstNode");
        spec.AddSealed("NilTypeParameterConstraintClause", "TypeParameterConstraintClauseList").Ctor();
        spec.AddSealed("ConsTypeParameterConstraintClause", "TypeParameterConstraintClauseList").Ctor(
            new ParamSpec("Token", "whereKw", "where", priority: 1),
            new ParamSpec("Token", "param", @"[A-Za-z_]\w*", priority: 0),
            new ParamSpec("Token", "colon", @":"),
            new ParamSpec("TypeParameterConstraintList", "constraints"),
            new ParamSpec("TypeParameterConstraintClauseList", "rest"));

        // --- 追加の型宣言 (全具象が Declaration 直接) ---

        // struct: modifiers struct name <T...>? : Base...? { MemberBody }
        spec.AddSealed("StructDeclaration", "Declaration").Ctor(
            new ParamSpec("ModifierList", "modifiers"),
            new ParamSpec("Token", "structKw", "struct", priority: 1),
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
            new ParamSpec("OptionalTypeParameterList", "typeParams"),
            new ParamSpec("BaseList", "bases"),
            new ParamSpec("TypeParameterConstraintClauseList", "constraints"),
            new ParamSpec("Token", "lb", @"\{"),
            new ParamSpec("MemberBody", "body"),
            new ParamSpec("Token", "rb", @"\}"));

        // interface: modifiers interface name <T...>? : Base...? { MemberBody }
        spec.AddSealed("InterfaceDeclaration", "Declaration").Ctor(
            new ParamSpec("ModifierList", "modifiers"),
            new ParamSpec("Token", "interfaceKw", "interface", priority: 1),
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
            new ParamSpec("OptionalTypeParameterList", "typeParams"),
            new ParamSpec("BaseList", "bases"),
            new ParamSpec("TypeParameterConstraintClauseList", "constraints"),
            new ParamSpec("Token", "lb", @"\{"),
            new ParamSpec("MemberBody", "body"),
            new ParamSpec("Token", "rb", @"\}"));

        // enum: modifiers enum name (: Type)? { EnumMemberBody }
        spec.AddSealed("EnumDeclaration", "Declaration").Ctor(
            new ParamSpec("ModifierList", "modifiers"),
            new ParamSpec("Token", "enumKw", "enum", priority: 1),
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
            new ParamSpec("BaseList", "bases"),
            new ParamSpec("Token", "lb", @"\{"),
            new ParamSpec("EnumMemberBody", "body"),
            new ParamSpec("Token", "rb", @"\}"));

        // delegate: modifiers delegate Type name ( ParameterList ) ;
        spec.AddSealed("DelegateDeclaration", "Declaration").Ctor(
            new ParamSpec("ModifierList", "modifiers"),
            new ParamSpec("Token", "delegateKw", "delegate", priority: 1),
            new ParamSpec("Type", "returnType"),
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
            new ParamSpec("Token", "lp", @"\("),
            new ParamSpec("ParameterList", "parameters"),
            new ParamSpec("Token", "rp", @"\)"),
            new ParamSpec("Token", "semi", @";"));

        // record: modifiers record name ( ParameterList )? { MemberBody }
        spec.AddSealed("RecordDeclaration", "Declaration").Ctor(
            new ParamSpec("ModifierList", "modifiers"),
            new ParamSpec("Token", "recordKw", "record", priority: 1),
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
            new ParamSpec("Token", "lp", @"\("),
            new ParamSpec("ParameterList", "parameters"),
            new ParamSpec("Token", "rp", @"\)"),
            new ParamSpec("BaseList", "bases"),
            new ParamSpec("Token", "lb", @"\{"),
            new ParamSpec("MemberBody", "body"),
            new ParamSpec("Token", "rb", @"\}"));

        // record struct: modifiers record struct name ( ParameterList )? { MemberBody }
        spec.AddSealed("RecordStructDeclaration", "Declaration").Ctor(
            new ParamSpec("ModifierList", "modifiers"),
            new ParamSpec("Token", "recordKw", "record", priority: 1),
            new ParamSpec("Token", "structKw", "struct", priority: 1),
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
            new ParamSpec("Token", "lp", @"\("),
            new ParamSpec("ParameterList", "parameters"),
            new ParamSpec("Token", "rp", @"\)"),
            new ParamSpec("Token", "lb", @"\{"),
            new ParamSpec("MemberBody", "body"),
            new ParamSpec("Token", "rb", @"\}"));

        // file-scoped namespace: namespace QualifiedName ;
        spec.AddSealed("FileScopedNamespaceDeclaration", "Declaration").Ctor(
            new ParamSpec("Token", "namespaceKw", "namespace", priority: 1),
            new ParamSpec("QualifiedName", "name"),
            new ParamSpec("Token", "semi", @";"));

        // global using: global using QualifiedName ; / global using name = Type ;
        spec.AddSealed("GlobalUsingDirective", "Declaration").Ctor(
            new ParamSpec("Token", "globalKw", "global", priority: 1),
            new ParamSpec("Token", "usingKw", "using", priority: 1),
            new ParamSpec("QualifiedName", "ns"),
            new ParamSpec("Token", "semi", @";"));
        spec.AddSealed("GlobalUsingAliasDirective", "Declaration").Ctor(
            new ParamSpec("Token", "globalKw", "global", priority: 1),
            new ParamSpec("Token", "usingKw", "using", priority: 1),
            new ParamSpec("Token", "alias", @"[A-Za-z_]\w*", priority: 0),
            new ParamSpec("Token", "eq", @"="),
            new ParamSpec("Type", "target"),
            new ParamSpec("Token", "semi", @";"));

        // extern alias: extern alias name ;
        spec.AddSealed("ExternAliasDirective", "Declaration").Ctor(
            new ParamSpec("Token", "externKw", "extern", priority: 1),
            new ParamSpec("Token", "aliasKw", "alias", priority: 1),
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
            new ParamSpec("Token", "semi", @";"));

        // --- enum メンバ ---
        spec.AddAbstract("EnumMemberBody", "AstNode");
        spec.AddSealed("NilEnumMember", "EnumMemberBody").Ctor();
        spec.AddSealed("SingleEnumMember", "EnumMemberBody").Ctor(
            new ParamSpec("EnumMember", "head"));
        spec.AddSealed("ConsEnumMember", "EnumMemberBody").Ctor(
            new ParamSpec("EnumMember", "head"),
            new ParamSpec("Token", "comma", @","),
            new ParamSpec("EnumMemberBody", "rest"));
        spec.AddAbstract("EnumMember", "AstNode");
        spec.AddSealed("EnumMemberDecl", "EnumMember").Ctor(
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0));
        spec.AddSealed("EnumMemberInitDecl", "EnumMember").Ctor(
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
            new ParamSpec("Token", "eq", @"="),
            new ParamSpec("Expression", "value"));
    }

private static void AddMembers(GrammarSpec spec)
{
    AddParameterList(spec);

    // AccessorList 補助非終端 (本メソッド内で定義)。
    // { } 内の get/set/init/add/remove アクセサの並び。空も許す (auto-property の { get; set; } 等)。
    spec.AddAbstract("AccessorList", "AstNode");
    spec.AddSealed("NilAccessorList", "AccessorList").Ctor();
    spec.AddSealed("ConsAccessor", "AccessorList").Ctor(
        new ParamSpec("Accessor", "head"),
        new ParamSpec("AccessorList", "rest"));

    // アクセサ本体: { BlockStatement } または ; 。get/set/init/add/remove はキーワード (priority:1)。
    // ※本体に Statement を使うと、Statement -> ';' (EmptyStatement) の還元経路が存在し、
    //   get; の ';' を (a) リテラル ';' として GetAccessorEmpty の一部、(b) EmptyStatement 経由で
    //   GetAccessor の body、の 2通りに解釈でき reduce-reduce が発生する。
    //   これを避けるため、ブロック本体は Statement ではなく BlockStatement を直接要求し、
    //   ';' の還元経路を accessor 文脈から除去。';' は *Empty (リテラル) のみで受理。
    spec.AddAbstract("Accessor", "AstNode");
    // body は Statement。get; は body=EmptyStatement (Statement->';') で一意に受理。
    // ※具象 BlockStatement を body 型にすると BlockStatement を LHS とする規則が無く
    //   ASTF003 未定義になる（AstFirst で具象は親抽象が LHS）。なので抽象 Statement を使う。
    // ※かつて GetAccessorEmpty (get ';') を別規則にしていたが、これと GetAccessor(body=EmptyStatement)
    //   の reduce-reduce (state 363) になるため廃止。get; は EmptyStatement 経路で統一。
    // 式ボディ (get => expr;) は *ExprBody 系で別規則化。
    spec.AddSealed("GetAccessor", "Accessor").Ctor(
        new ParamSpec("Token", "kw", "get", priority: 1),
        new ParamSpec("Statement", "body"));
    spec.AddSealed("GetAccessorExprBody", "Accessor").Ctor(
        new ParamSpec("Token", "kw", "get", priority: 1),
        new ParamSpec("Token", "arrow", @"=>"),
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "semi", @";"));
    spec.AddSealed("SetAccessor", "Accessor").Ctor(
        new ParamSpec("Token", "kw", "set", priority: 1),
        new ParamSpec("Statement", "body"));
    spec.AddSealed("SetAccessorExprBody", "Accessor").Ctor(
        new ParamSpec("Token", "kw", "set", priority: 1),
        new ParamSpec("Token", "arrow", @"=>"),
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "semi", @";"));
    spec.AddSealed("InitAccessor", "Accessor").Ctor(
        new ParamSpec("Token", "kw", "init", priority: 1),
        new ParamSpec("Statement", "body"));
    spec.AddSealed("InitAccessorExprBody", "Accessor").Ctor(
        new ParamSpec("Token", "kw", "init", priority: 1),
        new ParamSpec("Token", "arrow", @"=>"),
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "semi", @";"));
    spec.AddSealed("AddAccessor", "Accessor").Ctor(
        new ParamSpec("Token", "kw", "add", priority: 1),
        new ParamSpec("Statement", "body"));
    spec.AddSealed("AddAccessorExprBody", "Accessor").Ctor(
        new ParamSpec("Token", "kw", "add", priority: 1),
        new ParamSpec("Token", "arrow", @"=>"),
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "semi", @";"));
    spec.AddSealed("RemoveAccessor", "Accessor").Ctor(
        new ParamSpec("Token", "kw", "remove", priority: 1),
        new ParamSpec("Statement", "body"));
    spec.AddSealed("RemoveAccessorExprBody", "Accessor").Ctor(
        new ParamSpec("Token", "kw", "remove", priority: 1),
        new ParamSpec("Token", "arrow", @"=>"),
        new ParamSpec("Expression", "expr"),
        new ParamSpec("Token", "semi", @";"));

    // === MemberDeclaration 抽象 (1段階: 全具象が MemberDeclaration 直接) ===
    spec.AddAbstract("MemberDeclaration", "AstNode");

    // フィールド (既存)
    spec.AddSealed("FieldDeclaration", "MemberDeclaration").Ctor(
        new ParamSpec("ModifierList", "modifiers"),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "semi", @";"));
    // メソッド (既存)
    spec.AddSealed("MethodDeclaration", "MemberDeclaration").Ctor(
        new ParamSpec("ModifierList", "modifiers"),
        new ParamSpec("Type", "returnType"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("ParameterList", "parameters"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));

    // プロパティ: mods Type name { AccessorList }
    spec.AddSealed("PropertyDeclaration", "MemberDeclaration").Ctor(
        new ParamSpec("ModifierList", "modifiers"),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "lb", @"\{"),
        new ParamSpec("AccessorList", "accessors"),
        new ParamSpec("Token", "rb", @"\}"));

    // インデクサ: mods Type this [ ParameterList ] { AccessorList }
    // this/[/]/{/} は最後の終端でない位置にあるが、これらは演算子ではないため
    // shift-reduce を起こさない (括弧類は構造マーカー)。precedence 属性不要。
    spec.AddSealed("IndexerDeclaration", "MemberDeclaration").Ctor(
        new ParamSpec("ModifierList", "modifiers"),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "thisKw", "this", priority: 1),
        new ParamSpec("Token", "lb", @"\["),
        new ParamSpec("ParameterList", "parameters"),
        new ParamSpec("Token", "rb", @"\]"),
        new ParamSpec("Token", "lbrace", @"\{"),
        new ParamSpec("AccessorList", "accessors"),
        new ParamSpec("Token", "rbrace", @"\}"));

    // イベント (プロパティ形式): mods event Type name { AccessorList }
    // event はキーワード (priority:1) で修飾子 IdentifierExpr と弁別。
    spec.AddSealed("EventDeclaration", "MemberDeclaration").Ctor(
        new ParamSpec("ModifierList", "modifiers"),
        new ParamSpec("Token", "eventKw", "event", priority: 1),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "lb", @"\{"),
        new ParamSpec("AccessorList", "accessors"),
        new ParamSpec("Token", "rb", @"\}"));

    // イベント (フィールド形式): mods event Type name ; / mods event Type name = Expression ;
    // 省略可能初期化子 (= Expression) を 2 Ctor で表現。name 直後の lookahead が
    // = なら初期化子付き、; なら無初期化子で補完的に解決 (reduce-reduce なし)。
    spec.AddSealed("EventFieldDeclaration", "MemberDeclaration").Ctor(
        new ParamSpec("ModifierList", "modifiers"),
        new ParamSpec("Token", "eventKw", "event", priority: 1),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "semi", @";"));
    spec.AddSealed("EventFieldWithInitDeclaration", "MemberDeclaration").Ctor(
        new ParamSpec("ModifierList", "modifiers"),
        new ParamSpec("Token", "eventKw", "event", priority: 1),
        new ParamSpec("Type", "type"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "eq", @"="),
        new ParamSpec("Expression", "init"),
        new ParamSpec("Token", "semi", @";"));

    // 演算子オーバーロード (operator + 等) は意図的に除外。
    // op に (\+|-|!|~|...) の alternation を使うと、Lexer の最長一致/優先度で個別の
    // 演算子トークン (\+ や - 等) より先にマッチしてしまい、二項演算子の + が解析不能になる。
    // implicit/explicit operator (変換演算子) は op 記号を持たないので影響なし (下記で保持)。
    // ※代替: op を個別トークン (\+ や -) に分割すれば復活可能だが OperatorXxxDeclaration が 20 規則に膨らむ。

    // 変換演算子: mods (implicit|explicit) operator Type ( ParameterList ) Statement
    // implicit/explicit はキーワード (priority:1) で識別子と弁別。2 Ctor。
    spec.AddSealed("ImplicitOperatorDeclaration", "MemberDeclaration").Ctor(
        new ParamSpec("ModifierList", "modifiers"),
        new ParamSpec("Token", "implicitKw", "implicit", priority: 1),
        new ParamSpec("Token", "operatorKw", "operator", priority: 1),
        new ParamSpec("Type", "targetType"),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("ParameterList", "parameters"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));
    spec.AddSealed("ExplicitOperatorDeclaration", "MemberDeclaration").Ctor(
        new ParamSpec("ModifierList", "modifiers"),
        new ParamSpec("Token", "explicitKw", "explicit", priority: 1),
        new ParamSpec("Token", "operatorKw", "operator", priority: 1),
        new ParamSpec("Type", "targetType"),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("ParameterList", "parameters"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));

    // コンストラクタ: mods name ( ParameterList ) (: base ( ArgumentList ))? Statement
    // 省略可能なコンストラクタ初期化子 (: base (...)) を 2 Ctor で表現。
    // ) 直後の lookahead が : なら初期化子付き、Statement 開始トーク子なら無しで補完的 (reduce-reduce なし)。
    spec.AddSealed("ConstructorDeclaration", "MemberDeclaration").Ctor(
        new ParamSpec("ModifierList", "modifiers"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("ParameterList", "parameters"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));
    spec.AddSealed("ConstructorWithInitializerDeclaration", "MemberDeclaration").Ctor(
        new ParamSpec("ModifierList", "modifiers"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("ParameterList", "parameters"),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Token", "colon", @":"),
        new ParamSpec("Token", "baseKw", "base", priority: 1),
        new ParamSpec("Token", "initLp", @"\("),
        new ParamSpec("ArgumentList", "args"),
        new ParamSpec("Token", "initRp", @"\)"),
        new ParamSpec("Statement", "body"));

    // スタティックコンストラクタ: mods static name ( ) Statement
    // (空パラメータ) を NilParameterList 経由でなく () リテラルで受理し、
    // ConstructorDeclaration と区別 (後者は常に ParameterList を要求)。name の前に static が
    // ModifierList に取り込まれる分岐と StaticConstructorDeclaration の分岐が並存するが、
    // 静的コンストラクタは引数を持たないため () で識別可能。
    spec.AddSealed("StaticConstructorDeclaration", "MemberDeclaration").Ctor(
        new ParamSpec("ModifierList", "modifiers"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));

    // ファイナライザ: ~ name ( ) Statement
    spec.AddSealed("FinalizerDeclaration", "MemberDeclaration").Ctor(
        new ParamSpec("Token", "tilde", @"~"),
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("Token", "rp", @"\)"),
        new ParamSpec("Statement", "body"));

    // クラス本体リスト (左再帰 Cons/Nil)
    spec.AddAbstract("MemberBody", "AstNode");
    spec.AddSealed("NilMemberBody", "MemberBody").Ctor();
    spec.AddSealed("ConsMemberDecl", "MemberBody").Ctor(
        new ParamSpec("MemberDeclaration", "head"),
        new ParamSpec("MemberBody", "rest"));
}

private static void AddAttributes(GrammarSpec spec)
{
    // 属性セクション ([ ... ]) のリスト (宣言/メンバの先頭に 0 個以上連続)。
    // base=AstNode のリスト非終端 (抽象は介在させない → 補助のみ)。
    spec.AddAbstract("AttributeSectionList", "AstNode");
    spec.AddSealed("NilAttributeSection", "AttributeSectionList").Ctor();
    spec.AddSealed("ConsAttributeSection", "AttributeSectionList").Ctor(
        new ParamSpec("AttributeSection", "head"),
        new ParamSpec("AttributeSectionList", "rest"));

    // 単一の属性セクション [ (target :)? Attribute (, Attribute)* ]
    // 補助非終端ではなく、属性セクションは独立の抽象。
    spec.AddAbstract("AttributeSection", "AstNode");
    // [ Attribute, Attribute, ... ] (target 省略)
    spec.AddSealed("AttributeSectionNoTarget", "AttributeSection").Ctor(
        new ParamSpec("Token", "lb", @"\["),
        new ParamSpec("AttributeList", "attributes"),
        new ParamSpec("Token", "rb", @"\]"));
    // [ target : Attribute, Attribute, ... ] (target 付き)
    // target は assembly/module/type/method/return/field/param/property/event (文脈キーワード)。
    // ※ alternation "(assembly|...|return|...)" を1つの [Pattern] にすると、return 単独のトークン
    //   (ReturnStatement/ReturnExprStatement の "return", priority:1) と同じ priority で Lexer が衝突し、
    //   先の TokenId (属性ターゲット alternation) に "return" を飲み込まれて return 文が認識されなくなる。
    //   よって target は identifier [A-Za-z_]\w* (priority:0) として受け取る。属性セクションは現在
    //   到達不能 (ASTF002, 宣言への属性付与未統合) なので [return: ...] の return トークン不一致は
    //   実害なく、属性統合時に Parser 弁別へ整理する。
    // 最後の終端でない ":" の直前に target が来るため [Precedence] を付与して shift-reduce を解決する。
    spec.AddSealed("AttributeSectionWithTarget", "AttributeSection", precedence: PrecPostfix).Ctor(
        new ParamSpec("Token", "lb", @"\["),
        new ParamSpec("Token", "target", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "colon", @":"),
        new ParamSpec("AttributeList", "attributes"),
        new ParamSpec("Token", "rb", @"\]"));

    // 属性リスト (1 個以上、カンマ区切。左再帰 Cons/Nil 不可 → 単一 + Cons)。
    spec.AddAbstract("AttributeList", "AstNode");
    spec.AddSealed("SingleAttribute", "AttributeList").Ctor(new ParamSpec("Attribute", "head"));
    spec.AddSealed("ConsAttribute", "AttributeList").Ctor(
        new ParamSpec("AttributeList", "list"),
        new ParamSpec("Token", "comma", @","),
        new ParamSpec("Attribute", "head"));

    // 属性本体: QualifiedName ( AttributeArgumentList )?
    // base=AstNode の補助具象 (Attribute 抽象は介在させない → 直接 1 つに。簡素化のため抽象化せず具象のみ)。
    spec.AddAbstract("Attribute", "AstNode");
    spec.AddSealed("AttributeDecl", "Attribute").Ctor(new ParamSpec("QualifiedName", "name"));
    spec.AddSealed("AttributeDeclWithArgs", "Attribute").Ctor(
        new ParamSpec("QualifiedName", "name"),
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("AttributeArgumentList", "args"),
        new ParamSpec("Token", "rp", @"\)"));

    // 属性引数リスト (カンマ区切、0 個以上)。各引数は位置引数 (Expression) または
    // 名前付き引数 (name = Expression)。同一位置で両代替が並ぶが、
    // 先読みが '=' なら Named へ shift、',' か ')' なら Positional へ reduce となり reduce-reduce は出ない。
    // Named は最後の終端でない name を持つため [Precedence] を付けて '=' への shift を確定させる。
    spec.AddAbstract("AttributeArgumentList", "AstNode");
    spec.AddSealed("NilAttributeArgument", "AttributeArgumentList").Ctor();
    spec.AddSealed("ConsAttributeArgument", "AttributeArgumentList").Ctor(
        new ParamSpec("AttributeArgumentList", "list"),
        new ParamSpec("Token", "comma", @","),
        new ParamSpec("AttributeArg", "arg"));

    // 最初の引数は Nil/Cons の構造に載せるため、AttributeArgumentList を直接使うと
    // "先頭が空 (Nil)" で "[X()]" と "[X(a)]" を両立できる。
    // しかし先頭引数のみ特別扱いすると ConsAttributeArgument の先頭位置と重なるため、
    // 先頭も Cons と同じ形 (list=Nil, comma 無し) にする代わりに SingleAttributeArgument を置く。
    // → これにより最初の要素は AttributeArg 1 個、以降は (, AttributeArg)* の Cons。
    spec.AddSealed("SingleAttributeArgument", "AttributeArgumentList").Ctor(new ParamSpec("AttributeArg", "arg"));

    // 個別の属性引数 (位置 / 名前付き)。base=AstNode 補助抽象。
    spec.AddAbstract("AttributeArg", "AstNode");
    // 位置引数: Expression 単体。precedence 0 (Expression の還元を妨げない)。
    spec.AddSealed("PositionalAttributeArg", "AttributeArg").Ctor(new ParamSpec("Expression", "value"));
    // 名前付き引数: name = Expression。name は最後の終端でないため [Precedence] で '=' shift を確定。
    spec.AddSealed("NamedAttributeArg", "AttributeArg", precedence: PrecPostfix).Ctor(
        new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
        new ParamSpec("Token", "eq", @"="),
        new ParamSpec("Expression", "value"));
}

private static void AddPreProcessor(GrammarSpec spec)
{
    // --- プリプロセス ディレクティブ (abstract) ---
    // CSharpCompilationUnit の要素の一つ。Declaration と並列でぶら下がる。
    spec.AddAbstract("PPDirective", "AstNode");

    // --- PP 式 (補助 abstract)。#if / #elif の条件式。通常の Expression とは別ツリー。 ---
    spec.AddAbstract("PPExpr", "AstNode");
    // primary: シンボル (true/false の代わりに任意の条件付きシンボル名)
    spec.AddSealed("PPSymbolExpr", "PPExpr").Ctor(
        new ParamSpec("Token", "symbol", @"[A-Za-z_]\w*", priority: 0));
    // 前置論理否定: ! PPExpr  (演算子が最後の終端でない → [Precedence] 必須)
    spec.AddSealed("PPNotExpr", "PPExpr", precedence: PrecUnary).Ctor(
        new ParamSpec("Token", "bang", @"!"),
        new ParamSpec("PPExpr", "operand"));
    // 二項 PP 演算子。優先度は通常の式に合わせる (パターン共有のため整合)。
    // PPEquality: PPExpr (==|!=) PPExpr
    spec.AddSealed("PPEqExpr", "PPExpr", precedence: PrecEquality).Ctor(
        new ParamSpec("PPExpr", "left"),
        new ParamSpec("Token", "op", @"=="),
        new ParamSpec("PPExpr", "right"));
    spec.AddSealed("PPNeExpr", "PPExpr", precedence: PrecEquality).Ctor(
        new ParamSpec("PPExpr", "left"),
        new ParamSpec("Token", "op", @"!="),
        new ParamSpec("PPExpr", "right"));
    // PPAnd: PPExpr && PPExpr
    spec.AddSealed("PPAndExpr", "PPExpr", precedence: PrecConditionalAnd).Ctor(
        new ParamSpec("PPExpr", "left"),
        new ParamSpec("Token", "op", @"&&"),
        new ParamSpec("PPExpr", "right"));
    // PPOr: PPExpr || PPExpr
    spec.AddSealed("PPOrExpr", "PPExpr", precedence: PrecConditionalOr).Ctor(
        new ParamSpec("PPExpr", "left"),
        new ParamSpec("Token", "op", @"\|\|"),
        new ParamSpec("PPExpr", "right"));
    // PP 式の括弧 (一次式の括弧と区別するため独立クラス)
    spec.AddSealed("PPParenExpr", "PPExpr").Ctor(
        new ParamSpec("Token", "lp", @"\("),
        new ParamSpec("PPExpr", "inner"),
        new ParamSpec("Token", "rp", @"\)"));

    // --- 各ディレクティブ具象 (PPDirective の直接の子)。---
    // # は priority:1 の終端。キーワード (define/undef/if/elif/else/endif/region/endregion/
    // pragma/line/nullable/error/warning) も priority:1 (識別子 priority:0 と衝突するため)。

    // #define symbol
    spec.AddSealed("PPDefine", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwDefine", "define", priority: 1),
        new ParamSpec("Token", "symbol", @"[A-Za-z_]\w*", priority: 0));
    // #undef symbol
    spec.AddSealed("PPUndef", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwUndef", "undef", priority: 1),
        new ParamSpec("Token", "symbol", @"[A-Za-z_]\w*", priority: 0));

    // #if PPExpr
    spec.AddSealed("PPIf", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwIf", "if", priority: 1),
        new ParamSpec("PPExpr", "condition"));
    // #elif PPExpr
    spec.AddSealed("PPElif", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwElif", "elif", priority: 1),
        new ParamSpec("PPExpr", "condition"));
    // #else
    spec.AddSealed("PPElse", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwElse", "else", priority: 1));
    // #endif
    spec.AddSealed("PPEndIf", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwEndif", "endif", priority: 1));

    // #region text   (text は行末までの任意の識別子連続; 簡易化で単一識別子)
    spec.AddSealed("PPRegion", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwRegion", "region", priority: 1),
        new ParamSpec("Token", "text", @"[A-Za-z_]\w*", priority: 0));
    // #endregion text
    spec.AddSealed("PPEndRegion", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwEndregion", "endregion", priority: 1),
        new ParamSpec("Token", "text", @"[A-Za-z_]\w*", priority: 0));

    // #pragma warning ...   (warning/checksum を 1 終端で弁別)
    spec.AddSealed("PPPragmaWarning", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwPragma", "pragma", priority: 1),
        new ParamSpec("Token", "kwWarning", "warning", priority: 1),
        new ParamSpec("Token", "text", @"[A-Za-z_]\w*", priority: 0));
    spec.AddSealed("PPPragmaChecksum", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwPragma", "pragma", priority: 1),
        new ParamSpec("Token", "kwChecksum", "checksum", priority: 1),
        new ParamSpec("Token", "text", @"[A-Za-z_]\w*", priority: 0));

    // #line ...   (default/hidden/数値 は識別子で代用)
    spec.AddSealed("PPLine", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwLine", "line", priority: 1),
        new ParamSpec("Token", "text", @"[A-Za-z_]\w*", priority: 0));

    // #nullable enable|disable|warnings|annotations
    spec.AddSealed("PPNullableEnable", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwNullable", "nullable", priority: 1),
        new ParamSpec("Token", "kwEnable", "enable", priority: 1));
    spec.AddSealed("PPNullableDisable", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwNullable", "nullable", priority: 1),
        new ParamSpec("Token", "kwDisable", "disable", priority: 1));
    spec.AddSealed("PPNullableWarnings", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwNullable", "nullable", priority: 1),
        new ParamSpec("Token", "kwWarnings", "warnings", priority: 1));
    spec.AddSealed("PPNullableAnnotations", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwNullable", "nullable", priority: 1),
        new ParamSpec("Token", "kwAnnotations", "annotations", priority: 1));

    // #error text
    spec.AddSealed("PPError", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwError", "error", priority: 1),
        new ParamSpec("Token", "text", @"[A-Za-z_]\w*", priority: 0));
    // #warning text
    spec.AddSealed("PPWarning", "PPDirective").Ctor(
        new ParamSpec("Token", "hash", @"#", priority: 1),
        new ParamSpec("Token", "kwWarning2", "warning", priority: 1),
        new ParamSpec("Token", "text", @"[A-Za-z_]\w*", priority: 0));

    // --- CSharpCompilationUnit (Root) の要素に PPDirective を追加。---
    // 既存 ConsDeclaration と並列。base は直接の親 = Root。
    // # で始まるため Declaration と first-終端が異なり reduce-reduce は出ない。
    spec.AddSealed("ConsPP", "CSharpCompilationUnit").Ctor(
        new ParamSpec("PPDirective", "directive"),
        new ParamSpec("CSharpCompilationUnit", "rest"));
}

    private static string Cap(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);
}
