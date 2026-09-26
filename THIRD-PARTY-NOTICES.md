# Third-Party Notices

EternalAfternoonHeadTracking bundles, statically links, or credits the third-party components
listed below. Each remains the property of its authors and is used under its own
licence. Where a licence requires the copyright notice, the conditions and the
disclaimer to accompany a binary distribution, the full text is reproduced here
verbatim, and this file ships at the root of every release ZIP we publish.

This repository contains no Eternal Afternoon code, no extracted game assets,
no data files and no proprietary DLLs. The one piece of the game's author's work
kept here is the demonstration recording attributed under "Eternal Afternoon
footage" below, which ships in no release ZIP.

| Component | Version | Licence | How it ships |
|-----------|---------|---------|--------------|
| Mono.Cecil | 0.11.5 | MIT | Bundled verbatim in the installer ZIP |
| cameraunlock-core | b4df73a5d8076968fcbf7e4088dd49db11a2684e | MIT | Compiled into `EternalAfternoonHeadTracking.dll` |
| OpenTrack | n/a | ISC | Not bundled; UDP protocol interoperability only |

---

## Mono.Cecil

Vendored at `vendor/mono-cecil/`, shipped in the installer ZIP and used as the
install-time source. Taken from the upstream release asset untouched; the
upstream licence file ships beside it at `vendor/mono-cecil/LICENSE`.

- Upstream: https://github.com/jbevain/cecil
- Version: `0.11.5`
- SHA-256: `9cf1706f35b4f209c28da7417608bed7a307621b0f0179c52258af78bc4668d0`

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

Git submodule at `cameraunlock-core/`, compiled into `EternalAfternoonHeadTracking.dll`. Our own code,
MIT licensed, reproduced here so the notices are complete.

- Pinned commit: `b4df73a5d8076968fcbf7e4088dd49db11a2684e`

```
MIT License

Copyright (c) 2026 itsloopyo

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
and compatible trackers can drive it. No OpenTrack code, headers or binaries
are copied, linked or redistributed, so its licence triggers no notice
obligation here. It is credited because the wire format is its work.

---

## Unity reference stubs

`cameraunlock-core/csharp/stubs/UnityStubs.cs` and `UnityUIStubs.cs` are
written by us. They declare the signatures of the Unity and uGUI members this
mod binds against, with empty or trivially re-derived bodies, so that the
project compiles on a machine with no game installed. They contain no Unity
Technologies source, no Unity binaries and no text from Unity's documentation,
and none of them ship: they are compiled into throwaway reference assemblies at
build time, and the real engine assemblies are loaded from the player's own
installation at runtime.

---

## Eternal Afternoon footage

- **File:** `assets/readme-clip.gif`, embedded at the top of `README.md`.
- **Rights holder:** Eternal Afternoon is copyright (c) Alex Klexber, its
  developer and publisher, together with the rights holders of any third-party
  marks visible in frame. The recording is their work, not ours. All we
  contributed is the camera movement the mod produces.
- **Usage:** about ten seconds of ordinary play, recorded from the game running
  with this mod on a legitimately purchased copy, shown so a reader can see what
  the mod does before installing it. It is not a cutscene, not a trailer and not
  re-uploaded marketing material.
- **Bundled:** no. It is kept in this repository only. The packaging scripts copy
  no part of `assets/`, so it reaches neither release ZIP nor anything the
  launcher deploys. The README references it by absolute URL so the copy inside a
  ZIP still resolves.
- **Licence:** none is granted or implied by this repository. This material is
  not covered by the MIT licence in `LICENSE`, which says so explicitly, and
  nothing here permits its reuse. If Alex Klexber would rather it were not
  published, open an issue or reach us on Discord and it comes down.

---

## Eternal Afternoon

Eternal Afternoon is the property of Alex Klexber. This mod is an unofficial
fan project and is not affiliated with, endorsed by or supported by them.
Eternal Afternoon and all related names, logos, characters and marks are
trademarks of their respective owners, used here only to identify the game
this mod applies to. That is nominative use and not a claim of any right in
them. The mod requires a legitimately purchased copy of the game. It
circumvents no DRM, licence check or anti-cheat. It interoperates with the
game by reflecting over type and member names at runtime, and by injecting a
call to our own bootstrap into `Assembly-CSharp.dll` at install time. Those
names are recorded in our source as plain strings, which are facts about the
interface rather than expression copied from it, and the installer keeps a
`.original` backup so uninstalling restores the shipped assembly byte for
byte. No decompiled or disassembled game code is stored in this repository.
