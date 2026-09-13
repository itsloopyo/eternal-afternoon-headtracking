# Eternal Afternoon Head Tracking

![Eternal Afternoon running with this mod](https://raw.githubusercontent.com/itsloopyo/eternal-afternoon-headtracking/main/assets/readme-clip.gif)

*Footage of [Eternal Afternoon](https://store.steampowered.com/app/3924170/Eternal_Afternoon/), copyright (c) Alex Klexber, recorded on a purchased copy with this mod running. Used to show what the mod does; no ownership is claimed and no licence to it is granted. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).*

An unofficial head tracking mod for Eternal Afternoon that moves the view with your head while your mouse or controller keeps control of look and interaction, driven by OpenTrack over UDP, with no VR headset required.

## Features

- **Decoupled look and aim** - head tracking moves the camera; aim stays on your mouse/controller
- **6DOF positional tracking** - lean and peek with head position
- **Works with any OpenTrack compatible tracker** - free options available for PC, iOS and Android

## Requirements

- [Eternal Afternoon](https://store.steampowered.com/app/3924170/Eternal_Afternoon/) (Steam)
- [OpenTrack](https://github.com/opentrack/opentrack) or a compatible head tracking app (smartphone, webcam, or dedicated hardware)
- Windows 10/11 (64-bit)

## Installation

### Lopari

Download [Lopari](https://lopari.app), choose **Eternal Afternoon**, and click
**Play with head tracking**.

### Standalone Installer

1. Download the latest release from the [Releases page](https://github.com/itsloopyo/eternal-afternoon-headtracking/releases)
2. Extract the ZIP anywhere
3. Double-click `install.cmd`
4. Configure OpenTrack to output UDP to `127.0.0.1:4242`
5. Launch the game

The installer automatically finds your game via Steam registry lookup and patches `Assembly-CSharp.dll`. No mod loader (BepInEx/MelonLoader) is required. If the installer can't find the game:

- Set the `ETERNALAFTERNOON_PATH` environment variable to your game folder, or
- Run from command prompt: `install.cmd "D:\Games\Eternal Afternoon"`

### Manual Installation

If you prefer to place files by hand, or you grabbed the `-nexus` package:

1. Run `install.cmd` at least once against any copy of the game so `Assembly-CSharp.dll` gets patched (the mod is loaded via IL injection into that assembly, not via a separate mod loader)
2. Extract the Nexus ZIP into your game folder - the DLLs will land in `Eternal Afternoon_Data/Managed/`:
   - `EternalAfternoonHeadTracking.dll`
   - `CameraUnlock.Core.dll`
   - `CameraUnlock.Core.Unity.dll`
3. Configure your tracker to output UDP to `127.0.0.1:4242`
4. Launch the game

## Setting Up OpenTrack

The mod listens for OpenTrack pose data on UDP port `4242`, on every network
interface. One datagram is six little-endian 64-bit floats in the order
`x, y, z, yaw, pitch, roll`: position in centimetres, rotation in degrees, 48
bytes in total. Anything that sends that to that port drives the view.
OpenTrack's **UDP over network** output sends exactly this, and the steps below
set it up.

1. Install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. Pick a tracker under **Input**, using the notes below.
3. Set **Output** to **UDP over network**, host `127.0.0.1`, port `4242`.
4. Press **Start**. Tracking and the game can start in either order.

### Webcam

OpenTrack ships a `neuralnet tracker` input that reads a plain webcam. Select it
under **Input**, pick your camera in its settings, and use the output settings
above. How well it tracks depends on your camera and your lighting, so try it
before buying anything.

### Phone

A phone app can reach the mod directly, with no OpenTrack on the PC, if it sends
the datagram described above. Point it at this PC's IP address (run `ipconfig`
to find it) on port `4242`. Not every phone tracker speaks this protocol, so
check yours for an OpenTrack or UDP output option first. [Headcam](https://headcam.app)
sends it, and I wrote it so decent tracking is free for anyone who already owns
a phone.

Sending direct works when the app filters its own signal on the device. The
mod's smoothing is sized to take the edge off a clean signal rather than to
rescue a noisy one, so a raw feed sent direct will jitter. If it does, point the
app at OpenTrack's **UDP over network** *input* on some other port, say 5252,
and let OpenTrack's filters and curves clean it up before its output forwards to
`127.0.0.1:4242`.

Anything arriving from outside `127.0.0.0/8` counts as a remote connection and
is smoothed with `RemoteSmoothing` rather than `LocalSmoothing`. That includes a
tracker on this very PC that sends to the machine's own LAN address, because the
mod reads the source address and not the machine.

### Headset or other hardware

If your device has an OpenTrack input driver, select it under **Input** and use
the same output settings. OpenTrack's own **Input** list is the authority on
what it can read; the mod only ever sees what OpenTrack sends.

### Centring

Centring belongs to your tracker. The mod subtracts no centre of its own: it
applies the pose it receives exactly as it arrives, so a stream of zeros holds
the view where the game itself puts it. Press the centre control in your tracker
(OpenTrack's **Center** bind, or the CENTER button in Headcam) and the tracker
zeroes its own output, which leaves the view centred with the mod doing nothing.

That is why there is no centre hotkey here and nothing to re-centre in game. Two
centres in series would drift apart, because each side re-centres at moments the
other cannot see, and you would end up pressing twice to centre once. If the
view sits off to one side, centre it in the tracker.

## Controls

Two equivalent binding sets - use whichever your keyboard has:

| Action               | Nav-cluster | Chord          |
|----------------------|-------------|----------------|
| Toggle head tracking | `End`       | `Ctrl+Shift+Y` |
| Cycle tracking mode  | `Page Up`   | `Ctrl+Shift+G` |
| Toggle yaw mode      | `Page Down` | `Ctrl+Shift+H` |
| Toggle aim reticle   | `Insert`    | `Ctrl+Shift+U` |

`Page Up` / `Ctrl+Shift+G` cycles tracking mode:

1. Normal head-tracked gameplay (rotation + position)
2. Positional tracking disabled, rotational tracking enabled
3. Rotational tracking disabled, positional tracking enabled
4. Back to normal

The chord letters sit in a vertical strip in the center of the keyboard. `Ctrl+Shift+<letter>` is universally avoided by games, so the chord set works whether or not your keyboard has a nav cluster.

## Configuration

The mod creates `HeadTracking.cfg` in the game's Managed folder (`Eternal Afternoon_Data/Managed/`) on first run. Edit it and restart the game to apply changes.

```ini
# --- Network ---
UdpPort = 4242

# --- Keybindings (see https://docs.unity3d.com/ScriptReference/KeyCode.html) ---
ToggleKey = End
PositionToggleKey = PageUp
ReticleToggleKey = Insert
YawModeKey = PageDown

# --- Yaw Mode ---
# true = horizon-locked yaw (default), false = camera-local yaw.
WorldSpaceYaw = true

# --- Sensitivity ---
YawSensitivity = 1.0
PitchSensitivity = 1.0
RollSensitivity = 1.0

# --- Smoothing ---
# Picked per connection from the tracker's source address. Both values
# cover rotation and position. 0.0 = no smoothing, 1.0 = heavy.
# LocalSmoothing: tracker running on this machine (loopback).
# RemoteSmoothing: tracker on a remote device over the network.
LocalSmoothing = 0.0
RemoteSmoothing = 0.15

# --- Position Tracking ---
PositionSensitivityX = 1.0
PositionSensitivityY = 1.0
PositionSensitivityZ = 1.0
InvertPositionX = true
InvertPositionY = false
InvertTrackerZ = false

# --- Reticle ---
ShowReticle = true
ReticleColor = 1.0,1.0,1.0,1.0
```

Delete the file to reset to defaults.

## Troubleshooting

**Mod not loading:**
- Check `HeadTracking_BOOT.log` in `Eternal Afternoon_Data/Managed/` for bootstrap messages
- Check `HeadTracking_BOOT_ERROR.log` in your temp folder (`%TEMP%`) for bootstrap errors
- Check `HeadTracking.log` in the Managed folder for mod errors. This is the
  main log: it records the port it listened on and an `OpenTrack connected` line
  the moment the first tracker packet arrives. Send this file when reporting a
  problem.
- Verify game files through Steam and re-run `install.cmd`

All three logs are rewritten from scratch on every game launch, so they only ever
contain the most recent session.

**No tracking response:**
- Verify OpenTrack is running and outputting data
- Check UDP port matches (default 4242)
- Press **End** to enable tracking
- Check firewall isn't blocking UDP port 4242

**View is off-centre:**
- Centre it in your tracker app: OpenTrack's Center hotkey, or the centre button in your phone app. The mod keeps no centre of its own, it applies whatever pose the tracker sends.

**Jittery / unstable tracking:**
- Increase `RemoteSmoothing` (phone/network tracker) or `LocalSmoothing` (tracker on this PC) in `HeadTracking.cfg`
- Reduce sensitivity values in the config
- Improve lighting for webcam-based tracking

**Wrong rotation axis:**
- Flip the relevant `InvertPositionX/Y/Z` flag in `HeadTracking.cfg`
- If rotation feels mirrored, check OpenTrack's output mapping (invert the offending axis at the source rather than in the mod)

**Yaw feels wrong when looking up or down at extreme angles:**
- Try toggling between world-locked and camera-local yaw with `Page Down`. World-locked (default) is horizon-stable; camera-local follows the camera's current up-axis.

## Updating

Download the new release and run `install.cmd` again. Your config is preserved.

## Uninstalling

Run `uninstall.cmd` from the release folder. This removes the mod DLLs and restores `Assembly-CSharp.dll` from its `.original` backup. No mod loader to remove - this mod patches `Assembly-CSharp.dll` directly rather than shipping BepInEx or MelonLoader.

If something is stuck, use:

```
uninstall.cmd /force
```

to remove everything the installer touched regardless of the state file.

## Building from Source

### Prerequisites

- Windows 10/11
- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- [pixi](https://pixi.sh) task runner
- Eternal Afternoon installed via Steam (for Unity reference DLLs)

### Build

```bash
git clone --recurse-submodules https://github.com/itsloopyo/eternal-afternoon-headtracking.git
cd eternal-afternoon-headtracking

# Build and install to game
pixi run install

# Build only
pixi run build

# Package for release
pixi run package
```

## Community & Support

- Discord: [Loop's Head Tracking Hangout](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch for the released head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your iPhone or Android phone into the head tracker

## License

MIT License - see [LICENSE](LICENSE) for details.

## Credits

- [Alex Klexber](https://store.steampowered.com/app/3924170/Eternal_Afternoon/) - Eternal Afternoon
- [OpenTrack](https://github.com/opentrack/opentrack) - Head tracking protocol and software
- [Mono.Cecil](https://github.com/jbevain/cecil) - .NET assembly manipulation (used for install-time IL injection)

## Disclaimer

This mod is not affiliated with, endorsed by, or supported by Klexber or the Eternal Afternoon development team. Use at your own risk.
