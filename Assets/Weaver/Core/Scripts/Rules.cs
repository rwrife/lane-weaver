using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace Weaver.Core
{
    public static class RoadMath
    {
        public static float Speed(float elapsedSeconds, float baseSpeed, float rampPerSecond, float maximum)
        {
            if (float.IsNaN(elapsedSeconds) || float.IsNaN(baseSpeed) || float.IsNaN(rampPerSecond) || float.IsNaN(maximum) ||
                elapsedSeconds < 0 || baseSpeed < 0 || rampPerSecond < 0 || maximum < baseSpeed)
                throw new ArgumentOutOfRangeException();
            return Math.Min(maximum, baseSpeed + elapsedSeconds * rampPerSecond);
        }

        public static bool Overlaps(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh)
        {
            if (aw < 0 || ah < 0 || bw < 0 || bh < 0) throw new ArgumentOutOfRangeException();
            return Math.Abs(ax - bx) * 2 < aw + bw && Math.Abs(ay - by) * 2 < ah + bh;
        }
    }

    public sealed class LaneMotion
    {
        public int LaneCount { get; }
        public int TargetLane { get; private set; }
        public float Position { get; private set; }

        public LaneMotion(int laneCount, int startLane)
        {
            if (laneCount < 1 || startLane < 0 || startLane >= laneCount) throw new ArgumentOutOfRangeException();
            LaneCount = laneCount;
            TargetLane = startLane;
            Position = startLane;
        }

        public void Shift(int direction)
        {
            if (direction != -1 && direction != 1) throw new ArgumentOutOfRangeException(nameof(direction));
            TargetLane = Math.Max(0, Math.Min(LaneCount - 1, TargetLane + direction));
        }

        public void Tick(float deltaSeconds, float lanesPerSecond)
        {
            if (deltaSeconds < 0 || lanesPerSecond <= 0) throw new ArgumentOutOfRangeException();
            float step = deltaSeconds * lanesPerSecond;
            Position = Position < TargetLane ? Math.Min(TargetLane, Position + step)
                : Math.Max(TargetLane, Position - step);
        }
    }

    public sealed class TrafficScheduler
    {
        private uint state;
        private readonly int laneCount;
        private readonly float difficulty;

        public TrafficScheduler(uint seed, int laneCount, float difficulty)
        {
            if (laneCount < 2 || difficulty < 0 || difficulty > 1) throw new ArgumentOutOfRangeException();
            state = seed == 0 ? 0x6D2B79F5u : seed;
            this.laneCount = laneCount;
            this.difficulty = difficulty;
        }

        private uint Roll()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        // One vehicle maximum per tick preserves at least one free lane.
        // ponytail: caller owns tick cadence; add distance-based scheduling with road pooling.
        public int? Next(float elapsedSeconds)
        {
            if (elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            float density = Math.Min(0.85f, 0.12f + difficulty * 0.38f + elapsedSeconds * 0.001f);
            bool spawn = (Roll() / (double)uint.MaxValue) < density;
            int lane = (int)(Roll() % (uint)laneCount);
            return spawn ? lane : (int?)null;
        }
    }

    public sealed class ScoreState
    {
        public int Multiplier { get; private set; } = 1;
        public int Score { get; private set; }
        public bool Crashed { get; private set; }

        public void CleanPass(int basePoints)
        {
            if (basePoints <= 0) throw new ArgumentOutOfRangeException(nameof(basePoints));
            if (Crashed) return;
            Score = checked(Score + checked(basePoints * Multiplier));
            Multiplier = Math.Min(8, Multiplier + 1);
        }

        public void Collision() { Crashed = true; Multiplier = 1; }
    }

    [DataContract]
    public sealed class RunRecord
    {
        [DataMember(Order = 0)] public string Route { get; set; } = "";
        [DataMember(Order = 1)] public int Score { get; set; }
    }

    [DataContract]
    public sealed class RecordBook
    {
        [DataMember(Order = 0)] public List<RunRecord> Runs { get; set; } = new List<RunRecord>();
    }

    [DataContract]
    internal sealed class RecordEnvelope
    {
        [DataMember(Order = 0)] public string Payload { get; set; } = "";
        [DataMember(Order = 1)] public string Checksum { get; set; } = "";
    }

    public static class RecordCodec
    {
        private static string Encode<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static T Decode<T>(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var value = new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
                if (value is T typed) return typed;
                throw new InvalidDataException("Invalid record JSON");
            }
        }

        private static string Hash(string payload)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", "").ToLowerInvariant();
        }

        public static string Export(RecordBook book)
        {
            if (book == null || book.Runs == null) throw new ArgumentNullException(nameof(book));
            foreach (var run in book.Runs)
                if (run == null || string.IsNullOrWhiteSpace(run.Route) || run.Score < 0)
                    throw new ArgumentException("Invalid record", nameof(book));
            string payload = Encode(book);
            return Encode(new RecordEnvelope { Payload = payload, Checksum = Hash(payload) });
        }

        public static RecordBook Import(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Missing record JSON", nameof(json));
            try
            {
                var envelope = Decode<RecordEnvelope>(json);
                if (envelope.Payload == null || envelope.Checksum == null ||
                    !string.Equals(Hash(envelope.Payload), envelope.Checksum, StringComparison.Ordinal))
                    throw new InvalidDataException("Record checksum mismatch");
                var book = Decode<RecordBook>(envelope.Payload);
                if (book.Runs == null) throw new InvalidDataException("Invalid record book");
                foreach (var run in book.Runs)
                    if (run == null || string.IsNullOrWhiteSpace(run.Route) || run.Score < 0)
                        throw new InvalidDataException("Invalid record");
                return book;
            }
            catch (SerializationException ex) { throw new InvalidDataException("Invalid record JSON", ex); }
        }
    }
}
