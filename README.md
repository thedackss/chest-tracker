# chest-tracker

Counts the Fortnite chests you open, dig up or break, from the replay files Fortnite saves on your PC.
Made for the Treasure Hunters Pirate Shop token hunt: the token drops from chests, so you want to know how many you've opened.

- **Total**: every chest you opened, dug up (buried chests) or broke (floor/wall destroyed under it).
- **Quest**: chests opened or dug up, not broken, which is what the in-game "Open treasure chests" quest counts. It keeps counting after the quest stops at 1000.
- In-game overlay, tray icon, a notification after each match, and an HTML report with every match.

It only reads `.replay` files. It does not touch the game, its memory or your Epic account. It makes no network calls.

## Use it

1. In Fortnite: **Settings → Game → Replays → Record Replays: On**.
2. Run `ChestTracker.exe`. Windows may say "Windows protected your PC" (the exe is not signed): **More info → Run anyway**.
3. On the welcome screen, type where your "Open treasure chests" quest is right now, so the counts start from there.
4. Play. Each match is counted a few seconds after it ends.

For the overlay to show over the game, use **Windowed Fullscreen** (Settings → Video → Window Mode).

| Shortcut | |
|---|---|
| Ctrl+Shift+O | show / hide the overlay |
| Ctrl+Shift+L | lock / unlock the overlay (unlocked: drag it) |

The tray icon menu has the report and the settings. Data lives in `%APPDATA%\chest-tracker`.

## Build

Needs the .NET 10 SDK (Linux or Windows).

```sh
./publish.sh                     # -> dist/ChestTracker.exe
dotnet run --project tools/scan -- <replay or folder> [--start N]   # per-match counts, any OS
```

`tools/make-icon.ps1` regenerates the app icon (Windows).

## Credits

The replay parser (`lib/`) and the chest-attribution logic come from
[LED-N/Fortnite-ChestCounter](https://github.com/LED-N/Fortnite-ChestCounter) and
[Shiqan/FortniteReplayDecompressor](https://github.com/Shiqan/FortniteReplayDecompressor), both MIT.
See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
