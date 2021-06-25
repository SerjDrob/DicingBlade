#nullable enable

using System;
using System.Windows;
using DicingBlade.Classes;
using DicingBlade.ViewModels;
using DicingBlade.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DicingBlade
{
    /// <summary>
    ///     Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IHost _host;
        private readonly IServiceProvider _serviceProvider;

        public App()
        {
            var hostBuilder = new HostBuilder();
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                //Add business services as needed
                services.AddSingleton<MotionDevice>();
                services.AddSingleton<IVideoCapture, USBCamera>();
                services.AddSingleton<IMachine, Machine4X>();
                services.AddSingleton<IMainViewModel, MainViewModel>();
                services.AddSingleton<ExceptionsAgregator>();
                services.AddSingleton<MainWindowView>();
            });

            _host = hostBuilder.Build();
            _serviceProvider = _host.Services;
        }


        private void HandleOnStartup(object sender, StartupEventArgs e)
        {
            var mainWindow = _serviceProvider.GetService<MainWindowView>();
            mainWindow!.Show();
        }

        private async void HandleOnExit(object sender, ExitEventArgs e)
        {
            using (_host)
            {
                await _host.StopAsync().ConfigureAwait(false);
            }
        }
    }
}