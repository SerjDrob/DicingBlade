using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DicingBlade.Classes.BehaviourTrees
{
    public class Sequence : WorkerBase,ISequence<Sequence>
    {
        private List<WorkerBase> _workers = new();
        private int childrenCount = 0;

        public event Action<bool> Pulse;
        public event Action<bool> Cancell;

        public override async Task<bool> DoWork()
        {
            if (!_isCancelled)
            {
                base.DoWork();
                if (_notBlocked)
                {
                    foreach (var worker in _workers)
                    {
                        if (_isCancelled) return true;
                        if (worker is Leaf) worker.PulseAction(true);
                        var res = await worker.DoWork();
                    }
                }
            }
            return true;
        }
        public Sequence Hire(WorkerBase worker)
        {
            childrenCount++;
            worker.GiveMeName(false, $"{_name}.{childrenCount}");
            Pulse += worker.PulseAction;
            Cancell += worker.CancellAction;
            _workers.Add(worker);
            return this;
        }
        public override void GiveMeName(bool ascribe, string name)
        {
            base.GiveMeName(ascribe, name);
            var i = 1;
            _workers.ForEach(worker =>
            {
                worker.GiveMeName(false, $"{_name}.{i++}");
            });
        }
        public override void PulseAction(bool info)
        {
            Pulse?.Invoke(info);
        }
        public override Sequence SetActionBeforeWork(Action action)
        {
            return (Sequence)base.SetActionBeforeWork(action);
        }

        public override void CancellAction(bool info)
        {
            _isCancelled = true;
            Cancell?.Invoke(info);
        }
    }



    //public class TreeBuilder<TRoot> where TRoot : class, ISequence<TRoot>, new()
                                           
    //{
        
    //    public static TRoot root;       
    //    public TreeBuilder()
    //    {
    //      root = new TRoot();
    //    }
    //    public virtual SequenceBuilder<TRoot, Sequence> AddSequence
    //    {
    //        get
    //        {
    //            var seq = new Sequence();
    //            root.Hire(seq);
    //            return new SequenceBuilder<TRoot,Sequence>(this, seq);
    //        }
    //    }
    //    public virtual TickerBuilder<TRoot, TreeBuilder<TRoot>, TRoot> AddTicker
    //    {
    //        get
    //        {
    //            var tick = new Ticker();
    //            root.Hire(tick);
    //            return new TickerBuilder<TRoot, TreeBuilder<TRoot>, TRoot>(root, this, tick);
    //        }
    //    }
    //    //public LeafBuilder AddLeaf { }
    //    public TRoot BuildTree() {  return root; }        
    //}
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
