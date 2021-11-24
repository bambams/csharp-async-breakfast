using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AsyncBreakfast
{
    public class ExampleProgram
    {
        public async static Task<int> Main(string[] args)
        {
            return await new ExampleProgram().RunAsync(args);
        }

        public async Task<int> RunAsync(string[] args)
        {
            try
            {
                var garbage = new Stack<object>();

                var cooking = new Dictionary<string,List<ICookable>>
                {
                    { "eggs", new List<ICookable>() },
                    { "slicesOfBacon", new List<ICookable>() },
                    { "slicesOfBread", new List<ICookable>() },
                };

                var plate = new Dictionary<string,List<ICookable>>
                {
                    { "eggs", new List<ICookable>() },
                    { "slicesOfBacon", new List<ICookable>() },
                    { "slicesOfToast", new List<ICookable>() },
                };

                FryingPan fryingPan = null;
                Toaster toaster = null;

                Func<Bacon, Bacon> applyBaconEvents = bacon =>
                {
                    bacon.Burned += (sender, args) => garbage.Push(bacon);
                    bacon.Cooked += (sender, args) => plate["slicesOfBacon"].Add(bacon);

                    bacon.Done += (sender, args) =>
                    {
                        cooking["slicesOfBacon"].Remove(bacon);
                        fryingPan.Remove(bacon);
                    };

                    return bacon;
                };

                Func<Bread, Bread> applyBreadEvents = b =>
                {
                    b.Burned += (sender, args) => plate["slicesOfToast"].Add(b);

                    b.Done += (sender, args) =>
                    {
                        cooking["slicesOfBread"].Remove(b);
                        toaster.Remove(b);
                    };

                    return b;
                };

                Func<Egg, Egg> applyEggEvents = e =>
                {
                    e.Burned += (sender, args) => garbage.Push(e);
                    e.Cooked += (sender, args) => plate["eggs"].Add(e);

                    e.Done += (sender, args) =>
                    {
                        cooking["eggs"].Remove(e);
                        fryingPan.Remove(e);
                    };

                    return e;
                };

                Func<FryingPan, FryingPan> applyFryingPanEvents = fp =>
                {
                    fp.Added += (sender, args) => Console.Error.WriteLine($"Added {args.Cookable.Status} {args.Cookable.GetType().Name} {args.Cookable.Id} to the {fp.Name} {fp.Id}...");
                    fp.Removed += (sender, args) => Console.Error.WriteLine($"Removed {args.Cookable.Status} {args.Cookable.GetType().Name} {args.Cookable.Id} from the {fp.Name} {fp.Id}...");

                    return fp;
                };

                Func<Toaster, Toaster> applyToasterEvents = t =>
                {
                    t.Added += (sender, args) => Console.Error.WriteLine($"Added {args.Cookable.Status} {args.Cookable.GetType().Name} {args.Cookable.Id} to the {t.Name} {t.Id}...");
                    t.Removed += (sender, args) => Console.Error.WriteLine($"Removed {args.Cookable.Status} {args.Cookable.GetType().Name} {args.Cookable.Id} from the {t.Name} {t.Id}...");

                    return t;
                };

                Func<Bacon> createBacon = () => applyBaconEvents(new Bacon(CookableStatus.Raw, 0.0F, 1.0F));
                Func<Egg> createEgg = () => applyEggEvents(new Egg(CookableStatus.Raw, 0.0F, 1.0F));
                Func<Bread> createBread = () => applyBreadEvents(new Bread(CookableStatus.Cooked, 1.0F, 1.0F));

                Func<FryingPan> createFryingPan = () => applyFryingPanEvents(new FryingPan(3));
                Func<Toaster> createToaster = () => applyToasterEvents(new Toaster(2));

                fryingPan = createFryingPan();
                toaster = createToaster();

                var cookers = new BaseCooker[] { fryingPan, toaster, };

                var batches = new List<List<ICookable>>
                {
                    cooking["slicesOfBread"],
                    cooking["slicesOfBacon"],
                    cooking["eggs"],
                };

                for (int i=0, l=2; i<l; i++)
                {
                    cooking["slicesOfBread"].Add(createBread());
                }

                for (int i=0, l=3; i<l; i++)
                {
                    cooking["eggs"].Add(createEgg());
                }

                for (int i=0, l=3; i<l; i++)
                {
                    cooking["slicesOfBacon"].Add(createBacon());
                }

                while (batches.Any())
                {
                    if (fryingPan.Count < fryingPan.Capacity)
                    {
                        foreach (var batch in batches)
                        {
                            BaseCooker cooker = batch == cooking["slicesOfBread"] ? toaster : fryingPan;

                            if (batch.Count <= cooker.Space)
                            {
                                cooker.Add(batch);
                            }
                        }
                    }

                    foreach (var cooker in cookers)
                    {
                        if (!cooker.Empty)
                        {
                            await cooker.CookAsync(1, 0.0823F);
                        }
                    }

                    foreach (var batch in batches.Where(o => o.Count == 0).ToList())
                    {
                        batches.Remove(batch);
                    }
                }

                Console.Error.WriteLine($"Breakfast is ready! Breakfast consists of {plate["slicesOfToast"].Count} slices of toast, {plate["eggs"].Count} eggs, and {plate["slicesOfBacon"].Count} slices of bacon.");
                Console.Error.WriteLine($"We wasted {garbage.Count} items, counting {garbage.Count(o => o is ICookable)} food items:");

                foreach (var wasted in garbage)
                {
                    if (wasted is ICookable cookable)
                    {
                        Console.Error.WriteLine($"   - {cookable.TypeName} {cookable.Id} {cookable.Status}");
                    }
                    else
                    {
                        Console.Error.WriteLine($"   - {wasted} (unrelated object, {wasted.GetType().FullName})");
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to cook breakfast: {ex}");
            }

            return 1;
        }
    }

    [Flags]
    public enum CookableStatus
    {
        Frozen = 0x00,
        Raw = 0x01,
        Cooking = 0x02,
        PartiallyCooked = 0x04,
        Cooked = 0x08,
        Burned = 0x18,
    }

    public interface ICookable
    {
        Task<CookableStatus> CookAsync(int frames, float energyPerFrame);

        Guid Id { get; }

        CookableStatus Status { get; }

        float TargetDoneness { get; }

        string TypeName { get; }

        event EventHandler Burned;

        event EventHandler Cooked;

        event EventHandler Done;

        event EventHandler Frozen;

        event EventHandler StartedCooking;
    }

    public class BaseCookable: ICookable
    {
        public BaseCookable(CookableStatus status, float doneness, float targetDoneness)
        {
            doneness_ = doneness;
            status_ = status;
            targetDoneness_ = targetDoneness;

            Burned += _ChainedDone;
            Cooked += _ChainedDone;
            StatusUpdated += _OnStatusUpdated;
        }

        protected float doneness_;
        public float Doneness => doneness_;

        protected CookableStatus status_;
        public CookableStatus Status => status_;

        protected Guid id_ = Guid.NewGuid();
        public Guid Id => id_;

        protected float targetDoneness_;
        public float TargetDoneness => targetDoneness_;

        public string TypeName => GetType().Name;

        public async Task<CookableStatus> CookAsync(int frames, float energyPerFrame)
        {
            var before = doneness_;

            doneness_ += energyPerFrame * frames;

            var after = doneness_;
            var difference = after - before;

            var diffPercent = difference / targetDoneness_ * 100;
            var totalPercent = after / targetDoneness_ * 100;

            await Task.Delay(25);

            Console.Error.WriteLine($"Cooking {TypeName} {Id}... Progressed {diffPercent}%, now {totalPercent}%...");

            await Task.Delay(25);

            UpdateStatus();

            return await Task.FromResult(status_);
        }

        protected CookableStatus UpdateStatus()
        {
            var oldStatus = status_;

            if (doneness_ < 0)
            {
                status_ = CookableStatus.Frozen;
            }
            else if ((status_ & CookableStatus.Cooking) != CookableStatus.Cooking &&
                    0 < doneness_ &&
                    doneness_ < targetDoneness_)
            {
                status_ = CookableStatus.Cooking;
            }
            else if (targetDoneness_ <= doneness_)
            {
                if (doneness_ < 1.15 * targetDoneness_)
                {
                    status_ = CookableStatus.Cooked;
                }
                else
                {
                    status_ = CookableStatus.Burned;
                }
            }

            if (doneness_ > 0.25 * targetDoneness_ && doneness_ < targetDoneness_)
            {
                status_ = status_ | CookableStatus.PartiallyCooked;
            }

            var newStatus = status_;

            if (oldStatus != newStatus)
            {
                OnStatusUpdated(oldStatus, newStatus);
            }

            return newStatus;
        }

        #region Events

        public event EventHandler Burned;

        public event EventHandler Cooked;

        public event EventHandler Done;

        public event EventHandler Frozen;

        public event EventHandler StartedCooking;

        protected event EventHandler<StatusUpdatedEventArgs> StatusUpdated;

        protected void OnBurned()
        {
            if (Burned != null)
            {
                Burned(this, EventArgs.Empty);
            }
        }

        protected void OnCooked()
        {
            if (Cooked != null)
            {
                Cooked(this, EventArgs.Empty);
            }
        }

        protected void OnDone()
        {
            if (Done != null)
            {
                Done(this, EventArgs.Empty);
            }
        }

        protected void OnFrozen()
        {
            if (Frozen != null)
            {
                Frozen(this, EventArgs.Empty);
            }
        }

        protected void OnStartedCooking()
        {
            if (StartedCooking != null)
            {
                StartedCooking(this, EventArgs.Empty);
            }
        }

        protected void OnStatusUpdated(CookableStatus oldStatus, CookableStatus newStatus)
        {
            if (StatusUpdated != null)
            {
                StatusUpdated(this, new StatusUpdatedEventArgs(oldStatus, newStatus));
            }
        }

        /// <summary>
        /// Executes OnDone() after some other event.
        /// </summary>
        protected void _ChainedDone(object sender, EventArgs args)
        {
            OnDone();
        }

        protected void _OnStatusUpdated(object sender, StatusUpdatedEventArgs args)
        {
            Console.Error.WriteLine($"Status changed on {TypeName} {Id} from {args.OldStatus} to {args.NewStatus}.");

            switch (args.NewStatus)
            {
                case CookableStatus.Burned:
                    OnBurned();
                    break;
                case CookableStatus.Cooked:
                    OnCooked();
                    break;
                case CookableStatus.Frozen:
                    OnFrozen();
                    break;
            }
        }

        #endregion
    }

    public class StatusUpdatedEventArgs: EventArgs
    {
        public StatusUpdatedEventArgs(CookableStatus oldStatus, CookableStatus newStatus)
        {
            NewStatus = newStatus;
            OldStatus = oldStatus;
        }

        public CookableStatus NewStatus { get; protected set; }
        public CookableStatus OldStatus { get; protected set; }
    }

    public class Bacon: BaseCookable
    {
        public Bacon(CookableStatus status, float doneness, float targetDoneness):
            base(status, doneness, targetDoneness)
        {
        }
    }

    public class Egg: BaseCookable
    {
        public Egg(CookableStatus status, float doneness, float targetDoneness):
            base(status, doneness, targetDoneness)
        {
        }
    }

    public class Bread: BaseCookable
    {
        public Bread(CookableStatus status, float doneness, float targetDoneness):
            base(status, doneness, targetDoneness)
        {
        }
    }

    public class CookableEventArgs: EventArgs
    {
        public CookableEventArgs(ICookable cookable)
        {
            Cookable = cookable;
        }

        public ICookable Cookable { get; protected set; }
    }

    public class BaseCooker
    {
        public BaseCooker(string name, int capacity)
        {
            capacity_ = capacity;
            name_ = name;
        }

        protected int capacity_;
        public int Capacity => capacity_;

        protected List<ICookable> contents_ = new List<ICookable>();
        public List<ICookable> Contents => contents_;

        public int Count => contents_.Count;

        public bool Empty => Count == 0;

        protected Guid id_ = Guid.NewGuid();
        public Guid Id => id_;

        protected string name_;
        public string Name => name_;

        public int Space => capacity_ - Count;

        public void Add(IEnumerable<ICookable> cookables)
        {
            var count = cookables.Count();
            var contentsCount = Count;

            if (capacity_ < contentsCount + count)
            {
                throw new InvalidOperationException($"Cannot add {count} items to {name_} {id_} because it can only hold {capacity_} items and already contains {contentsCount}.");
            }
            //else
            //{
            //    Console.Error.WriteLine($"{Name} {Id} should have capacity for {count} items because it only contains {contentsCount} items and supports {Capacity}.");
            //}

            foreach (var cookable in cookables)
            {
                contents_.Add(cookable);

                OnAdded(cookable);
            }
        }

        public void Add(params ICookable[] cookables)
        {
            Add((IEnumerable<ICookable>)cookables);
        }

        public void Remove(IEnumerable<ICookable> cookables)
        {
            foreach (var cookable in cookables)
            {
                if (contents_.Contains(cookable))
                {
                    contents_.Remove(cookable);

                    OnRemoved(cookable);
                }
            }
        }

        public void Remove(params ICookable[] cookables)
        {
            Remove((IEnumerable<ICookable>)cookables);
        }

        public async Task CookAsync(int frames, float energyPerFrame)
        {
            List<Task> tasks = new List<Task>();

            foreach (var cookable in contents_)
            {
                var cookTask = cookable.CookAsync(frames, energyPerFrame);
                var continueTask = cookTask.ContinueWith(_ => Console.Error.WriteLine($"The {cookable.TypeName} {cookable.Id} sizzles..."));

                tasks.Add(continueTask);
            }

            Console.Error.WriteLine($"{Name} {Id} is cooking {Count} items...");

            await Task.WhenAll(tasks);
        }

        #region Events

        public event EventHandler<CookableEventArgs> Added;

        public event EventHandler<CookableEventArgs> Removed;

        protected void OnAdded(ICookable cookable)
        {
            if (Added != null)
            {
                Added(this, new CookableEventArgs(cookable));
            }
        }

        protected void OnRemoved(ICookable cookable)
        {
            if (Removed != null)
            {
                Removed(this, new CookableEventArgs(cookable));
            }
        }

        #endregion
    }

    public class FryingPan: BaseCooker
    {
        public FryingPan(int capacity):
            base("Frying Pan", capacity)
        {
        }
    }

    public class Toaster: BaseCooker
    {
        public Toaster(int capacity):
            base("Toaster", capacity)
        {
        }
    }
}
