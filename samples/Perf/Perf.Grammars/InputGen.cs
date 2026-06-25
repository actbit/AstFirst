using System.Text;

namespace Perf.Grammars;

/// <summary>各文法パターンの正当な入力を生成 (ベンチマーク用)。パターンごとに構造が異なる。</summary>
public static class InputGen
{
    /// <summary>DeepPrec: tokenCount 個の数値を20種の演算子で結ぶ。</summary>
    public static string DeepPrec(int tokenCount)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < tokenCount; i++)
        {
            if (i > 0)
                sb.Append(' ').Append(OpTable.BinaryOps[(i - 1) % OpTable.BinaryOps.Length].Lit).Append(' ');
            sb.Append(i % 1000);
        }
        return sb.ToString();
    }

    /// <summary>WideRules: 100 種の文 (s0;..s99;) を repeat 回繰り返す。</summary>
    public static string WideRules(int repeat)
    {
        var sb = new StringBuilder();
        for (int r = 0; r < repeat; r++)
            for (int i = 0; i < 100; i++)
                sb.Append('s').Append(i).Append("; ");
        return sb.ToString();
    }

    /// <summary>ManyTokens: tokenCount 個のキーワード/識別子を交互に。</summary>
    public static string ManyTokens(int tokenCount)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < tokenCount; i++)
        {
            if (i > 0) sb.Append(' ');
            if ((i & 1) == 0) sb.Append("kw").Append(i % 100);
            else sb.Append("var").Append(i);
        }
        return sb.ToString();
    }

    /// <summary>DeepNest: 深さ depth の括弧で 1 を包む。</summary>
    public static string DeepNest(int depth)
        => new string('(', depth) + "1" + new string(')', depth);

    /// <summary>MegaLang: 宣言 + 代入式文 を repeat 回繰り返す。</summary>
    public static string MegaLang(int repeat)
    {
        var sb = new StringBuilder();
        for (int r = 0; r < repeat; r++)
        {
            sb.Append("decl").Append(r % 30).Append(" ; ");
            sb.Append("x = ").Append(r % 10).Append(" + ").Append((r + 1) % 10).Append(" * 2 ; ");
        }
        return sb.ToString();
    }

    /// <summary>C#: クラス (generic フィールド/メソッド/if/while/式) を repeat 個並べる。
    /// generic は Member の型のみ、ローカルは var (技術1)。</summary>
    public static string CSharp(int repeat)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < repeat; i++)
        {
            sb.Append("class C").Append(i).Append(" { ");
            sb.Append("int f").Append(i).Append("; ");
            sb.Append("int[] g").Append(i).Append("; ");
            sb.Append("int M").Append(i).Append("(int a, int b) { ");
            sb.Append("var x = a + b * 2; ");
            sb.Append("if (x > 0) { x = x - 1; } ");
            sb.Append("while (x > 0) { x = x - 1; } ");
            sb.Append("return x; ");
            sb.Append("} ");
            sb.Append("} ");
        }
        return sb.ToString();
    }
}

/// <summary>ベンチマークの入力サイズ (各パターンで Small/Medium/Large に対応する生成パラメータが異なる)。</summary>
public enum BenchSize { Small, Medium, Large }
