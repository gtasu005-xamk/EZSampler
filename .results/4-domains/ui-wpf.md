# Domain: ui-wpf

## Purpose
Defines the WPF application shell and main window for a Windows UI host.

## Key Files
- `./src/EZSampler.UI.Wpf/App.xaml`
- `./src/EZSampler.UI.Wpf/App.xaml.cs`
- `./src/EZSampler.UI.Wpf/MainWindow.xaml`
- `./src/EZSampler.UI.Wpf/MainWindow.xaml.cs`

## Observed Patterns
- Application startup is configured via `StartupUri` in `App.xaml`.
- UI is currently a simple `Window` with an empty `Grid`.
- Code-behind is minimal and only calls `InitializeComponent()`.

## Code Examples
```xaml
<Application x:Class="EZSampler.UI.Wpf.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
         
    </Application.Resources>
</Application>
```

```xaml
<Window x:Class="EZSampler.UI.Wpf.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="MainWindow" Height="450" Width="800">
    <Grid>

    </Grid>
</Window>
```

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

## Notes
- No MVVM infrastructure or dependency injection is present in the current UI.