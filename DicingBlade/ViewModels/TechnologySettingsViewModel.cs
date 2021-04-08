using System.Linq;
using DicingBlade.Classes;
using DicingBlade.Properties;
using PropertyChanged;
using System.ComponentModel;
using System.Windows.Input;
using System.IO;
using Microsoft.Win32;

namespace DicingBlade.ViewModels
{

    [AddINotifyPropertyChangedInterface]
    public class TechnologySettingsViewModel : ITechnology, IDataErrorInfo
    {
        public TechnologySettingsViewModel()
        {
            _validator = new TechnologySettingsValidator();
            CloseCmd = new Command(args => ClosingWnd());
            OpenFileCmd = new Command(args => OpenFile());
            SaveFileAsCmd = new Command(args => SaveFileAs());
            FileName = Settings.Default.TechnologyLastFile;
            if (FileName == null | !File.Exists(FileName))
            {
                SpindleFreq = 25000;
                FeedSpeed = 2;
                WaferBladeGap = 1;
                FilmThickness = 0.08;
                UnterCut = 0;
                PassCount = 1;
                PassType = Directions.Direct;
                StartControlNum = 3;
                ControlPeriod = 3;
                PassType = Directions.Direct;
            }
            else
            {
                ((ITechnology)StatMethods.DeSerializeObjectJson<Technology>(FileName)).CopyPropertiesTo(this);
            }

        }
        public string FileName { get; set; }
        public int SpindleFreq { get; set; }
        public double FeedSpeed { get; set; }
        public double WaferBladeGap { get; set; }
        public double FilmThickness { get; set; }
        public double UnterCut { get; set; }
        public int PassCount { get; set; }
        public Directions PassType { get; set; }
        public int StartControlNum { get; set; }
        public int ControlPeriod { get; set; }
        public ICommand CloseCmd { get; set; }
        public ICommand OpenFileCmd { get; set; }
        public ICommand SaveFileAsCmd { get; set; }
        private void ClosingWnd()
        {
            PropContainer.Technology = this;
            new Technology(PropContainer.Technology).SerializeObjectJson(PropContainer.Technology.FileName);
            Settings.Default.TechnologyLastFile = PropContainer.Technology.FileName;
            Settings.Default.Save();
        }

        private void OpenFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Файлы технологии (*.json)|*.json",
            };

            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                FileName = dialog.FileName;
                ((ITechnology)StatMethods.DeSerializeObjectJson<Technology>(FileName)).CopyPropertiesTo(this);
            }
        }

        private void SaveFileAs()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Файлы технологии (*.json)|*.json",
            };

            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                FileName = dialog.FileName;
                ClosingWnd();
            }
        }
        public string Error =>
            //if (validator != null)
            //{
            //    var results = validator.Validate(this);
            //    if (results != null && results.Errors.Any())
            //    {
            //        var errors = string.Join(Environment.NewLine, results.Errors.Select(x => x.ErrorMessage).ToArray());
            //        return errors;
            //    }
            //}
            string.Empty;

        private readonly TechnologySettingsValidator _validator;
        public string this[string columnName]
        {
            get
            {
                var firstOrDefault = _validator.Validate(this).Errors.FirstOrDefault(lol => lol.PropertyName == columnName);
                if (firstOrDefault != null)
                    return _validator != null ? firstOrDefault.ErrorMessage : string.Empty;
                return string.Empty;
            }
        }

    }
}
