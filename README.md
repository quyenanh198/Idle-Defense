# Idle Hero Defense

Mobile portrait idle-defense game built with Unity 2022.3 LTS.

## Current milestone

Milestone 24 delivers the end-to-end playable reference vertical slice: deterministic combat, progression,
idle rewards, modes, live configuration, monetization abstractions, an authoritative ASP.NET Core backend,
server energy/ad/reward flows, and fixed-step battle transcripts with validation and rate limiting.

## Open and test

1. Install Unity Hub and Unity `2022.3.62f1` with Android Build Support.
2. Add this folder as a Unity project.
3. Allow Package Manager to restore packages.
4. Open **Window > General > Test Runner > EditMode** and run all tests.

From PowerShell, run the same suite with:

```powershell
./Scripts/test-unity.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe"
```

Release validation is available under **Idle Hero Defense > Validate Project**. Android and Windows builds are available under **Idle Hero Defense > Build** or through these batch entry points:

```text
IdleHeroDefense.Editor.BuildCommand.BuildAndroid
IdleHeroDefense.Editor.BuildCommand.BuildWindows
```

Environment variables `GAME_VERSION`, `BUILD_NUMBER`, and `OUTPUT_PATH` override build metadata and destination.

## Backend

The ASP.NET Core 8 reference service lives in `Backend/IdleHeroDefense.Api`:

```powershell
dotnet test Backend/IdleHeroDefense.sln --configuration Release
dotnet run --project Backend/IdleHeroDefense.Api
```

See `Backend/README.md` for endpoint and deployment configuration.

Unity is not installed in the current local automation environment, so the EditMode suite must also be run on a Unity-enabled workstation or the included CI runner.

## Source layout

```text
Assets/_Game/Scripts/Domain  Pure combat rules
Assets/_Game/Tests/EditMode  NUnit EditMode tests
Packages                     Unity dependencies
ProjectSettings              Pinned editor version
Docs                         Roadmap and hand-offs
```

## Next milestone

Run the complete Unity/.NET CI suite, replace development adapters with platform SDKs and production storage,
then add server-side deterministic replay for adversarial competitive modes.
