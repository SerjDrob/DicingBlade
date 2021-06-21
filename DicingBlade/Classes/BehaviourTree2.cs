using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Workspace;

    namespace DicingBlade.Classes.Test
    {
        public class Leaf:Worker
        {            
            private Func<Task> _myAction;
           
            Leaf(Func<Task> myWork)
            {
                _myAction = myWork;
            }
            public override async Task<bool> DoWork()
            {
                base.DoWork();
                await _myAction();
                return true;
            }

            public override void SetPauseToken(PauseTokenSource pauseTokenSource)
            {
                _pauseTokenSource = pauseTokenSource;
            }
        }
        public class Sequence : Worker
        {
            private List<Worker> _workers;
            public Sequence()
            {
                _workers = new List<Worker>();  
            }
            public Sequence Hire(Worker worker)
            {
                _workers.Add(worker);
                return this;
            }
            public override async Task<bool> DoWork()
            {
                try
                {
                    _workers?.ForEach(worker => worker.DoWork());
                }
                catch (Exception)
                {
                    throw;
                }
                return true;
            }

            public override void SetPauseToken(PauseTokenSource pauseTokenSource)
            {
                _workers.ForEach(worker => worker.SetPauseToken(pauseTokenSource));
            }
        }
        public abstract class Worker
        {            
            public virtual async Task<bool> DoWork()
            {
                await _pauseTokenSource?.Token.WaitWhilePausedAsync();
                //KnowMyName?.Invoke(_myName);    
                return true;
            }
            protected PauseTokenSource _pauseTokenSource;
            public abstract void SetPauseToken(PauseTokenSource pauseTokenSource);
        }
    }
}
