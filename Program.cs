using System;
using System.Collections.Generic;
using System.IO;
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
                Communication.Instance.Verbose = args.Count(arg => arg == "--verbose" || arg == "-v");

                const string EggsKey = "eggs";
                const string SlicesOfBaconKey = "slicesOfBacon";
                const string SlicesOfBreadKey = "slicesOfBread";

                var random = new Random();

                var garbage = new Stack<object>();

                Func<Dictionary<string,List<ICookable>>> initTracker = () => new Dictionary<string,List<ICookable>>
                {
                    { EggsKey, new List<ICookable>() },
                    { SlicesOfBaconKey, new List<ICookable>() },
                    { SlicesOfBreadKey, new List<ICookable>() },
                };

                var mealplans = initTracker();
                var cooking = initTracker();
                var plate = initTracker();

                var actives = new List<Dictionary<string,List<ICookable>>>
                {
                    mealplans,
                };

                FryingPan fryingPan = null;
                Toaster toaster = null;

                Action<string,ICookable,Dictionary<string,List<ICookable>>> magicAdd = (key, cookable, tracker) =>
                {
                    if (!tracker.ContainsKey(key))
                    {
                        tracker[key] = new List<ICookable>();
                    }

                    tracker[key].Add(cookable);
                };

                Action<string, ICookable> plan = (foodGroup, cookable) => magicAdd(foodGroup, cookable, mealplans);

                Action purgeGarbage = () =>
                {
                    foreach (var item in garbage)
                    {
                        if (item is ICookable cookable)
                        {
                            fryingPan.PurgeGarbage(cookable);
                            toaster.PurgeGarbage(cookable);

                            var containers = actives.Inline();

                            foreach (var container in containers)
                            {
                                if (container.Contains(cookable))
                                {
                                    container.Remove(cookable);
                                }
                            }
                        }
                    }
                };

                Action<Dictionary<string,List<ICookable>>> register = tracker =>
                {
                    if (!actives.Contains(tracker))
                    {
                        actives.Add(tracker);
                    }
                };

                Action<Dictionary<string, List<ICookable>>> deactivateIfEmpty = tracker =>
                {
                    if (!tracker.Any())
                    {
                        if (actives.Contains(tracker))
                        {
                            actives.Remove(tracker);
                        }
                    }
                };

                Action<string, Dictionary<string, List<ICookable>>> strikeout = (foodGroup, tracker) =>
                {
                    tracker.Remove(foodGroup);
                    deactivateIfEmpty(tracker);
                };


                Action<string, Dictionary<string, List<ICookable>>> maybeStrikeout = (foodGroup, tracker) =>
                {
                    if (!tracker[foodGroup].Any())
                    {
                        strikeout(foodGroup, tracker);
                    }
                };

                Action<string, BaseCooker, ICookable> finishCooking = (foodGroup, cooker, cookable) =>
                {
                    cooker.Remove(cookable);
                    strikeout(foodGroup, cooking);
                };

                Action<string, BaseCooker, List<ICookable>> startCooking = (foodGroup, cooker, cookables) =>
                {
                    if (cookables.Any())
                    {
                        cooker.Add(cookables);

                        strikeout(foodGroup, mealplans);

                        foreach (var cookable in cookables)
                        {
                            magicAdd(foodGroup, cookable, cooking);
                        }

                        register(cooking);
                    }
                };

                Action<string, BaseCooker, ICookable> moveToPlate = (foodGroup, cooker, cookable) =>
                {
                    finishCooking(foodGroup, cooker, cookable);
                    magicAdd(foodGroup, cookable, plate);
                    //register(plate);

                    Communication.Instance.Say($"Moving {cookable.Status} {cookable.TypeName} {cookable.Id} to the plate...");
                };

                Action<string, BaseCooker, ICookable> trash = (foodGroup, cooker, cookable)  =>
                {
                    finishCooking(foodGroup, cooker, cookable);
                    garbage.Push(cookable);

                    Communication.Instance.Say($"Trashing item {cookable.Status} {cookable.TypeName} {cookable.Id}...");

                    purgeGarbage();
                };

                Func<Bacon, Bacon> applyBaconEvents = b =>
                {
                    Action<Action<string, BaseCooker, ICookable>> call = f => f(SlicesOfBaconKey, fryingPan, b);

                    b.Burned += (sender, args) => call(trash);
                    b.Cooked += (sender, args) => call(moveToPlate);

                    return b;
                };

                Func<Bread, Bread> applyBreadEvents = b =>
                {
                    Action<Action<string, BaseCooker, ICookable>> call = f => f(SlicesOfBreadKey, toaster, b);

                    b.Burned += (sender, args) => call(moveToPlate);

                    return b;
                };

                Func<Egg, Egg> applyEggEvents = e =>
                {
                    Action<Action<string, BaseCooker, ICookable>> call = f => f(EggsKey, fryingPan, e);

                    e.Burned += (sender, args) => call(trash);
                    e.Cooked += (sender, args) => call(moveToPlate);

                    return e;
                };

                Func<FryingPan, FryingPan> applyFryingPanEvents = fp =>
                {
                    fp.Added += (sender, args) => Communication.Instance.Say($"Added {args.Cookable.Status} {args.Cookable.GetType().Name} {args.Cookable.Id} to the {fp.Name} {fp.Id}...");
                    fp.Removed += (sender, args) => Communication.Instance.Say($"Removed {args.Cookable.Status} {args.Cookable.GetType().Name} {args.Cookable.Id} from the {fp.Name} {fp.Id}...");

                    return fp;
                };

                Func<Toaster, Toaster> applyToasterEvents = t =>
                {
                    t.Added += (sender, args) => Communication.Instance.Say($"Added {args.Cookable.Status} {args.Cookable.GetType().Name} {args.Cookable.Id} to the {t.Name} {t.Id}...");
                    t.Removed += (sender, args) => Communication.Instance.Say($"Removed {args.Cookable.Status} {args.Cookable.GetType().Name} {args.Cookable.Id} from the {t.Name} {t.Id}...");

                    return t;
                };

                Func<Bacon> createBacon = () => applyBaconEvents(new Bacon(CookableStatus.Raw, 0.0F, 1.0F));
                Func<Egg> createEgg = () => applyEggEvents(new Egg(CookableStatus.Raw, 0.0F, 1.0F));
                Func<Bread> createBread = () => applyBreadEvents(new Bread(CookableStatus.Cooked, 1.0F, 1.0F));

                const float BaseEnergyPerFrame = 0.25F;

                Func<float> randomize = () => (float)random.NextDouble() * BaseEnergyPerFrame;
                Func<FryingPan> createFryingPan = () => applyFryingPanEvents(new FryingPan(3, randomize));
                Func<Toaster> createToaster = () => applyToasterEvents(new Toaster(2, randomize));

                fryingPan = createFryingPan();
                toaster = createToaster();

                var cookers = new Dictionary<string,BaseCooker>
                {
                    { EggsKey, fryingPan},
                    { SlicesOfBreadKey, toaster},
                    { SlicesOfBaconKey, fryingPan},
                };

                const int NumToast = 2;
                for (int i=0, l=NumToast; i<l; i++)
                {
                    plan(SlicesOfBreadKey, createBread());
                }

                const int NumEggs = 3;
                for (int i=0, l=NumEggs; i<l; i++)
                {
                    plan(EggsKey, createEgg());
                }

                const int NumBacon = 3;
                for (int i=0, l=NumBacon; i<l; i++)
                {
                    plan(SlicesOfBaconKey, createBacon());
                }

                while (actives.Any())
                {
                    Communication.Instance.SayTRACE($"mealplans:{mealplans.Count} cooking:{cooking.Count} fryingPan:{fryingPan.Count} toaster:{toaster.Count} plate:{plate.Count}");

                    foreach (var mealplan in mealplans.ToList())
                    {
                        var foodGroup = mealplan.Key;
                        var cooker = cookers[foodGroup];
                        var items = mealplan.Value;

                        if (items.Count <= cooker.Space)
                        {
                            Communication.Instance.SayTRACE($"There is {cooker.Space} space in the {cooker.Name} {cooker.Id}, and we're adding {items.Count}.");

                            startCooking(foodGroup, cooker, items);
                        }
                        else
                        {
                            var firstItem = items.First();

                            Communication.Instance.SayVerbose($"We checked the {cooker.Name} {cooker.Id} which has {cooker.Count} out of {cooker.Capacity} used, but there is not enough space for {items.Count} {firstItem.TypeName} to be added.");
                        }
                    }

                    foreach (var cooker in cookers.Values.Distinct())
                    {
                        if (!cooker.Empty)
                        {
                            await cooker.CookAsync(1, random);
                        }
                        else
                        {
                            Communication.Instance.SayTRACE($"The {cooker.Name} {cooker.Id} is empty. Nothing to cook.");
                        }
                    }

                    purgeGarbage();

                    Action<Dictionary<string,List<ICookable>>> tidy = tracker =>
                    {
                        var empties = tracker
                            .Where(kv => kv.Value.Count == 0)
                            .Select(kv => kv.Key)
                            .ToList();

                        foreach (var foodGroup in empties)
                        {
                            maybeStrikeout(foodGroup, tracker);
                        }
                    };

                    foreach (var tracker in actives.Distinct())
                    {
                        tidy(tracker);
                    }
                }

                Communication.Instance.Say($"Breakfast is ready! Breakfast consists of {plate[SlicesOfBreadKey].Count} slices of toast, {plate[EggsKey].Count} eggs, and {plate[SlicesOfBaconKey].Count} slices of bacon.");
                Communication.Instance.Say($"We wasted {garbage.Count} items, counting {garbage.Count(o => o is ICookable)} food items:");

                foreach (var wasted in garbage)
                {
                    if (wasted is ICookable cookable)
                    {
                        Communication.Instance.Say($"   - {cookable.TypeName} {cookable.Id} {cookable.Status}");
                    }
                    else
                    {
                        Communication.Instance.Say($"   - {wasted} (unrelated object, {wasted.GetType().FullName})");
                    }
                }

                return garbage.Count;
            }
            catch (Exception ex)
            {
                Communication.Instance.Say($"Failed to cook breakfast: {ex}");
            }

            return -1;
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

        event EventHandler Cooking;

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

                Communication.Instance.SayVerbose($"Simulating load by awaiting a delay of {duration} milliseconds.");

                await Task.Delay(duration);
            };

            await randomDelay();

            Communication.Instance.SayVerbose($"The {Status} {TypeName} {Id} sizzles... Progressed {diffPercent}%, now {totalPercent}%...");

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

        public event EventHandler Cooking;

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

        protected void OnCooking()
        {
            if (Cooking != null)
            {
                Cooking(this, EventArgs.Empty);
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
            Communication.Instance.SayVerbose($"Status changed on {TypeName} {Id} from {args.OldStatus} to {args.NewStatus}.");

            switch (args.NewStatus)
            {
                case CookableStatus.Burned:
                    OnBurned();
                    break;
                case CookableStatus.Cooked:
                    OnCooked();
                    break;
                case CookableStatus.Cooking:
                    OnCooking();
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
        public BaseCooker(string name, int capacity, Func<float> energyPerFrame)
        {
            capacity_ = capacity;
            energyPerFrame_ = energyPerFrame;
            name_ = name;
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

        public void Add(IEnumerable<ICookable> cookables)
        {
            var count = cookables.Count();
            var contentsCount = Count;

            if (capacity_ < contentsCount + count)
            {
                throw new InvalidOperationException($"Cannot add {count} items to {name_} {id_} because it can only hold {capacity_} items and already contains {contentsCount}.");
            }
            else
            {
                Communication.Instance.SayVerbose($"{Name} {Id} should have capacity for {count} items. It supports {Capacity} items and it only contains {contentsCount} items.");
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

                Communication.Instance.SayVerbose($"Cooking with {energyPerFrame} randomized energy per frame...");

                var cookTask = cookable.CookAsync(frames, energyPerFrame, random);

                tasks.Add(cookTask);
            }

            Communication.Instance.SayVerbose($"{Name} {Id} is cooking {Count} items...");

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
        public FryingPan(int capacity, Func<float> energyPerFrame):
            base("Frying Pan", capacity, energyPerFrame)
        {
        }
    }

    public class Toaster: BaseCooker
    {
        public Toaster(int capacity, Func<float> energyPerFrame):
            base("Toaster", capacity, energyPerFrame)
        {
        }
    }

    public class Communication
    {
        public static readonly Communication Instance = new Communication();

        protected int verbose_;
        public int Verbose { get => verbose_; set => verbose_ = value; }

        public void Say(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentNullException("Program tried to Say() nothing. That's a programming error.", nameof(message));
            }

            textWriter_.WriteLine(message);
        }

        public void SayVerbose(string message)
        {
            if (verbose_ > 0)
            {
                Say(message);
            }
        }

        public void SayTRACE(string message)
        {
            if (verbose_ > 1)
            {
                Say($"TRACE: {message}");
            }
        }

        protected TextWriter textWriter_ = Console.Error;
    }

    public static class Extensions
    {
        public static List<List<ICookable>> Inline(this List<Dictionary<string,List<ICookable>>> lod)
        {
            var result = new List<List<ICookable>>();

            foreach (var d in lod)
            {
                foreach (var list in d.Values)
                {
                    if (list.Any())
                    {
                        result.Add(list);
                    }
                }
            }

            return result.Distinct().ToList();
        }
    }
}
