# Lane Weaver — PLAN

## Scope and architecture

A Unity 6.3 LTS (6000.3.x, C#) arcade motorcycle traffic-dodger. Windows (StandaloneWindows64) is the primary CI/release target; Android and iOS (phones AND tablets, iPad/Android-tablet support enabled) must keep compiling. Same codebase, adaptive to phone, tablet, desktop window sizes.

Layers (assembly definitions keep EditMode tests fast and engine-free where possible):

- `Weaver.Core` — pure C# (no UnityEngine): procedural traffic scheduler, lane-switch mechanics, speed ramp formulas, scoring/clean-pass multiplier state machine, collision hitbox math, record-book model + JSON serializer + checksum. Unit-tested in EditMode (and plain `dotnet test` via a thin test project in Docker on Linux).
- `Weaver.Physics` — MonoBehaviours around lightweight 2.5D lane transforms: `MotorcycleController`, `TrafficVehicleController`, `RoadSegmentPool`, substepped lane interpolation.
- `Weaver.Routes` — route prefabs/definitions (Desert Canyon, Neon Tunnel, Coastal Metropolis) and a `RouteDefinition` ScriptableObject per route.
- `Weaver.Input` — Unity Input System actions; control schemes: KeyboardMouse, Gamepad, Touch; `InputPromptProvider` for platform-correct glyphs.
- `Weaver.Presentation` — visual road curvature, parallax backgrounds, speed lines, engine SFX + adaptive music bed, accessibility limiters (motion/shake/flash).
- `Weaver.UI` — custom game UI (UI Toolkit), HUD, pause/records/settings.
- `Weaver.Persistence` — `RecordBookStore` + `SettingsStore` seams over local JSON files (`Application.persistentDataPath`).

## Technology choices

- Unity 6.3 LTS: current supported LTS, Input System, URP, UI Toolkit.
- URP for consistent look across Windows/mobile; bloom/trail effects budgeted for mobile.
- UX strategy: **B — fully custom branded UX**. A bespoke arcade design system that is neither Material nor Cupertino, still honoring each platform: system back/quit/pause conventions, safe areas, window resizing, system text scale, hover/focus states and full keyboard navigation on Windows, platform-correct input prompts (touch vs keyboard/mouse vs gamepad). Rationale: games are visual products; platform-native widget kits would add nothing and fight the art direction.
- Testing/CI: GameCI (`game-ci/unity-test-runner`, `game-ci/unity-builder`) on `windows-latest`; Linux Docker used for structure/lint checks and the pure-C# core test project.
- Fleet identifier prefix: `com.infinityball.laneweaver`.
- No Flutter, React Native, Expo, Kotlin Multiplatform, .NET MAUI, SpriteKit, Flame, or custom engines.

## Milestones and dependency order

1. Skeleton + CI + Git LFS + Unity `.gitignore` (blocks everything).
2. Pure-C# core (traffic scheduler, lane transitions, clean-pass multiplier, record book).
3. Physics/rendering core + Desert Canyon route playable on Windows.
4. Adaptive input + custom UI + accessibility.
5. Audio/VFX + Neon Tunnel and Coastal Metropolis routes.
6. Icon wiring + store copy review.
7. Windows release workflow.

## Testing strategy

- EditMode tests for `Weaver.Core` (lane transitions, speed ramp formulas, clean-pass multiplier state machine, record-book round trip, checksum tamper detection).
- PlayMode smoke tests: motorcycle spawn, lane change timing, traffic collision → game over.
- Input tests: simulate keyboard, gamepad, touch devices via Input System test fixtures; tilt-off fallback.
- Accessibility checks: flash limiter bounds, shake toggle, remap persistence.
- If `UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD` are not available as repo secrets, mark the Unity job blocked and report it; never fake a green result.
- Verification is host-side in Docker where applicable; never fabricate results.

## Packaging and distribution plan

- `.github/workflows/release.yml`: on `v*` tag or manual dispatch, `windows-latest` GameCI StandaloneWindows64 build → zip → GitHub release asset. Never log secret values.
- Android debug APK and iOS compile verification are CI lanes; Play/TestFlight uploads (using ASC_KEY_ID, ASC_ISSUER_ID, ASC_KEY_P8, ASC_TEAM_ID names only) are later opt-in milestones.
- Identifier `com.infinityball.laneweaver` across Unity bundle ID, Android applicationId, iOS bundle ID, MSIX identity.
- `AppStore/icon.png` (real generated 1024x1024 artwork, present) wired into Windows/Android/iOS icon sets; `AppStore/description.txt` kept accurate.

## Risks

- Unity license secrets may be unavailable in CI → mark Unity jobs blocked and report.
- High-speed lane changes require crisp input polling to avoid unfair collision deaths; budget iteration on hitbox tolerances.
- Mobile performance of road rendering; provide quality tiers.
- Original-IP discipline: no licensed motorcycles, car models, brand names, or sounds.

## Explicit non-goals

No licensed vehicles; no accounts, cloud, ads, IAP, analytics; no VR; no multiplayer; no open-world map in MVP; no Flutter, React Native, Expo, Kotlin Multiplatform, .NET MAUI, SpriteKit, Flame, or custom engines; no required motion sensors.
