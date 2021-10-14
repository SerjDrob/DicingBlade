using Microsoft.VisualStudio.Workspace;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DicingBlade.Classes.BehaviourTrees
{
    public class Leaf : WorkerBase
    {

        private readonly Action _myWork;
        private bool isPausedAfterWork = false;
        private CancellationTokenSource cancellationTokenSource = new();
        private int pauseCount = 0;
        private int resumeCount = 0;

        private object _lock = new object();
        public Leaf(Action myWork)
        {
            _myWork = myWork;
        }
        private PauseTokenSource _pauseTokenAfterWork = new();
        private bool _waitMeAfterWorkDone = false;
        public override async Task<bool> DoWork()
        {
            if (!_isCancelled)
            {
                base.DoWork();
                try
                {
                    if (_notBlocked)
                    {
                        var task = new Task(_myWork, cancellationTokenSource.Token);
                        task.Start();
                        await task;

                        if (_waitMeAfterWorkDone)
                            await _pauseTokenAfterWork.Token.WaitWhilePausedAsync().ContinueWith(t => { isPausedAfterWork = false; });
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return true;
        }
        public Leaf WaitForMe()
        {
            _waitMeAfterWorkDone = true;
            return this;
        }
        public override void PulseAction(bool info)
        {
            lock (_lock)
            {
                if (info & _waitMeAfterWorkDone)
                {
                    if (!isPausedAfterWork)
                    {
                        isPausedAfterWork = true;
                        pauseCount++;
                        _pauseTokenAfterWork.Pause();
                    }

                }
                else
                {
                    if (isPausedAfterWork)
                    {
                        resumeCount++;
                        _pauseTokenAfterWork.Resume();
                    }
                }
            }
        }
        public override void CancellAction(bool info)
        {
            if (info)
            {
                cancellationTokenSource.Cancel();
            }
        }
        public override Leaf SetActionBeforeWork(Action action)
        {
            return (Leaf)base.SetActionBeforeWork(action);
        }
        public override Leaf SetBlock(Block block)
        {
            return (Leaf)base.SetBlock(block);
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
