using Xunit;
using Weaver.Core;

namespace Weaver.Core.Tests;

public class SkeletonTests
{
    [Fact]
    public void Meta_BundleMatchesContract()
    {
        Assert.Equal("com.infinityball.laneweaver", GameMeta.BundleIdentifier);
        Assert.Equal("Lane Weaver", GameMeta.Title);
    }

    [Fact]
    public void Meta_LaneGeometrySensible()
    {
        Assert.Equal(3, GameMeta.DefaultLaneCount);
        Assert.True(GameMeta.StandardLaneWidth >= 2.5f);
    }
}
