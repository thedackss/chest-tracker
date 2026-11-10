# chest-tracker

Counts the Fortnite chests you open, dig up or break, from the replay files Fortnite saves on your PC.
Made for the Treasure Hunters Pirate Shop token hunt: the token drops from chests, so you want to know how many you've opened.

- **Total**: every chest you opened, dug up (buried chests) or broke (floor/wall destroyed under it).
- **Quest**: chests opened or dug up, not broken, which is what the in-game "Open treasure chests" quest counts. It keeps counting after the quest stops at 1000.
- In-game overlay, tray icon, a notification after each match, and a window with your totals, chests per day and every match
  (dark or light, English or Spanish).

It only reads `.replay` files. It does not touch the game, its memory or your Epic account. It makes no network calls.

## Use it

1. In Fortnite: **Settings → Game → Replays → Record Replays: On**.
2. Download `ChestTracker.exe` from [Releases](https://github.com/thedackss/chest-tracker/releases) and run it.
   If Windows says "Windows protected your PC" (unsigned or still-new exe): **More info → Run anyway**.
3. On the welcome screen, type where your "Open treasure chests" quest is right now, so the counts start from there.
4. Play. Each match is counted a few seconds after it ends.

For the overlay to show over the game, use **Windowed Fullscreen** (Settings → Video → Window Mode).

Streaming with OBS (Game Capture doesn't see the overlay): turn on **Settings → Streaming (OBS) → Overlay for OBS**, then in OBS
add **Sources → + → Browser** with the address shown there (`http://localhost:28282/overlay`), width 500, height 300.
Add `?align=left` or `?scale=1.5` to the address to change it.

| Shortcut | |
|---|---|
| Ctrl+Shift+O | show / hide the overlay |
| Ctrl+Shift+L | lock / unlock the overlay (unlocked: drag it) |

Click the tray icon for today's numbers and quick toggles; double-click it (or run the exe again) to open the window,
with the overview, every match (exportable as an HTML report) and the settings. Data lives in `%APPDATA%\chest-tracker`.

## What it changes on your PC, and uninstalling

There is no installer: the exe runs from wherever you put it.

- It saves its settings, match history and report in `%APPDATA%\chest-tracker`.
- With **Start with Windows** on (Settings), it adds a `chest-tracker` entry to
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

To uninstall: turn off **Start with Windows** in Settings, **Quit** from the tray icon,
then delete the exe and the `%APPDATA%\chest-tracker` folder.

## Privacy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

The optional **Overlay for OBS** (Settings → Streaming, off by default) serves your chest counts at
`http://localhost:28282/overlay` so OBS can show them. It only listens on this PC (127.0.0.1) and refuses requests
addressed to anything other than `localhost`; nothing is reachable from your network or the internet.

## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

- Committers and reviewers: [thedackss](https://github.com/thedackss)
- Approvers: [thedackss](https://github.com/thedackss)

Release exes are built from this repository by [GitHub Actions](.github/workflows/release.yml) and each signing is approved by hand.
Changes from people outside the team (pull requests) are reviewed before they are merged.

## Build

Needs the .NET 10 SDK (Linux or Windows).

```sh
./publish.sh                     # -> dist/ChestTracker.exe
dotnet run --project tools/scan -- <replay or folder> [--start N]   # per-match counts, any OS
```

`tools/make-icon.ps1` regenerates the app icon (Windows).
`ChestTracker.exe --data <test folder> --preview <folder>` saves screenshots of the overlay and every window (dark and light).

## Credits

The replay parser (`lib/`) and the chest-attribution logic come from
[LED-N/Fortnite-ChestCounter](https://github.com/LED-N/Fortnite-ChestCounter) and
[Shiqan/FortniteReplayDecompressor](https://github.com/Shiqan/FortniteReplayDecompressor), both MIT.
The windows use [Avalonia](https://avaloniaui.net) (MIT). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
