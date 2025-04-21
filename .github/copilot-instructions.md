## Project Overview

- This is a 3D mobile game developed in **Unity 6** on **macOS** (Apple Silicon).
- The game features bases that spawn units, which follow player-drawn dashed paths to attack enemy bases and enemy units.
- The scene uses an 80x35 meter Plane as the ground, an orthographic camera, and a state-driven architecture with modes like PathDrawing.
- The codebase is written in **C#**, targeting mobile platforms (iOS/Android) with performance optimization.

## Developer Background

- I am an experienced programmer new to C# and Unity.
- I understand general programming concepts but need guidance on Unity-specific patterns, C# syntax, and best practices for mobile game development with Unity.

## Coding Guidelines

- Write all code in **C#** using Unity 6 APIs.
- Use C# with Unity best practices, game development best practices for small projects
- Minimize garbage collection (avoid frequent `new` allocations).
- Prefer **component-driven architecture** with `MonoBehaviour` scripts over centralized managers unless necessary.
- Name variables and methods descriptively
- Follow C# naming conventions: `PascalCase` for public members, `camelCase` for private fields

- SDK: ARM64 .NET SDK for Apple Silicon compatibility.

## Copilot Preferences

- Include **comments** explaining complex logic, especially for Unity-specific features like coroutines or physics.
- Keep existing comments when possible, especially the TODO comments.
