using ManiaScriptSharp.DocH.Blocks;
using System.Collections.Immutable;

namespace ManiaScriptSharp.DocH.Tests.Mocks;

public class MockMajorHBlock : MajorHBlock
{
    protected internal override ImmutableArray<Func<HGeneral>> HGenerals { get; }

	public MockMajorHBlock(ImmutableArray<Func<HGeneral>> hGenerals)
	{
		HGenerals = hGenerals;
    }

    public MockMajorHBlock() : this(ImmutableArray.Create<Func<HGeneral>>())
    {
        
    }
}
