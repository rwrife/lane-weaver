namespace Weaver.Core
{
    /// <summary>
    /// Pinned game metadata and version constants.
    /// Pure C# (no UnityEngine) — consumed by core rules, serialization, and HUD.
    /// </summary>
    public static class GameMeta
    {
        public const string Title = "Lane Weaver";
        public const string BundleIdentifier = "com.infinityball.laneweaver";
        public const string CoreVersion = "0.1.0";
        public const int DefaultLaneCount = 3;
        public const float StandardLaneWidth = 3.2f;
    }
}

