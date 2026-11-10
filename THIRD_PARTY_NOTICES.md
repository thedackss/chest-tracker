# Third-party notices

chest-tracker reuses code from the following MIT-licensed projects.

## Fortnite-ChestCounter

https://github.com/LED-N/Fortnite-ChestCounter (v1.4.0)

- `src/ChestTracker.Engine/ReplayAnalyzer.cs`, `ChestKinds.cs`, `TreasureNames.cs` and `ReplayHeader.cs` are adapted from its `ReplayAnalyzer.cs` and `Tracker.cs`: chest attribution through `SearchingPawn`, refill / stale-state handling, treasure detection and the replay header probe.
- `lib/` is its patched copy of FortniteReplayDecompressor (see `lib/PATCHES.md`).

```
MIT License

Copyright (c) 2026 LED-N

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## FortniteReplayDecompressor

https://github.com/Shiqan/FortniteReplayDecompressor — `lib/FortniteReplayReader`, `lib/Unreal.Core`, `lib/Unreal.Encryption`.
License: `lib/LICENSE-FortniteReplayDecompressor` (MIT, Copyright (c) 2020-2026 Shiqan).

## Ooz / OozSharp

`lib/OozSharp`: C# version of [ooz](https://github.com/powzix/ooz) by powzix, converted for Fortnite by [SL-x-TnT](https://github.com/SL-x-TnT).

## Libraries in ChestTracker.exe (NuGet packages)

The windows are built with these libraries, included in the exe:

| Library | License | Copyright |
|---|---|---|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) (Avalonia, Avalonia.Win32, Avalonia.Skia, Avalonia.HarfBuzz, Avalonia.Themes.Fluent, Avalonia.Remote.Protocol) | MIT | The AvaloniaUI Project |
| [MicroCom.Runtime](https://github.com/kekekeks/MicroCom) | MIT | Nikita Tsukanov |
| [SkiaSharp](https://github.com/mono/SkiaSharp), [HarfBuzzSharp](https://github.com/mono/SkiaSharp) | MIT | Microsoft Corporation |
| [Skia](https://skia.org) (native, inside libSkiaSharp) | BSD-3-Clause | Google LLC |
| [HarfBuzz](https://github.com/harfbuzz/harfbuzz) (native, inside libHarfBuzzSharp) | MIT ("Old MIT") | HarfBuzz contributors |

The license texts are in each package (NuGet) and project repository linked above.
