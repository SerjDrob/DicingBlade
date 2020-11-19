﻿using System;
using System.Windows;
using DicingBlade.ViewModels;

namespace DicingBlade
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {

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
