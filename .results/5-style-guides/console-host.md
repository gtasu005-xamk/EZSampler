# Style Guide: console-host

## Scope
Console host entrypoint for capture testing.

## Observed Conventions
- Uses top-level statements.
- Interaction is via `Console.WriteLine` and `Console.ReadLine` prompts.
- Capture lifecycle is orchestrated directly with `CaptureService`.

## Examples
- `./src/EZSampler.Console/Program.cs`
