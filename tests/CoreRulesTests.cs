using System;
using System.IO;
using Xunit;

namespace Weaver.Core.Tests
{
    public class CoreRulesTests
    {
        [Fact]
        public void SeededSchedulerReproducesSequence()
        {
            var a = new TrafficScheduler(1234, 3, 0.5f);
            var b = new TrafficScheduler(1234, 3, 0.5f);
            for (int i = 0; i < 200; i++)
                Assert.Equal(a.Next(i * 0.5f), b.Next(i * 0.5f));

            var c = new TrafficScheduler(99, 3, 0.5f);
            bool differs = false;
            var d = new TrafficScheduler(100, 3, 0.5f);
            for (int i = 0; i < 200; i++)
                if (c.Next(i * 0.5f) != d.Next(i * 0.5f)) { differs = true; break; }
            Assert.True(differs);
        }

        [Fact]
        public void SchedulerNeverBlocksEveryLane()
        {
            var scheduler = new TrafficScheduler(7, 3, 1f);
            for (int i = 0; i < 500; i++)
            {
                int? lane = scheduler.Next(i * 0.25f);
                Assert.True(lane is null || (lane >= 0 && lane < 3));
            }
        }

        [Theory]
        [InlineData(0f, 18f, 0.4f, 42f, 18f)]
        [InlineData(10f, 18f, 0.4f, 42f, 22f)]
        [InlineData(120f, 18f, 0.4f, 42f, 42f)]
        public void SpeedRampRisesThenClamps(float t, float baseSpeed, float ramp, float max, float expected)
            => Assert.Equal(expected, RoadMath.Speed(t, baseSpeed, ramp, max), 3);

        [Fact]
        public void LaneShiftClampsAndInterpolates()
        {
            var lane = new LaneMotion(3, 1);
            lane.Shift(1);
            lane.Shift(1);
            Assert.Equal(2, lane.TargetLane);
            lane.Tick(0.1f, 8f);
            Assert.Equal(1.8f, lane.Position, 3);
            for (int i = 0; i < 10; i++) lane.Tick(0.1f, 8f);
            Assert.Equal(2f, lane.Position, 3);
        }

        [Fact]
        public void HitboxOverlapDetectsSameLaneOnly()
        {
            Assert.True(RoadMath.Overlaps(1, 0, 1.1f, 2f, 1, 0.5f, 1.8f, 2f));
            Assert.False(RoadMath.Overlaps(1, 0, 1.1f, 2f, 3, 0.5f, 1.8f, 2f));
        }

        [Fact]
        public void MultiplierBuildsThenResetsOnCollision()
        {
            var score = new ScoreState();
            score.CleanPass(10); // 10 * 1
            score.CleanPass(10); // 10 * 2
            Assert.Equal(30, score.Score);
            Assert.Equal(3, score.Multiplier);
            for (int i = 0; i < 20; i++) score.CleanPass(1);
            Assert.Equal(8, score.Multiplier);
            score.Collision();
            Assert.Equal(1, score.Multiplier);
            Assert.True(score.Crashed);
            score.CleanPass(10);
            Assert.Equal(1, score.Multiplier);
        }

        [Fact]
        public void RecordBookJsonRoundTrip()
        {
            var book = new RecordBook();
            book.Runs.Add(new RunRecord { Route = "Desert Canyon", Score = 1234 });
            book.Runs.Add(new RunRecord { Route = "Neon Tunnel", Score = 99 });
            string json = RecordCodec.Export(book);
            var restored = RecordCodec.Import(json);
            Assert.Equal(2, restored.Runs.Count);
            Assert.Equal("Desert Canyon", restored.Runs[0].Route);
            Assert.Equal(99, restored.Runs[1].Score);
        }

        [Fact]
        public void RecordRoundTripPreservesEscapesAndUnicode()
        {
            var book = new RecordBook();
            book.Runs.Add(new RunRecord { Route = "Quote\" Back\\ Tab\t Line\n \u00e9\u2603", Score = 0 });
            var restored = RecordCodec.Import(RecordCodec.Export(book));
            Assert.Equal(book.Runs[0].Route, restored.Runs[0].Route);
            Assert.Empty(RecordCodec.Import(RecordCodec.Export(new RecordBook())).Runs);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData("{\"payload\":\"{}\",\"checksum\":\"00\"}")]
        [InlineData("{\"payload\":1,\"checksum\":\"00\"}")]
        [InlineData("[1,2,3]")]
        [InlineData("{\"payload\":\"x\",\"checksum\":\"y\"} trailing")]
        public void MalformedRecordJsonIsRejectedWithoutCrashing(string json)
            => Assert.ThrowsAny<Exception>(() => RecordCodec.Import(json));

        [Fact]
        public void ValidChecksumButBadSchemaIsRejected()
        {
            // Build envelopes whose checksum is valid for a schema-invalid payload.
            foreach (string payload in new[] { "{\"runs\":[{\"route\":\"A\",\"score\":-5}]}", "{\"runs\":[{\"route\":\"\",\"score\":1}]}", "{\"runs\":{}}" })
            {
                string envelope = "{\"payload\":" + JsonQuote(payload) + ",\"checksum\":\"" + Sha(payload) + "\"}";
                Assert.Throws<InvalidDataException>(() => RecordCodec.Import(envelope));
            }
        }

        private static string JsonQuote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static string Sha(string s)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s))).Replace("-", "").ToLowerInvariant();
        }

        [Fact]
        public void ChecksumDetectsTampering()
        {
            var book = new RecordBook();
            book.Runs.Add(new RunRecord { Route = "Coastal Metropolis", Score = 500 });
            string json = RecordCodec.Export(book);
            string tampered = json.Replace("500", "999");
            Assert.NotEqual(json, tampered);
            Assert.Throws<InvalidDataException>(() => RecordCodec.Import(tampered));
        }
    }
}
