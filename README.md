# Rick and Morty: Virtual Rick-ality: iOS port (no VR)

Tools, touch controls and CI for running *Rick and Morty: Virtual Rick-ality* (Owlchemy Labs / Adult Swim Games)
on a jailbroken / TrollStore iPhone as a flat-screen game. **This repository contains no game files.** You need your
own copy of the game; everything derived from it lives in a private repository.

- `spike/`: empty Unity 5.4.3p4 project used to prove CI can install, license and IL2CPP-build Unity 5.4 for iOS.
- `ci/unity.sh`: runs the Unity editor in batch mode with a watchdog.
