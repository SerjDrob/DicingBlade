using PropertyChanged;
using DicingBlade.Classes;
using System.Windows.Input;
using System.Windows;
using DicingBlade.Properties;
using System.IO;
using Microsoft.Win32;
using System;

namespace DicingBlade.ViewModels
{
    public class WatchSettingsService
    {
        private object _settings;
        public object Settings
        {
            set
            {
                if (value!=_settings)
                {
                    _settings = value;
                    OnSettingsChangedEvent?.Invoke(this,new SettingsChangedEventArgs(_settings));
                }

            }
        }

        public event EventHandler<SettingsChangedEventArgs> OnSettingsChangedEvent;
    }

    public class SettingsChangedEventArgs : EventArgs
    {
        private object _settings;
        public object Settings
        {
            get => _settings;
        }
        public SettingsChangedEventArgs(object settings)
        {
            _settings = settings;
        }
    }

    [AddINotifyPropertyChangedInterface]
    internal class WaferSettingsViewModel : IWafer
    {
        private bool _isRound;
        public bool IsRound
        {
            get => _isRound;
            set
            {
                _isRound = value;
                SquareVisibility = value ? Visibility.Collapsed : Visibility.Visible;
            }
        }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Thickness { get; set; }
        public double IndexW { get; set; }
        public double IndexH { get; set; }
        public double Diameter { get; set; }
        public string FileName { get; set; }
        public Visibility SquareVisibility { get; set; }
        public Wafer Wafer { get; set; }
        private readonly WatchSettingsService settingsService;
        public WaferSettingsViewModel(WatchSettingsService settingsService)
        {
            this.settingsService = settingsService;
            CloseCmd = new Command(args => ClosingWnd());
            OpenFileCmd = new Command(args => OpenFile());
            SaveFileAsCmd = new Command(args => SaveFileAs());
            ChangeShapeCmd = new Command(args => ChangeShape());
            FileName = Settings.Default.WaferLastFile;
            if (FileName == string.Empty | !File.Exists(FileName))
            {
                SquareVisibility = Visibility.Visible;
                Width = 30;
                Height = 10;
                Thickness = 1;
                IndexH = 1;
                IndexW = 2;
                Diameter = 40;
            }
            else
            {
                ((IWafer)StatMethods.DeSerializeObjectJson<TempWafer>(FileName)).CopyPropertiesTo(this);
            }
        }

        

        public ICommand CloseCmd { get; set; }
        public ICommand OpenFileCmd { get; set; }
        public ICommand SaveFileAsCmd { get; set; }
        public ICommand ChangeShapeCmd { get; set; }
        public void SetCurrentIndex(double index)
        {
            switch (CurrentSide)
            {
                case 0:
                    IndexH = index;
                    break;
                case 1:
                    IndexW = index;
                    break;
                default:
                    break;
            };
        }
        public int CurrentSide { get; set; }

        private void ClosingWnd()
        {
            PropContainer.WaferTemp = this;
            new TempWafer(PropContainer.WaferTemp).SerializeObjectJson(FileName);
            Settings.Default.WaferLastFile = FileName;
            Settings.Default.Save();
            settingsService.Settings = this;
        }

        private void ChangeShape()
        {
            IsRound ^= true;
        }
        private void OpenFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Файлы пластины (*.json)|*.json",
            };

            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                FileName = dialog.FileName;
                ((IWafer)StatMethods.DeSerializeObjectJson<TempWafer>(FileName)).CopyPropertiesTo(this);
            }
        }
        private void SaveFileAs()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Файлы пластины (*.json)|*.json",
            };

            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                FileName = dialog.FileName;
                ClosingWnd();
            }
        }

    }
}
