# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A CounterStrikeSharp (CSS) plugin for CS2 servers that lets players vote to kick, ban, mute, gag or silence other players. It does not punish anyone itself: once a vote passes it runs the admin plugin's console commands (`css_ban`, `css_kick`, `css_mute`, `css_gag`, `css_silence`), so punishments land in whatever admin system / web panel the server uses.

## Build

```bash
dotnet build src/Playervotes.csproj
```

- Targets `net10.0`. There are no tests and no linter.
- Output goes to `src/bin/<Configuration>/counterstrikesharp/plugins/Playervotes/` (layout matches the server's `addons/counterstrikesharp/plugins/` folder), with `lang/` copied alongside.
- `CounterStrikeSharp.API` is referenced with `Version="*"` (always latest).
- `PanoramaManager` 0.4.4 is a NuGet reference with `ExcludeAssets=runtime` (installed on the server once).
- `MenuManagerApi.dll` is a local reference via `..\..\API\MenuManagerApi.dll`, i.e. a folder **outside this repo** (`CS2 Pluginy/API/`). The build fails if that DLL is missing. It is not copied to output (`Private=false`); on the server it lives in `addons/counterstrikesharp/shared/MenuManagerApi/`.

## Architecture

`Playervotes` is a `partial` class split by concern; non-engine code lives in separate classes.

- `Main.cs` - metadata, `Load`/`Unload`, MenuManager capability, `OnConfigParsed` (runs `ConfigUpdater` so new keys land in the server's file).
- `Commands.cs` - `css_vote{kick,ban,mute,gag,silence}` (registered in `Load` only if enabled, so toggling needs a reload), `css_yes`/`css_no`, `css_cancelvote`; `Settings(VoteType)` reads the per-type section (`Config.Kick`/`Ban`/...; percentage 0 = `General.DefaultPercentage`); player lists and the MenuManager target menu.
- `Votes.cs` - the single active `VoteSession`: start checks (one vote at a time, immunity, `TargetProtectionMinutes`, `AllowSelfVote` via `IsExcludedTarget`), `CastVote`, `EvaluateVote` (recount every vote / second / disconnect; target gone = cancelled, except a voteban which continues, keeps the target's vote in the count, and passes via `Ban.OfflineCommand`), `FinishVote` + `ExecuteVote` (runs the admin plugin's commands), E-key `OnTick` hooked only while a vote is open.
- `VoteSession.cs` - pure vote state and threshold math: `ceil(pct * eligible / 100)`, eligible = humans with a valid pawn, target included; without `AllowSelfVote` the target gets a fixed NO at vote start.
- `Hud/VoteHud.cs` - the Panorama panel through PanoramaManager: ONE layout (`pv_vote`) driven by TWO handles, passive (`CaptureInput=false`) and live (`true`); E switches handles in the same tick. Paints only changed vars/classes per viewer. `Hud/VotePanel.cs` is the plugin side (start/stop, per-player `VoteView` built with `Localizer.ForPlayer`).
- `src/panorama/` - layout + stylesheet sources. `dotnet build` does not compile them; they must be compiled to `.vxml_c`/`.vcss_c` and shipped to clients.
- `Config.cs` - sectioned config (`General`, one section per vote type, debug keys at the top level), `ConfigVersion` 2. `ConfigMigration.cs` turns a version 1 (flat) file into it, keeping values and a `.v1.bak` copy.
- `ConfigUpdater.cs`, `PerfMonitor.cs`, `Debug.cs` - shared templates (config auto-update, `EnablePerfMonitor` timing, `EnableDebugLogs`).

Timer rule: the 1 s `REPEAT` timer is never killed, and panel open/close from it goes through `Server.NextFrame`.

Adding a new vote type means touching: `VoteType`, a section in `Config.cs` (+ `VoteConfigFor` and `ConfigMigration` if it replaces old keys), `ExecuteVote`, and keys in **every** `src/lang/*.json` file (`<type>.title`, `panel.type.<type>`, the executed message).

## Localization

`src/lang/{en,fi,pl,pt-BR,ru}.json` use CSS chat color tags (`{green}`, `{default}`, …) and positional `{0}`, `{1}` args. Keep all language files in sync with the arguments passed from code (e.g. `votefailed` gets vote title, target, yes, no, required; `panel.*` keys have no color tags; `votebanned`/`votemuted`/`votegagged`/`votesilenced` get name and duration).

## Versioning

`ModuleVersion` in `Main.cs` is the release version (commits like `v2.5` bump it). The README holds an example config that should be kept in sync with `Config.cs` when config fields change. Renaming or moving keys needs a `ConfigMigration` step and a `ConfigVersion` bump.
