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
           
            public Leaf(Func<Task> myWork)
            {
                _myAction = myWork;
            }
            public override async Task<bool> DoWork()
            {
                if (!_block.IsBlocked)
                {
                    await base.DoWork();
                    try
                    {
                        await _myAction();
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                    return true; 
                }
                return false;
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
            public override Sequence SetBlock(Block block)
            {
                base.SetBlock(block);
                return this;
            }
            public Sequence Hire(Worker worker)
            {
                _workers.Add(worker);
                return this;
            }
            public override async Task<bool> DoWork()
            {
                if (!_block.IsBlocked)
                {
                    await base.DoWork();
                    try
                    {
                        _workers?.ForEach(worker => worker.DoWork());
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                    return true; 
                }
                return false;
            }

            public override void SetPauseToken(PauseTokenSource pauseTokenSource)
            {
                _workers.ForEach(worker => worker.SetPauseToken(pauseTokenSource));
            }
        }
        public class Ticker : Worker
        {
            private Worker _worker;
            public Ticker Hire(Worker worker)
            {
                _worker = worker;
                return this;
            }
            public override Ticker SetBlock(Block block)
            {
                base.SetBlock(block);
                return this;
            }
            public override async Task<bool> DoWork()
            {
                await base.DoWork();
                while (!_block.IsBlocked)
                {
                    try
                    {
                        await _worker.DoWork();
                    }
                    catch (Exception)
                    {
                        return false;                      
                    }
                }
                return true;
            }
            public override void SetPauseToken(PauseTokenSource pauseTokenSource)
            {
                _pauseTokenSource = pauseTokenSource;
            }
        }
        public abstract class Worker
        {           
            public virtual Worker SetBlock(Block block)
            {
                _block = block;
                return this;
            }
            public virtual async Task<bool> DoWork()
            {
                await _pauseTokenSource?.Token.WaitWhilePausedAsync();                    
                return true;
            }
            protected Block _block;
            protected PauseTokenSource _pauseTokenSource;
            public abstract void SetPauseToken(PauseTokenSource pauseTokenSource);
        }
        public class Block
        {
            public bool IsBlocked { get; set; } = false;
        }


        public class Test
        {
            Test()
            {
                var l = new Leaf(async ()=> { });
                               

            }
        }

    }
}
