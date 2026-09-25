using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace LocationBeacon;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        base.OnStart();
        Debug.WriteLine("App started");
    }

    protected override void OnResume()
    {
        base.OnResume();
        Debug.WriteLine("App resumed");
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        Debug.WriteLine("App backgrounded");
        // Beacon continues running in background
    }
}