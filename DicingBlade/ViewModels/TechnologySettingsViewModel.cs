using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DicingBlade.Classes;
using PropertyChanged;
using FluentValidation.Results;
using System.Windows.Forms;
using System.ComponentModel;
using System.Windows.Input;

namespace DicingBlade.ViewModels
{
   
    [AddINotifyPropertyChangedInterface]
    public class TechnologySettingsViewModel:ITechnology,IDataErrorInfo
    {        
        public TechnologySettingsViewModel() 
        {            
            validator = new TechnologySettingsValidator();
            CloseCmd = new Command(args => ClosingWnd());
            if (FileName == null) 
            {
                SpindleFreq = 25000;
                FeedSpeed = 2;
                WaferBladeGap = 1;
                FilmThickness=0.08;
                UnterCut = 0;
                PassCount = 1;
                PassType = Directions.direct;
                StartControlNum = 3;
                ControlPeriod = 3;
                PassType = Directions.direct;
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
        public ICommand CloseCmd{ get; set; }
        private void ClosingWnd() 
        {
            PropContainer.Technology = this;
        }

        public string Error
        {
            get
            {
                if (validator != null)
                {
                    var results = validator.Validate(this);
                    if (results != null && results.Errors.Any())
                    {
                        var errors = string.Join(Environment.NewLine, results.Errors.Select(x => x.ErrorMessage).ToArray());
                        return errors;
                    }
                }
                return string.Empty;
            }
        }
       
        private TechnologySettingsValidator validator;
        public string this[string columnName]
        {
            get
            {
                var firstOrDefault = validator.Validate(this).Errors.FirstOrDefault(lol => lol.PropertyName == columnName);
                if (firstOrDefault != null)
                    return validator != null ? firstOrDefault.ErrorMessage : "";
                return "";               
            }
        }
       
    }
}
