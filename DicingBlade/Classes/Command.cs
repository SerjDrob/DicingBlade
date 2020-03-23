using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DicingBlade.Classes
{
    class Command : ICommand
    {
        public event EventHandler CanExecuteChanged 
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public Command(Action<object> action, Func<object,bool> func = null) 
        {
            ExecuteDelegate = action;
        }
        public Predicate<object> CanExecuteDelegate { get; set; }
        public Action<object> ExecuteDelegate { get; set; }
        public bool CanExecute(object parameter)
        {
            if (CanExecuteDelegate != null) 
            {
                return CanExecuteDelegate(parameter);
            }
            return true;
        }

        public void Execute(object parameter)
        {
            if (ExecuteDelegate != null)
            {
                ExecuteDelegate(parameter);
            }
        }
    }
}
