# Style Guide: project-files

## Scope
C# project files for console, core library, and WPF UI.

## Observed Conventions
- SDK-style .NET projects.
- Nullable reference types and implicit usings are enabled.
- Core library references NAudio.
- Console and WPF projects reference `EZSampler.Core` via `ProjectReference`.

## Examples
- `./src/EZSampler.Console/EZSampler.Console.csproj`
- `./src/EZSampler.core/EZSampler.Core.csproj`
- `./src/EZSampler.UI.Wpf/EZSampler.UI.Wpf.csproj`
