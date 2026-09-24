# Building Persona Director

The repository keeps the game and mod assemblies out of source control. The project references the local RimWorld, Harmony, and RimTalk assemblies on your machine.

## Requirements

- Visual Studio with the .NET desktop development workload
- The .NET Framework 4.8 Developer Pack
- RimWorld 1.6, Harmony, and RimTalk installed locally

## Configure local references

1. Copy `RPD.Local.props.example` to `RPD.Local.props` in the repository root.
2. Set the three paths in `RPD.Local.props`:
   - `RimWorldManagedDir`: the RimWorld `RimWorldWin64_Data/Managed` directory containing `Assembly-CSharp.dll` and Unity assemblies.
   - `HarmonyAssemblyPath`: the installed `0Harmony.dll` file.
   - `RimTalkAssemblyPath`: the installed `RimTalk.dll` file.
3. Open `RPD.sln` and build the **Release** configuration.

The output is `bin/Release/RPD.dll`. After reviewing the build, copy it to `Assemblies/RPD.dll` when preparing an updated mod package. The `.gitignore` excludes local paths and build output.

Do not copy RimWorld, Harmony, or RimTalk dependency assemblies into this repository; the project references them from their installed locations.
