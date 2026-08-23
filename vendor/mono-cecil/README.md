# mono-cecil (vendored)

This directory contains a bundled copy of Mono.Cecil, the MIT-licensed .NET
assembly reading and writing library by Jb Evain. It is not a mod loader: this
mod has no loader, and Cecil is what `install.cmd` uses to inject a call to our
bootstrap into the game's `Assembly-CSharp.dll`, and what `uninstall.cmd` uses
to reverse it.

The vendored package is the install-time source of truth: `install.cmd` takes
`Mono.Cecil.dll` from here and never reaches out to the network. Refresh
manually with `pixi run update-deps`, review the diff, then commit.

## Snapshot

- Package: `Mono.Cecil.0.11.5.nupkg`
- Version: 0.11.5
- Upstream URL: https://www.nuget.org/api/v2/package/Mono.Cecil/0.11.5
- Project: https://github.com/jbevain/cecil
- SHA-256: `9cf1706f35b4f209c28da7417608bed7a307621b0f0179c52258af78bc4668d0`
- Fetched at: 2026-05-02T11:41:34.9066681+01:00
- Source: direct-url

Taken from upstream unmodified. The upstream licence sits beside it in
`LICENSE`, and is reproduced in the repository's `THIRD-PARTY-NOTICES.md`.
