using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    class BehaviourTree
    {
        public BehaviourTree(Sequence root)
        {
            this._root = root;
        }
        private Sequence _root;
    }
    
    class Sequence : DoMyWork
    {       
        public Sequence Hire(DoMyWork member)
        {            
            _myWorkers.Add(member);
            return this;
        }
        public Sequence SetBlock(Condition condition)
        {
            _myCondition = condition;
            return this;
        }
        public override event Action CheckMyCondition;
        private Condition _myCondition = new();
        private bool _withinCollection = false;
        private bool _success = true;
        private List<DoMyWork> _myWorkers = new();
        private List<DoMyWork>.Enumerator _enumerator;

        public void ResetEnumerator() 
        {
            _withinCollection = false;
            _success = true;
            ImWorking = false;
        }
        public override async Task<bool> DoWork()
        {
            if (!ImWorking)
            {
                if (!_withinCollection)
                {
                    _enumerator = _myWorkers.GetEnumerator();
                }
                if (_success)
                {
                    _withinCollection = _enumerator.MoveNext();
                    CheckMyCondition?.Invoke();
                    if (!_withinCollection & _myCondition.State)
                    {
                        _enumerator = _myWorkers.GetEnumerator();
                        _withinCollection = _enumerator.MoveNext();
                    }
                }
                if (_withinCollection)
                {
                    var worker = _enumerator.Current;
                    ImWorking = true;
                    _success = await worker.DoWork();
                    ImWorking = false;
                }
                CheckMyCondition?.Invoke();
                if (!_withinCollection & !_myCondition.State)
                {
                    return true;
                }
            }
            return false;
        }
        
        private void AddMembers(params DoMyWork[] members)
        {
            foreach (var member in members)
            {
                _myWorkers.Add(member);
            }
            _enumerator = _myWorkers.GetEnumerator();
        }       
    }
    class Leaf : DoMyWork
    {

        public Leaf(Func<Task> action, DoMyWork worker = null)
        {
            _myAction = action;
            _myWorker = worker;            
        }
        public Leaf Hire(Func<Task> action, DoMyWork worker = null)
        {
            _myAction = action;
            _myWorker = worker;
            return this;
        }

        public Leaf SetBlock(Condition condition)
        {
            _blockConditions.Add(condition);
            return this;
        }
        private List<Condition> _blockConditions = new();

        private bool _resetSeq = false;
        public DoMyWork CanResetSeq()
        {
            _resetSeq = true;
            return this;
        }
        private void ResetWorkerSeq()
        {
            if (_myWorker.GetType()==typeof(Sequence) & _resetSeq)
            {
                var worker = _myWorker as Sequence;
                worker.ResetEnumerator();
            }
        }
        private DoMyWork _myWorker;
        private Func<Task> _myAction;
        public override event Action CheckMyCondition;
        //public Condition BlockAction { get; set; } = new Condition();
        public bool IsRunning { get; private set; } = false;       
        public override async Task<bool> DoWork()
        {
            CheckMyCondition?.Invoke();

            if (CheckBlocks())
            {
                IsRunning = true;
                await _myAction();
                if (_myWorker is not null)
                {
                    ResetWorkerSeq();
                    await _myWorker.DoWork();
                }                
                IsRunning = false;
            }

            return true;
        }
        private bool CheckBlocks()
        {
            if (_blockConditions.Count==0)
            {
                return true;
            }
            else
            {                
                return !(_blockConditions.Where(c => c.State == false).Count() > 0);
            }
           
        }
    }
    class Selector : DoMyWork
    {
        public DoMyWork Hire(params (DoMyWork worker, Condition condition)[] members)
        {
            AddMembers(members);
            return this;
        }
        public Selector Hire(DoMyWork worker)
        {
            _myWorkers.Add((worker, new Condition()));
            return this;
        }
        public Selector SetBlock(Condition condition)
        {
            if (_myWorkers.Count!=0)
            {
                var last = (_myWorkers.Last().worker, condition);
                _myWorkers.RemoveAt(_myWorkers.Count - 1);
                _myWorkers.Add(last);
            }
            return this;
        }
        private List<(DoMyWork worker, Condition condition)> _myWorkers = new();

        public override event Action CheckMyCondition;

        private bool _resetSeq = false;
        public DoMyWork CanResetSeq()
        {
            _resetSeq = true;
            return this;
        }
        public void ResetWorkerSeq(DoMyWork myworker)
        {
            if (myworker.GetType() == typeof(Sequence) & _resetSeq)
            {
                var worker = myworker as Sequence;
                worker.ResetEnumerator();
            }
        }
        private void AddMembers((DoMyWork, Condition)[] members)
        {
            foreach (var member in members)
            {
                _myWorkers.Add(member);
            }
        }
        private DoMyWork SelectByCondition()
        {
            CheckMyCondition?.Invoke();
            var collection = _myWorkers.Where(c => c.Item2.State).ToList();
            if (collection.Count==0)
            {
                return null;
            }
            else
            {                
                return collection.First().Item1;
            }
        }

        public override async Task<bool> DoWork()
        {
            var worker = SelectByCondition();
            if(worker is not null)
            {
                ResetWorkerSeq(worker);
                await worker.DoWork();
            }            
            return true;
        }
    }
    class Condition
    {
        public Condition(bool state = false)
        {
            this.State = state;
        }
        public bool State { get; private set; } = false;
        public void SetState(bool state)
        {
            this.State = state;
        }
    }

    class Ticker : DoMyWork
    {
        private Condition _myCondition;
        private DoMyWork _myWorker;
        private DoMyWork _mySlave;
        private bool _imWorking;
        public override event Action CheckMyCondition;
        public Ticker(DoMyWork worker, Condition condition)
        {
            _myWorker = worker;
            _mySlave = null;
            _myCondition = condition;
            _imWorking = false;
        }
        public Ticker HireSlave(DoMyWork slave)
        {
            _mySlave = slave;
            return this;
        }
        public override async Task<bool> DoWork()
        {
            if (_imWorking)
            {
                _mySlave?.DoWork();
            }
            else
            {
                base.DoWork();
                while (_myCondition.State)
                {
                    await _myWorker.DoWork();
                }
                return true;
            }
            return false;            
        }
    }
    public abstract class DoMyWork
    {
        protected string MyName;
        protected bool ImWorking = false;
        public void SetMyName(string name)
        {
            MyName = name;
        }
        public event Action<string> KnowMyName;
        public abstract event Action CheckMyCondition;
        public virtual async Task<bool> DoWork() 
        {
            KnowMyName?.Invoke(MyName);    
            return true;
        }
    }
}