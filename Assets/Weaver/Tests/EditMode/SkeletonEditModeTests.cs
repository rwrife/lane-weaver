using NUnit.Framework;
using Weaver.Core;

namespace Weaver.Tests.EditMode
{
    public class SkeletonEditModeTests
    {
        [Test]
        public void BundleIdentifier_MatchesFleetPrefix()
        {
            Assert.That(GameMeta.BundleIdentifier, Is.EqualTo("com.infinityball.laneweaver"));
        }

        [Test]
        public void LaneCount_HasThreeStandardLanes()
        {
            Assert.That(GameMeta.DefaultLaneCount, Is.EqualTo(3));
        }
    }
}
