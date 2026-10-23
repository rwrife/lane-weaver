#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

    public sealed class RunRecord
    {
        public string Route { get; set; } = "";
        public int Score { get; set; }
    }

    public sealed class RecordBook
    {
        public List<RunRecord> Runs { get; } = new List<RunRecord>();
    }

    // Hand-rolled JSON keeps Weaver.Core dependency-free and IL2CPP/AOT-safe:
    // DataContractJsonSerializer relies on runtime reflection codegen that
    // Unity's IL2CPP mobile backends (Android/iOS) do not support.
    // ponytail: fixed flat schema {runs:[{route,score}]}; upgrade path is a
    // generated serializer or System.Text.Json if it lands in core modules.
    public static class RecordCodec
    {
        private static string Hash(string payload)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", "").ToLowerInvariant();
        }

        public static string Export(RecordBook book)
        {
            if (book == null) throw new ArgumentNullException(nameof(book));
            foreach (var run in book.Runs)
                if (run == null || string.IsNullOrWhiteSpace(run.Route) || run.Score < 0)
                    throw new ArgumentException("Invalid record", nameof(book));
            var payload = new StringBuilder();
            payload.Append("{\"runs\":[");
            for (int i = 0; i < book.Runs.Count; i++)
            {
                if (i > 0) payload.Append(',');
                payload.Append("{\"route\":").Append(JsonText.Quote(book.Runs[i].Route))
                       .Append(",\"score\":").Append(book.Runs[i].Score.ToString(CultureInfo.InvariantCulture))
                       .Append('}');
            }
            payload.Append("]}");
            string text = payload.ToString();
            return "{\"payload\":" + JsonText.Quote(text) + ",\"checksum\":\"" + Hash(text) + "\"}";
        }

        public static RecordBook Import(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Missing record JSON", nameof(json));
            var envelope = JsonText.Parse(json);
            string payload = JsonText.StringValue(envelope, "payload", 2);
            string checksum = JsonText.StringValue(envelope, "checksum", 2);
            if (!string.Equals(Hash(payload), checksum, StringComparison.Ordinal))
                throw new InvalidDataException("Record checksum mismatch");

            var root = JsonText.Parse(payload);
            var runs = JsonText.Member(root, "runs", 1);
            if (runs.Kind != 'a') throw new InvalidDataException("runs must be an array");
            var book = new RecordBook();
            foreach (var item in runs.Items)
            {
                string route = JsonText.StringValue(item, "route", 2);
                int score = JsonText.IntValue(item, "score", 2);
                if (string.IsNullOrWhiteSpace(route) || score < 0)
                    throw new InvalidDataException("Invalid record");
                book.Runs.Add(new RunRecord { Route = route, Score = score });
            }
            return book;
        }
    }

    internal static class JsonText
    {
        internal sealed class Node
        {
            internal char Kind; // 'o' object, 'a' array, 's' string, 'n' number
            internal string Text;
            internal readonly List<KeyValuePair<string, Node>> Members = new List<KeyValuePair<string, Node>>();
            internal readonly List<Node> Items = new List<Node>();
        }

        internal static string Quote(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        internal static Node Parse(string json)
        {
            var reader = new Reader(json);
            reader.SkipWhitespace();
            Node node = reader.Value();
            reader.SkipWhitespace();
            if (!reader.AtEnd) throw new InvalidDataException("Trailing JSON data");
            return node;
        }

        internal static Node Member(Node obj, string name, int expectedCount)
        {
            if (obj == null || obj.Kind != 'o') throw new InvalidDataException("Expected JSON object");
            if (obj.Members.Count != expectedCount) throw new InvalidDataException("Unexpected member count");
            Node found = null;
            foreach (var m in obj.Members)
            {
                if (m.Key != name) continue;
                if (found != null) throw new InvalidDataException("Duplicate member " + name);
                found = m.Value;
            }
            if (found == null) throw new InvalidDataException("Missing member " + name);
            return found;
        }

        internal static string StringValue(Node obj, string name, int expectedCount)
        {
            Node member = Member(obj, name, expectedCount);
            if (member.Kind != 's') throw new InvalidDataException("Member " + name + " must be a string");
            return member.Text;
        }

        internal static int IntValue(Node obj, string name, int expectedCount)
        {
            Node member = Member(obj, name, expectedCount);
            if (member.Kind != 'n' ||
                !int.TryParse(member.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                throw new InvalidDataException("Member " + name + " must be a non-negative integer");
            return value;
        }

        private sealed class Reader
        {
            private readonly string _s;
            private int _i;
            private int _depth;

            internal Reader(string s) { _s = s; }

            internal bool AtEnd => _i == _s.Length;

            internal void SkipWhitespace()
            {
                while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\t' || _s[_i] == '\n' || _s[_i] == '\r')) _i++;
            }

            internal Node Value()
            {
                if (++_depth > 16) throw new InvalidDataException("JSON nesting too deep");
                char c = Peek();
                Node node;
                if (c == '{') node = Object();
                else if (c == '[') node = Array();
                else if (c == '"') node = new Node { Kind = 's', Text = StringText() };
                else node = Number();
                _depth--;
                return node;
            }

            private char Peek()
            {
                if (_i >= _s.Length) throw new InvalidDataException("Unexpected end of JSON");
                return _s[_i];
            }

            private Node Object()
            {
                var node = new Node { Kind = 'o' };
                _i++; // consume '{'
                SkipWhitespace();
                if (Peek() == '}') { _i++; return node; }
                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"') throw new InvalidDataException("Expected member name");
                    string name = StringText();
                    SkipWhitespace();
                    if (Peek() != ':') throw new InvalidDataException("Expected ':'");
                    _i++;
                    SkipWhitespace();
                    node.Members.Add(new KeyValuePair<string, Node>(name, Value()));
                    SkipWhitespace();
                    char c = Peek();
                    if (c == ',') { _i++; continue; }
                    if (c == '}') { _i++; return node; }
                    throw new InvalidDataException("Expected ',' or '}'");
                }
            }

            private Node Array()
            {
                var node = new Node { Kind = 'a' };
                _i++; // consume '['
                SkipWhitespace();
                if (Peek() == ']') { _i++; return node; }
                while (true)
                {
                    SkipWhitespace();
                    node.Items.Add(Value());
                    SkipWhitespace();
                    char c = Peek();
                    if (c == ',') { _i++; continue; }
                    if (c == ']') { _i++; return node; }
                    throw new InvalidDataException("Expected ',' or ']'");
                }
            }

            private string StringText()
            {
                _i++; // consume opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _s.Length) throw new InvalidDataException("Unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        if (c < 0x20) throw new InvalidDataException("Control character in string");
                        sb.Append(c);
                        continue;
                    }
                    if (_i >= _s.Length) throw new InvalidDataException("Unterminated escape");
                    switch (_s[_i++])
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw new InvalidDataException("Bad \\u escape");
                            string hex = _s.Substring(_i, 4);
                            if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort code))
                                throw new InvalidDataException("Bad \\u escape");
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default: throw new InvalidDataException("Unknown escape sequence");
                    }
                }
            }

            private Node Number()
            {
                int start = _i;
                if (_i < _s.Length && _s[_i] == '-') _i++;
                while (_i < _s.Length && "0123456789+-.".IndexOf(_s[_i]) >= 0) _i++;
                if (_i == start) throw new InvalidDataException("Invalid JSON value");
                return new Node { Kind = 'n', Text = _s.Substring(start, _i - start) };
            }
        }
    }
}
