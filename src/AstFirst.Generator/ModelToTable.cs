using AstFirst.Core.Parsing;

namespace AstFirst.Generator;

/// <summary>GrammarModel から LALR(1) 解析テーブルを構築する。</summary>
public static class ModelToTable
{
    public static LalrTable Build(GrammarModel model)
    {
        var g = ModelToGrammar.Build(model);
        var first = new FirstSet(g);
        var auto = Lr0AutomatonBuilder.Build(g);
        var la = new LalrLookahead(g, auto, first);
        return LalrTableBuilder.Build(g, auto, la);
    }

    /// <summary>テーブルと元の Grammar を両方返す (生成コードで両方使う)。</summary>
    public static (Grammar grammar, LalrTable table) BuildWithGrammar(GrammarModel model)
    {
        var g = ModelToGrammar.Build(model);
        var first = new FirstSet(g);
        var auto = Lr0AutomatonBuilder.Build(g);
        var la = new LalrLookahead(g, auto, first);
        return (g, LalrTableBuilder.Build(g, auto, la));
    }
}
