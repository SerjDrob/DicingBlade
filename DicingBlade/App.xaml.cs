using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using DicingBlade.Classes;
using DicingBlade.ViewModels;
using MachineClassLibrary.Laser.Markers;
using MachineClassLibrary.Laser;
using MachineClassLibrary.Machine.Machines;
using MachineClassLibrary.Machine.MotionDevices;
using MachineClassLibrary.Machine;
using MachineClassLibrary.VideoCapture;
using Microsoft.Extensions.DependencyInjection;
using DicingBlade.Utility;
using System.IO;

namespace DicingBlade
{

    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        public ServiceCollection MainIoC { get; private set; }


        
        public App() 
        {
            var machineconfigs = ExtensionMethods
            .DeserilizeObject<MachineConfiguration>(Path.Combine(ProjectPath.GetFolderPath("AppSettings"), "MachineConfigs.json"));


            MainIoC = new ServiceCollection();

            MainIoC.AddMediatR(Assembly.GetExecutingAssembly())
                       //.AddSingleton<ISubject,Subject>()
                       .AddScoped<MotDevMock>()
                       .AddScoped<MotionDevicePCI1240U>()
                       .AddScoped<MotionDevicePCI1245E>()
                       .AddSingleton(sp =>
                       {
                           return new MotionBoardFactory(sp, machineconfigs).GetMotionBoard();
                       })
                       .AddSingleton<ExceptionsAgregator>()
                       .AddScoped<JCZLaser>()
                       .AddScoped<MockLaser>()
                       .AddScoped<PWM>()
                       .AddScoped<PWM2>()
                       .AddSingleton(sp =>
                       {
                           return new LaserBoardFactory(sp, machineconfigs).GetPWM();
                       })
                       .AddSingleton(sp =>
                       {
                           return new LaserBoardFactory(sp, machineconfigs).GetLaserBoard();
                       })
                       .AddScoped<IVideoCapture, USBCamera>()
                       .AddSingleton<LaserMachine>()
                       .AddSingleton<MainViewModel>()
                       .AddDbContext<DbContext, LaserDbContext>()
                       ;
        }
        protected override void OnStartup(StartupEventArgs e)
        {          
            new Views.MainWindowView()
            {
                DataContext = new MainViewModel()
            }.Show();
        }
        protected override void OnExit(ExitEventArgs e)
        {
            Environment.Exit(0);
            base.OnExit(e);
        }

    }
}
