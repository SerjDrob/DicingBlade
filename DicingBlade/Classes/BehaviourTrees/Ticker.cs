using System;
using System.Threading.Tasks;

namespace DicingBlade.Classes.BehaviourTrees
{
    public class Ticker : WorkerBase, ISequence<Ticker>
    {
        public event Action<bool> Pulse;
        public event Action<bool> Cancell;

        private WorkerBase _worker;

        public override async Task<bool> DoWork()
        {
            if (!_isCancelled)
            {
                base.DoWork();
                while (_notBlocked && !_isCancelled)
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
            }
            return true;
        }

        public Ticker Hire(WorkerBase worker)
        {
            worker.GiveMeName(false, $"{_name}.1");
            Pulse += worker.PulseAction;
            Cancell += worker.CancellAction;
            _worker = worker;
            return this;
        }
        public override void GiveMeName(bool ascribe, string name)
        {
            base.GiveMeName(ascribe, name);
            _worker?.GiveMeName(false, $"{_name}.1");
        }
        public override void PulseAction(bool info)
        {
            Pulse?.Invoke(info);
        }
        public override Ticker SetActionBeforeWork(Action action)
        {
            return (Ticker)base.SetActionBeforeWork(action);
        }

        public override void CancellAction(bool info)
        {
            _isCancelled = true;
            Cancell?.Invoke(info);
        }
    }
    //public class SequenceBuilder<TRoot, TParent>:TreeBuilder<TRoot> where TRoot : class, ISequence<TRoot>, new()
    //                                                                   where TParent : class, TreeBuilder<TParent>, new()


    //{
    //    private TreeBuilder<TRoot> tree;
    //    private Sequence sequence;
    //    private TParent parent;
    //    public SequenceBuilder(TreeBuilder<TRoot> tree, TParent parent)
    //    {
    //        this.tree = tree;
    //        this.sequence = sequence;
    //        this.parent = parent;            
    //    }
    //    public override SequenceBuilder<TRoot, TreeBuilder<TRoot>, TRoot> AddSequence
    //    {
    //        get
    //        {
    //            var seq = new Sequence();
    //            sequence.Hire(seq);
    //            return new SequenceBuilder<TRoot, TreeBuilder<TRoot>, TRoot>(root, this, seq);
    //        }
    //    }
    //    public override TickerBuilder<TRoot, TreeBuilder<TRoot>, TRoot> AddTicker
    //    {
    //        get
    //        {
    //            var tick = new Ticker();
    //            root.Hire(tick);
    //            return new TickerBuilder<TRoot, TreeBuilder<TRoot>, TRoot>(root, this, tick);
    //        }
    //    }
    //    public TParent Build() => parent;        
    //}
    //public class TickerBuilder<TRoot, TParent, T> : TreeBuilder<TRoot> where TRoot : class, ISequence<TRoot>, new()
    //                                                                   where T : class, ISequence<T>, new()
    //                                                                   where TParent : TreeBuilder<T>
    //{
    //    private TRoot root;
    //    private Ticker ticker;
    //    private TParent parent;
    //    public TickerBuilder(TRoot root, TParent parent, Ticker ticker)
    //    {
    //        this.root = root;
    //        this.ticker = ticker;
    //        this.parent = parent;
    //    }
    //    public override SequenceBuilder<TRoot, TreeBuilder<TRoot>, TRoot> AddSequence
    //    {
    //        get
    //        {
    //            var seq = new Sequence();
    //            ticker.Hire(seq);
    //            return new SequenceBuilder<TRoot, TreeBuilder<TRoot>, TRoot>(root, this, seq);
    //        }
    //    }
    //    public TParent Build() => parent;

    //}
    //public class LeafBuilder
    //{

    //}
}
