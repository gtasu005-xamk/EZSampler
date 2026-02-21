# Domain: console-host

## Purpose
Provides a console-based host for running the capture service interactively.

## Key Files
- `./src/EZSampler.Console/Program.cs`

## Observed Patterns
- The console host prompts the user and blocks on `Console.ReadLine()`.
- Capture lifecycle is managed directly with `CaptureService`.

## Code Examples
```csharp
Console.WriteLine("EZSampler CaptureService test host");
Console.WriteLine("Press ENTER to start recording.");
Console.ReadLine();

await using var captureService = new CaptureService();

captureService.Faulted += (_, ex) =>
{
    Console.WriteLine($"Faulted: {ex.Message}");
};

await captureService.StartAsync(new CaptureOptions());
```

```csharp
Console.WriteLine("Recording... Press ENTER to stop.");
Console.ReadLine();

await captureService.StopAsync();
```

## Notes
- The console app does not configure device selection or custom options yet.