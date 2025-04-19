# 3D Mobile Game in Unity 6
## Overview
A strategy game where players control bases that spawn units. Units follow player-drawn dashed paths to attack enemy bases. Built in Unity 6 on macOS for mobile (touch input) and testing (mouse input).

## Scene Setup
- **Plane**: 80x35m (Scale: 8, 1, 3.5), ground surface.

## Mechanics
- Bases spawn units (one at Start, planned 5s timer after unit moves).
- Units move to `spawnTo` using Rigidbody, then follow player-drawn paths.
- Units ignore base collision while pending.
