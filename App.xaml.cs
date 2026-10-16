using HeatMapTracker.Views;

namespace HeatMapTracker;

/// <summary>Application shell host. Resolves the main page from the DI container.</summary>
public partial class App : Application
{
    private readonly MainView _mainView;

    public App(MainView mainView)
    {
        InitializeComponent();
        _mainView = mainView ?? throw new ArgumentNullException(nameof(mainView));
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new NavigationPage(_mainView));
}
