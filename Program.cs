using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AsyncBreakfast
{
    public class ExampleProgram
    {
        protected bool verbose_;
        public bool Verbose => verbose_;

        public async static Task<int> Main(string[] args)
        {
            return await new ExampleProgram().RunAsync(args);
        }

        public async Task<int> RunAsync(string[] args)
        {
            try
            {
                verbose_ = args.Contains("--verbose") || args.Contains("-v");

                const string EggsKey = "eggs";
                const string SlicesOfBaconKey = "slicesOfBacon";
                const string SlicesOfBreadKey = "slicesOfBread";

                var random = new Random();

                var garbage = new Stack<object>();

                var cooking = new Dictionary<string,List<ICookable>>
                {
                    { EggsKey, new List<ICookable>() },
                    { SlicesOfBaconKey, new List<ICookable>() },
                    { SlicesOfBreadKey, new List<ICookable>() },
                };

                var plate = new Dictionary<string,List<ICookable>>
                {
                    { EggsKey, new List<ICookable>() },
                    { SlicesOfBaconKey, new List<ICookable>() },
                    { SlicesOfBreadKey, new List<ICookable>() },
                };

                FryingPan fryingPan = null;
                Toaster toaster = null;

                Action purgeGarbage = () =>
                {
                    foreach (var item in garbage)
                    {
                        if (item is ICookable cookable)
                        {
                            fryingPan.PurgeGarbage(cookable);
                            toaster.PurgeGarbage(cookable);

                            foreach (var batch in cooking.Values.Union(plate.Values))
                            {
                                if (batch.Contains(cookable))
                                {
                                    batch.Remove(cookable);
                                }
                            }
                        }
                    }
                };

                Action<string, BaseCooker, ICookable> finishCooking = (section, cooker, cookable) =>
                {
                    cooking[section].Remove(cookable);
                    cooker.Remove(cookable);
                };

                Action<string, BaseCooker, ICookable> moveToPlate = (section, cooker, cookable) =>
                {
                    finishCooking(section, cooker, cookable);
                    plate[section].Add(cookable);
                };

                Action<string, BaseCooker, ICookable> trash = (section, cooker, cookable)  =>
                {
                    finishCooking(section, cooker, cookable);
                    garbage.Push(cookable);

                    Console.Error.WriteLine($"Trashing item {cookable.Status} {cookable.TypeName} {cookable.Id}...");

                    purgeGarbage();
                };

                Func<Bacon, Bacon> applyBaconEvents = b =>
                {
                    b.Burned += (sender, args) => trash(SlicesOfBaconKey, fryingPan, b);
                    b.Cooked += (sender, args) => moveToPlate(SlicesOfBaconKey, fryingPan, b);

                    return b;
                };

                Func<Bread, Bread> applyBreadEvents = b =>
                {
                    b.Burned += (sender, args) => moveToPlate(SlicesOfBreadKey, toaster, b);

                    return b;
                };

                Func<Egg, Egg> applyEggEvents = e =>
                {
                    e.Burned += (sender, args) => trash(EggsKey, fryingPan, e);
                    e.Cooked += (sender, args) => moveToPlate(EggsKey, fryingPan, e);

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

                Func<Bacon> createBacon = () => applyBaconEvents(new Bacon(CookableStatus.Raw, 0.0F, 1.0F, verbose_));
                Func<Egg> createEgg = () => applyEggEvents(new Egg(CookableStatus.Raw, 0.0F, 1.0F, verbose_));
                Func<Bread> createBread = () => applyBreadEvents(new Bread(CookableStatus.Cooked, 1.0F, 1.0F, verbose_));

                const float BaseEnergyPerFrame = 0.25F;

                Func<float> randomize = () => (float)random.NextDouble() * BaseEnergyPerFrame;
                Func<FryingPan> createFryingPan = () => applyFryingPanEvents(new FryingPan(3, randomize, verbose_));
                Func<Toaster> createToaster = () => applyToasterEvents(new Toaster(2, randomize, verbose_));

                fryingPan = createFryingPan();
                toaster = createToaster();

                var cookers = new BaseCooker[] { fryingPan, toaster, };

                const int NumToast = 2;
                for (int i=0, l=NumToast; i<l; i++)
                {
                    cooking[SlicesOfBreadKey].Add(createBread());
                }

                const int NumEggs = 3;
                for (int i=0, l=NumEggs; i<l; i++)
                {
                    cooking[EggsKey].Add(createEgg());
                }

                const int NumBacon = 3;
                for (int i=0, l=NumBacon; i<l; i++)
                {
                    cooking[SlicesOfBaconKey].Add(createBacon());
                }

                var batches = new List<List<ICookable>>
                {
                    cooking[SlicesOfBreadKey],
                    cooking[SlicesOfBaconKey],
                    cooking[EggsKey],
                };

                while (batches.Any())
                {
                    if (fryingPan.Count < fryingPan.Capacity)
                    {
                        foreach (var batch in batches)
                        {
                            BaseCooker cooker = batch == cooking[SlicesOfBreadKey] ? (BaseCooker)toaster : (BaseCooker)fryingPan;

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
                            await cooker.CookAsync(1, random);
                        }
                    }

                    purgeGarbage();

                    foreach (var batch in batches.Where(o => o.Count == 0).ToList())
                    {
                        batches.Remove(batch);
                    }
                }

                Console.Error.WriteLine($"Breakfast is ready! Breakfast consists of {plate[SlicesOfBreadKey].Count} slices of toast, {plate[EggsKey].Count} eggs, and {plate[SlicesOfBaconKey].Count} slices of bacon.");
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
        Task<CookableStatus> CookAsync(int frames, float energyPerFrame, Random random);

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
        public BaseCookable(CookableStatus status, float doneness, float targetDoneness, bool verbose)
        {
            doneness_ = doneness;
            status_ = status;
            targetDoneness_ = targetDoneness;
            verbose_ = verbose;

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

        protected bool verbose_;
        public bool Verbose => verbose_;

        public async Task<CookableStatus> CookAsync(int frames, float energyPerFrame, Random random)
        {
            var before = doneness_;

            doneness_ += energyPerFrame * frames;

            var after = doneness_;
            var difference = after - before;

            var diffPercent = difference / targetDoneness_ * 100;
            var totalPercent = after / targetDoneness_ * 100;

            Func<Task> randomDelay = async () =>
            {
                var duration = random.Next(0, 250);

                if (verbose_)
                {
                    Console.Error.WriteLine($"Simulating load by awaiting a delay of {duration} milliseconds.");
                }

                await Task.Delay(duration);
            };

            await randomDelay();

            if (verbose_)
            {
                Console.Error.WriteLine($"The {Status} {TypeName} {Id} sizzles... Progressed {diffPercent}%, now {totalPercent}%...");
            }

            await randomDelay();

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
            if (verbose_)
            {
                Console.Error.WriteLine($"Status changed on {TypeName} {Id} from {args.OldStatus} to {args.NewStatus}.");
            }

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
        public Bacon(CookableStatus status, float doneness, float targetDoneness, bool verbose):
            base(status, doneness, targetDoneness, verbose)
        {
        }
    }

    public class Egg: BaseCookable
    {
        public Egg(CookableStatus status, float doneness, float targetDoneness, bool verbose):
            base(status, doneness, targetDoneness, verbose)
        {
        }
    }

    public class Bread: BaseCookable
    {
        public Bread(CookableStatus status, float doneness, float targetDoneness, bool verbose):
            base(status, doneness, targetDoneness, verbose)
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
        public BaseCooker(string name, int capacity, Func<float> energyPerFrame, bool verbose)
        {
            capacity_ = capacity;
            energyPerFrame_ = energyPerFrame;
            name_ = name;
            verbose_ = verbose;
        }

        protected int capacity_;
        public int Capacity => capacity_;

        protected List<ICookable> contents_ = new List<ICookable>();
        public IReadOnlyList<ICookable> Contents => contents_.AsReadOnly();

        public int Count => contents_.Count;

        public bool Empty => Count == 0;

        protected Func<float> energyPerFrame_;
        public Func<float> EnergyPerFrame => energyPerFrame_;

        protected Guid id_ = Guid.NewGuid();
        public Guid Id => id_;

        protected string name_;
        public string Name => name_;

        public int Space => capacity_ - Count;

        protected bool verbose_;
        public bool Verbose => verbose_;

        public void Add(IEnumerable<ICookable> cookables)
        {
            var count = cookables.Count();
            var contentsCount = Count;

            if (capacity_ < contentsCount + count)
            {
                throw new InvalidOperationException($"Cannot add {count} items to {name_} {id_} because it can only hold {capacity_} items and already contains {contentsCount}.");
            }
            else if (verbose_)
            {
                Console.Error.WriteLine($"{Name} {Id} should have capacity for {count} items. It supports {Capacity} items and it only contains {contentsCount} items.");
            }

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

        public async Task CookAsync(int frames, Random random)
        {
            var contents = contents_.ToList();
            var tasks = new List<Task>();

            foreach (var cookable in contents)
            {
                var energyPerFrame = energyPerFrame_();

                if (verbose_)
                {
                    Console.Error.WriteLine($"Cooking with {energyPerFrame} randomized energy per frame...");
                }

                var cookTask = cookable.CookAsync(frames, energyPerFrame, random);

                tasks.Add(cookTask);
            }

            if (verbose_)
            {
                Console.Error.WriteLine($"{Name} {Id} is cooking {Count} items...");
            }

            await Task.WhenAll(tasks);
        }

        public void PurgeGarbage(ICookable trash)
        {
            if (contents_.Contains(trash))
            {
                contents_.Remove(trash);
            }
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
        public FryingPan(int capacity, Func<float> energyPerFrame, bool verbose):
            base("Frying Pan", capacity, energyPerFrame, verbose)
        {
        }
    }

    public class Toaster: BaseCooker
    {
        public Toaster(int capacity, Func<float> energyPerFrame, bool verbose):
            base("Toaster", capacity, energyPerFrame, verbose)
        {
        }
    }
}
