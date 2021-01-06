using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    public interface Messager
    {        
        public event Action<string> ThrowMessage;
    }
    public class ExceptionsAgregator
    {
        private static readonly ExceptionsAgregator instance = new ExceptionsAgregator();
        private ExceptionsAgregator()
        {            
        }

        public static ExceptionsAgregator GetExceptionsAgregator()
        {            
            return instance;
        }
        public void RegisterMessager(Messager messager)
        {
            messager.ThrowMessage += Messager_ThrowMessage;
        }
        public void SetShowMethod(Action<string> method)
        {
            ShowMessage = method;
        }
        private Action<string> ShowMessage;
        private void Messager_ThrowMessage(string message)
        {
            ShowMessage?.Invoke(message);
        }
    }
}
