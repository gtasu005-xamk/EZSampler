using EZSampler.Core.Capture;

Console.WriteLine("EZSampler CaptureService test host");
Console.WriteLine("Press ENTER to start recording.");
Console.ReadLine();

await using var captureService = new CaptureService();

captureService.StatusChanged += (_, status) =>
{
    if (status.State == CaptureState.Stopped)
    {
        return;
    }

};

captureService.Faulted += (_, ex) =>
{
    Console.WriteLine($"Faulted: {ex.Message}");
};

await captureService.StartAsync(new CaptureOptions());

Console.WriteLine("Recording... Press ENTER to stop.");
Console.ReadLine();

await captureService.StopAsync();

Console.WriteLine("Stopped. File saved on Desktop.");
Console.WriteLine("Press ENTER to exit.");
Console.ReadLine();
