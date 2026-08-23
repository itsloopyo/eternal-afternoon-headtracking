# Third-Party Notices

EternalAfternoonHeadTracking bundles, statically links, or credits the third-party
components listed below. Each remains the property of its authors and is used
under its own licence. Where a licence requires the copyright notice, the
conditions and the disclaimer to accompany a binary distribution, the full text
is reproduced here verbatim, and this file ships at the root of every release
archive we publish.

No part of Eternal Afternoon is redistributed by this project. It contains no
game code, no game assets, no decompiled or disassembled output, and no
proprietary DLLs. The single piece of material in this repository that belongs
to the game's author is the demonstration recording described under "Eternal
Afternoon footage" below, which ships in no release archive.

| Component | Version | Licence | How it ships |
|-----------|---------|---------|--------------|
| Mono.Cecil | 0.11.5 | MIT | Bundled verbatim in the installer ZIP; `Mono.Cecil.dll` deployed for install-time patching |
| cameraunlock-core | 3465659888b2270addac9de0b2a728f59a00360c | MIT | Compiled into `CameraUnlock.Core.dll` / `CameraUnlock.Core.Unity.dll`, shipped in both ZIPs |
| OpenTrack | n/a | ISC | Not bundled; UDP protocol interoperability only |

---

## Mono.Cecil

Vendored at `vendor/mono-cecil/`, shipped in the installer ZIP and used as the
install-time source. Taken from the upstream NuGet package untouched; the
upstream licence file ships beside it at `vendor/mono-cecil/LICENSE`.
`Mono.Cecil.dll` is extracted from that package and deployed into the game's
`Managed` folder, because `install.cmd` and `uninstall.cmd` load it from there to
apply and to reverse the bootstrap injection. The mod itself does not reference
it at runtime.

- Upstream: https://github.com/jbevain/cecil
- Package: https://www.nuget.org/packages/Mono.Cecil/0.11.5
- Version: 0.11.5
- SHA-256 of `Mono.Cecil.0.11.5.nupkg`: `9cf1706f35b4f209c28da7417608bed7a307621b0f0179c52258af78bc4668d0`

```
Copyright (c) 2008 - 2015 Jb Evain
Copyright (c) 2008 - 2011 Novell, Inc.

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

---

## cameraunlock-core

Git submodule at `cameraunlock-core/`, built into `CameraUnlock.Core.dll` and
`CameraUnlock.Core.Unity.dll` which ship in both release archives. Our own code,
MIT licensed, reproduced here so the notices are complete.

- Upstream: https://github.com/itsloopyo/cameraunlock-core
- Pinned commit: `3465659888b2270addac9de0b2a728f59a00360c`

```
MIT License

Copyright (c) 2026 CameraUnlock

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

---

## OpenTrack

Not bundled and not linked. This mod implements the OpenTrack UDP pose datagram
layout so that OpenTrack (https://github.com/opentrack/opentrack, ISC licence)
and compatible trackers can drive it. No OpenTrack code, headers or binaries are
copied, linked or redistributed, so its licence triggers no notice obligation
here. It is credited because the wire format is its work.

---

## Unity reference stubs

`src/EternalAfternoonHeadTracking/libs/UnityStubs.cs` and `UnityUIStubs.cs` are
written by us. They declare the signatures of the Unity and uGUI members this
mod binds against, with empty or trivially re-derived bodies, so that the
project compiles on a machine with no game installed. They contain no Unity
Technologies source, no Unity binaries and no text from Unity's documentation,
and none of them ship: they are compiled into throwaway reference assemblies at
build time, and the real engine assemblies are loaded from the player's own
installation at runtime.

---

## Eternal Afternoon footage

- **File:** `assets/readme-clip.gif`
- **Rights holder:** Alex Klexber, together with the rights holders of any
  third-party marks visible in frame.
- **Usage:** a recording of the game running with this mod, captured on a
  legitimately purchased copy, shown so a reader can see what the mod does
  before installing it.
- **Bundled:** no. The packaging scripts copy no part of `assets/`, so it is in
  neither release archive nor anything the launcher deploys.
- **Licence:** none is granted or implied by this repository. This material is
  not covered by the MIT licence in `LICENSE`, which says so explicitly. Nothing
  here permits its reuse. If the rights holder would rather it were not
  published, open an issue or reach us on Discord and it comes down.

---

## Eternal Afternoon

Eternal Afternoon is the property of Alex Klexber. This mod is an unofficial fan
project and is not affiliated with, endorsed by or supported by them. Eternal
Afternoon and all related names, logos, characters and marks are trademarks of
their respective owners, used here only to identify the game this mod applies
to. That is nominative use and not a claim of any right in them.

The mod requires a legitimately purchased copy of the game. It circumvents no
DRM, licence check or anti-cheat. It interoperates with the game by reflecting
over type and member names at runtime, and by injecting a call to our own
bootstrap into `Assembly-CSharp.dll` at install time. Those names are recorded
in our source as plain strings, which are facts about the interface rather than
expression copied from it, and the installer keeps a `.original` backup so
uninstalling restores the shipped assembly byte for byte. No decompiled or
disassembled game code is stored in this repository.
