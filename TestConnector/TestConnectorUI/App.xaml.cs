using System.Windows;

using Microsoft.Extensions.DependencyInjection;

using TestConnectorLib.Connector.Implementations;
using TestConnectorLib.Connector.Interfaces;

using TestConnectorUI.Navigation;
using TestConnectorUI.ViewModels;
using TestConnectorUI.ViewModels.Base;

namespace TestConnectorUI;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    private App()
    {
        var services = new ServiceCollection();

        _ = services.AddSingleton<Func<Type, BaseViewModel>>(provider =>
            type =>
            {
                var vm = (BaseViewModel)provider.GetRequiredService(type);
                return vm;
            }
        );
        _ = services.AddSingleton<NavigationStore>();

        _ = services.AddTransient<ITestConnector, TestConnectorBitfinex>();

        _ = services.AddTransient<MainViewModel>();
        _ = services.AddTransient<ConverterViewModel>();

        _ = services.AddSingleton<MainWindow>(provider =>
        {
            provider
                .GetRequiredService<NavigationStore>()
                .SetViewModel<ConverterViewModel>();

            return new()
            {
                DataContext = provider.GetRequiredService<MainViewModel>()
            };
        });

        _serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow.Show();

        base.OnStartup(e);
    }
}
