using AstFirst.Tests.EndToEnd.DeepAbstractTest;

namespace AstFirst.Tests.EndToEnd;

/// <summary>多段階中間抽象 (DRoot → DMid1 → DMid2 → DLeaf) の E2E 検証。
/// PassThrough 単位規則が複数連鎖しても、具象 (DLeaf) の値が開始記号 (DRoot) まで伝播すること。</summary>
public class DeepAbstractEndToEndTests
{
    [Fact]
    public void ParsesThroughMultiLevelAbstractChain()
    {
        // PassThrough 単位規則: DRoot → DMid1 → DMid2 → DLeaf(Make)。値は DLeaf のまま伝播。
        var result = DRootParser.Parse("abc");
        Assert.False(result.HasErrors);
        Assert.IsType<DLeaf>(result.Ast);
    }

    [Fact]
    public void EmptyInputIsRejected()
    {
        // DLeaf は [a-z]+ を要求するので、空入力は受理されない。
        var result = DRootParser.Parse("");
        Assert.True(result.HasErrors);
    }
}
