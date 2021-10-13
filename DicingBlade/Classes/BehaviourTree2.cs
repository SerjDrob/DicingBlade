using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Microsoft.VisualStudio.Workspace;


namespace DicingBlade.Classes.Test
{
   

    public class Leaf : Worker
    {
        private Func<Task> _myAction;       
        
        public Leaf(Func<Task> myWork)
        {           
            _myAction = myWork;
        }
        public override async Task<bool> DoWork()
        {
            if (_notBlocked)
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
        public override Leaf SetBlock(Block block)
        {
            base.SetBlock(block);
            return this;
        }
        public override Leaf SayMyName(string name)
        {
            base.SayMyName(name);
            return this;
        }
    }
    public class Sequence : Worker
    {
        private List<Worker> _workers;
        private PauseTokenSource _pauseTokenAfterWork;
        public Sequence()
        {
            _workers = new List<Worker>();
            _pauseTokenAfterWork = new();
        }
        public void SubscribeAllOnCheckEvent(Action<string> action)
        {
            this.CheckBeforeWorking += action;
            _workers?.ForEach(worker =>
            {
                switch (worker)
                {
                    case Sequence sequence:
                        sequence.SubscribeAllOnCheckEvent(action);
                        break;
                    case Ticker ticker:
                        ticker.SubscribeAllOnCheckEvent(action);
                        break;
                    case Leaf leaf:
                        leaf.CheckBeforeWorking += action;
                        break;
                    default:
                        break;
                }
            }
            );
        }
        public override Sequence SetBlock(Block block)
        {
            base.SetBlock(block);
            return this;
        }
        public Sequence Hire(Worker worker)
        {
            _workers.Add(worker);
            switch (worker)
            {
                case Sequence sequence:
                    sequence.EnslaveMe(_pauseTokenAfterWork);                    
                    break;
                case Ticker ticker:
                    ticker.EnslaveMe(_pauseTokenAfterWork);                      
                    break;
                default:
                    break;
            }
            return this;
        }
        private void SetWaters() 
        {            
            _pauseTokenAfterWork?.Pause();
        }
        public void EnslaveMe(PauseTokenSource pauseTokenSource)
        {
            _pauseTokenAfterWork = pauseTokenSource;
        }
        public void ResumeWaitersWork() 
        {
            _pauseTokenAfterWork?.Resume();
        }
        /// <summary>
        /// Makes last added worker wait for resuming after the work's done
        /// </summary>
        /// <returns>The Sequence</returns>
        public Sequence WaitForMe()
        {
            _workers.Last().WaitMeAfterWorkDone = true;
            return this;
        }
        public override async Task<bool> DoWork()
        {
            if (_notBlocked)
            {
                await base.DoWork();
                try
                {
                    _workers?.ForEach(async worker => {
                        SetWaters();
                        await worker.DoWork();
                        if (worker.WaitMeAfterWorkDone & _pauseTokenAfterWork is not null)
                        {
                            await _pauseTokenAfterWork.Token.WaitWhilePausedAsync();
                        }                        
                    });
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
        public override Sequence SayMyName(string name)
        {
            base.SayMyName(name);
            return this;
        }
    }
    public class Ticker : Worker
    {
        private Worker _worker;
        private PauseTokenSource _pauseTokenAfterWork;
        public Ticker Hire(Worker worker)
        {
            _worker = worker;
            switch (worker)
            {
                case Sequence sequence:
                    sequence.EnslaveMe(_pauseTokenAfterWork);
                    break;
                case Ticker ticker:
                    ticker.EnslaveMe(_pauseTokenAfterWork);
                    break;
                default:
                    break;
            }
            return this;
        }
        public void SubscribeAllOnCheckEvent(Action<string> action)
        {
            this.CheckBeforeWorking += action;
            switch (_worker)
            {
                case Sequence sequence:
                    sequence.SubscribeAllOnCheckEvent(action);
                    break;
                case Ticker ticker:
                    ticker.SubscribeAllOnCheckEvent(action);    
                    break;
                case Leaf leaf:
                    leaf.CheckBeforeWorking += action;
                    break;
                default:
                    break;
            }            
        }
        public void ResumeWaitersWork()
        {
            _pauseTokenAfterWork.Resume();
        }
        public void EnslaveMe(PauseTokenSource pauseTokenSource)
        {
            _pauseTokenAfterWork = pauseTokenSource;
        }
        public override Ticker SetBlock(Block block)
        {
            base.SetBlock(block);
            return this;
        }
        public override async Task<bool> DoWork()
        {
            await base.DoWork();
            while (_notBlocked)
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
        public override Ticker SayMyName(string name)
        {
            base.SayMyName(name);
            return this;
        }
    }
    public abstract class Worker
    {
        private string _name;
        public virtual Worker SayMyName(string name)
        {
            _name = name;
            return this;
        }
        private bool _waitMeAfterWorkDone = false;
        public bool WaitMeAfterWorkDone 
        { 
            get => _waitMeAfterWorkDone & _notBlocked;
            set { _waitMeAfterWorkDone = value; }
        }        
        public virtual Worker SetBlock(Block block)
        {
            _blocks.Add(block);
            return this;
        }
        public event Action<string> CheckBeforeWorking;
        public virtual async Task<bool> DoWork()
        {
            CheckBeforeWorking?.Invoke(_name);
            if (_notBlocked & _pauseTokenBeforeWork is not null)
            {
                await _pauseTokenBeforeWork?.Token.WaitWhilePausedAsync(); 
            }
            return true;
        }
        private List<Block> _blocks = new();
        protected bool _notBlocked
        {
            get => _blocks.All(b => b.NotBlocked);
        }
        protected PauseTokenSource _pauseTokenBeforeWork;        
        public virtual void SetPauseToken(PauseTokenSource pauseTokenSource)
        {
            _pauseTokenBeforeWork = pauseTokenSource;
        }
        public void PauseMe()
        {
            _pauseTokenBeforeWork?.Pause();
        }

        public void ResumeMe()
        {
            _pauseTokenBeforeWork?.Resume();
        }
    }
    public class Block
    {
        public Block BlockMe() 
        {
            NotBlocked = false;
            return this;
        }
        public Block UnBlockMe()
        {
            NotBlocked = true;
            return this;
        }
        public bool NotBlocked { get; private set; } = true;
    }

}
