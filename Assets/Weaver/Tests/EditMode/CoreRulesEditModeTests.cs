using System.IO;
using NUnit.Framework;
using Weaver.Core;

namespace Weaver.Tests.EditMode
{
    public sealed class CoreRulesEditModeTests
    {
        [Test]
        public void SeedReplaysTraffic()
        {
            var a = new TrafficScheduler(123u, 3, 0.5f);
            var b = new TrafficScheduler(123u, 3, 0.5f);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.Next(i), b.Next(i));
        }

        [Test]
        public void LaneTransitionClamps()
        {
            var motion = new LaneMotion(3, 1);
            motion.Shift(1);
            motion.Shift(1);
            motion.Tick(0.1f, 8f);
            Assert.AreEqual(2, motion.TargetLane);
            Assert.AreEqual(1.8f, motion.Position, 0.001f);
            motion.Tick(1f, 8f);
            Assert.AreEqual(2f, motion.Position);
        }

        [Test]
        public void SpeedRampsAndClamps()
        {
            Assert.AreEqual(22f, RoadMath.Speed(10f, 18f, 0.4f, 42f));
            Assert.AreEqual(42f, RoadMath.Speed(120f, 18f, 0.4f, 42f));
        }

        [Test]
        public void CleanPassBuildsAndCollisionResets()
        {
            var score = new ScoreState();
            score.CleanPass(10);
            score.CleanPass(10);
            Assert.AreEqual(30, score.Score);
            Assert.AreEqual(3, score.Multiplier);
            score.Collision();
            Assert.AreEqual(1, score.Multiplier);
        }

        [Test]
        public void RecordsRoundTripAndTamperDetection()
        {
            var book = new RecordBook();
            book.Runs.Add(new RunRecord { Route = "Desert Canyon", Score = 500 });
            string json = RecordCodec.Export(book);
            Assert.AreEqual(500, RecordCodec.Import(json).Runs[0].Score);
            Assert.AreNotEqual(json, json.Replace("500", "999"));
            Assert.Throws<InvalidDataException>(() => RecordCodec.Import(json.Replace("500", "999")));
        }
    }
}
