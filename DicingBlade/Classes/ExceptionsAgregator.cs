using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    public interface IMessager
    {        
        public event Action<string,int> ThrowMessage;
    }
    public class ExceptionsAgregator
    {
        private static readonly ExceptionsAgregator instance = new ExceptionsAgregator();
        private ExceptionsAgregator()
        {            
        }
        private List<Action<string>> _MessagesActions = new();

        public static ExceptionsAgregator GetExceptionsAgregator()
        {            
            return instance;
        }
        public void RegisterMessager(IMessager messager)
        {
            messager.ThrowMessage += Messager_ThrowMessage;
        }
        public void SetShowMethod(Action<string> method)
        {
            _MessagesActions.Add(method);
            //ShowMessage = method;
        }
        //private Action<string> ShowMessage;
        private void Messager_ThrowMessage(string message, int methodNum)
        {
            if (methodNum<0 | methodNum > _MessagesActions.Count-1)
            {
                throw new Exception();
            }
            _MessagesActions[methodNum]?.Invoke(message);
            //ShowMessage?.Invoke(message);
        }
    }
}
