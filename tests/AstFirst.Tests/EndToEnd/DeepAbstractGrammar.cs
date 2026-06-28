using AstFirst;

namespace AstFirst.Tests.EndToEnd.DeepAbstractTest;

// 多段階中間抽象 (DRoot → DMid1 → DMid2 → DLeaf) の E2E 検証用文法。
// PassThrough 単位規則が連鎖し (DRoot→DMid1, DMid1→DMid2)、DLeaf の値が DRoot まで伝播する。

[Grammar]
[Skip(@"\s+")]
public abstract partial class DRoot : AstNode { }

public abstract partial class DMid1 : DRoot { }

public abstract partial class DMid2 : DMid1 { }

public sealed partial class DLeaf : DMid2
{
    [Rule]
    public static void Make([Token(@"[a-z]+")] Token text) { }
}
