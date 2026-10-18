# Lane Weaver

Offline arcade motorcycle traffic-dodger for players who want short, replayable highway runs on Windows, phones, and tablets. Weave through traffic at rising speeds on original desert, tunnel, and city roads; beat your own distance and clean-pass records.

## Why / who

Traffic-riding games often use ads, upgrades, and network friction. Lane Weaver targets solo arcade players who want a small, accessible, account-free game. No real-world motorcycle brands or roads.

## Intended workflow

1. Install the Windows portable zip from a `v*` GitHub release (planned).
2. Choose route and difficulty; start a run with keyboard/gamepad on desktop or touch steering on mobile.
3. Change lanes, avoid cars, pass cleanly for score, and stop when a collision ends the run. This is an arcade game, not a riding simulator.
4. Review local personal bests; export or restore records as JSON.

## MVP

- Unity 6.3 LTS 2.5D perspective highway, three original route palettes (desert, tunnel, city), procedural lane traffic, speed progression, hitbox-based collisions, restart/pause.
- Keyboard/mouse/gamepad and touch via Unity Input System; no required tilt or motion sensors.
- Local per-route distance and clean-pass records; JSON export/backup.
- Custom branded UI, touch-safe controls, scalable text, high contrast, reduced-motion option, full keyboard navigation on Windows, responsive phone/tablet/window layouts.

## Non-goals

No accounts, cloud, ads, IAP, real-world brands, licensed assets, online multiplayer, open-world riding, traffic-rule instruction, or safety advice. No Flutter, React Native, Expo, Kotlin Multiplatform, .NET MAUI, SpriteKit, Flame, or custom engine: games use Unity.

## Privacy / permissions

Gameplay and records stay on device in Unity's per-user persistent data directory. No network or telemetry SDK; export writes only to a user-selected file. No location, camera, or microphone permission. Input System handles touch/keyboard/gamepad; motion sensors not needed. Deleting local saves clears records.

## Platforms / status

**Planning scaffold only.** Unity project settings and package manifest exist; no gameplay scene, build, test suite, or CI run yet. Windows StandaloneWindows64 is primary CI/release target. Android/iOS phones and tablets are first-class targets; native iPad support enabled. App Store / Play submission deferred. `AppStore/description.txt` describes planned features; real generated `AppStore/icon.png` exists but icon sets are not wired yet.

Identifier: `com.infinityball.laneweaver` for Unity package, Android applicationId, iOS bundle ID, and Windows/MSIX identity. App Store Connect registration: CREATED. Repo Actions secret names: ASC_KEY_ID, ASC_ISSUER_ID, ASC_KEY_P8, ASC_TEAM_ID (values never stored here). Unity license secret names needed by GameCI: UNITY_LICENSE, UNITY_EMAIL, UNITY_PASSWORD; absent secrets mean blocked build, not green.

## Milestones / build quickstart

1. Complete Unity 6.3 LTS editor-generated skeleton, Input System, EditMode tests and Windows GameCI lane on `windows-latest`; run host-side checks in Docker.
2. Traffic generator + collision/score state machine, playable Windows loop.
3. Touch and adaptive custom UI, Android/iOS compile checks, accessibility.
4. Local save/export, original route visuals and sound, generated icon wiring.
5. `.github/workflows/release.yml`: `v*` tag/manual Windows zip on GitHub release; stores later.

Install Unity 6.3 LTS (6000.3.x), open repository in Unity Hub after skeleton issue completes, select StandaloneWindows64, and run EditMode tests. No working scene exists yet. See [PLAN.md](PLAN.md) and open issues. License: MIT.
