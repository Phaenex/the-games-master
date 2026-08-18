using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

// Compiled together with the production GmHouseMemory, GmParlorAdaptation, and GmParlorCore
// sources. The small types at the bottom replace scene/save authorities that this persistence-only
// executable never invokes.
public static class HouseProcessHelper
{
    public static int Main(string[] args)
    {
        if (args.Length == 0) return 64;
        try
        {
            switch (args[0])
            {
                case "hold": return HoldLease(args);
                case "mutate": return MutateUnderLease(args);
                case "bootstrap": return Bootstrap(args);
                case "allocate": return Allocate(args);
                case "prepare": return Prepare(args);
                case "apply": return Apply(args);
                case "complete": return Complete(args);
                case "inspect": return Inspect(args);
                case "root-allocate-pause":
                case "prepare-pause":
                case "profile-pause":
                case "ack-pause": return PauseTransaction(args);
                default: return 64;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 70;
        }
    }

    static int HoldLease(string[] args)
    {
        if (args.Length != 3) return 64;
        return WithProductionLease(args[1], () =>
        {
            File.WriteAllText(args[2], "ready");
            while (true) Thread.Sleep(1000);
        }, 74);
    }

    static int MutateUnderLease(string[] args)
    {
        if (args.Length != 3) return 64;
        return WithProductionLease(args[1], () => File.WriteAllText(args[2], "acquired"), 73);
    }

    static int WithProductionLease(string lease, Action action, int deniedCode)
    {
        try
        {
            using (new GmHousePhysicalFileSystem().AcquireExclusiveLease(lease)) action();
            return 0;
        }
        catch (IOException) { return deniedCode; }
    }

    static int Bootstrap(string[] args)
    {
        if (args.Length != 2) return 64;
        GmHouseMemoryStore store = Open(args[1]);
        Require(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 4815,
            out GmHouseRunGeneration run, out string error), error);
        Console.WriteLine(run.Identity.RunId);
        return 0;
    }

    static int Allocate(string[] args)
    {
        if (args.Length != 2) return 64;
        GmHouseMemoryStore store = OpenExisting(args[1]);
        Require(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 4816,
            out GmHouseRunGeneration run, out string error), error);
        Console.WriteLine(run.Identity.RunId);
        return 0;
    }

    static int Prepare(string[] args)
    {
        if (args.Length != 3) return 64;
        PrepareActive(OpenExisting(args[1]), args[2]);
        return 0;
    }

    static int Apply(string[] args)
    {
        if (args.Length != 3) return 64;
        GmHouseMemoryStore store = OpenExisting(args[1]);
        GmHouseRunGeneration run = Load(store, args[2]);
        Require(run.Stage == GmHouseRunStage.Prepared, "run is not prepared");
        Require(store.TryApplyReceipt(run.PreparedReceipt, store.CurrentProfile.Cas,
            out _, out string error), error);
        return 0;
    }

    static int Complete(string[] args)
    {
        if (args.Length != 3) return 64;
        GmHouseMemoryStore store = OpenExisting(args[1]);
        GmHouseRunGeneration run = Load(store, args[2]);
        if (run.Stage == GmHouseRunStage.Active) run = PrepareActive(store, args[2]);
        if (run.Stage == GmHouseRunStage.Prepared)
        {
            var protocol = new GmHouseTerminalProtocol(store);
            Require(protocol.TryRecover(args[2], out run, out string error), error);
        }
        Require(run.Stage == GmHouseRunStage.Acknowledged, "terminal run did not acknowledge");
        return 0;
    }

    static int Inspect(string[] args)
    {
        if (args.Length != 3) return 64;
        GmHouseMemoryStore store = OpenExisting(args[1]);
        GmHouseRunGeneration run = string.IsNullOrEmpty(args[2]) ? null : Load(store, args[2]);
        int receiptCount = store.CurrentProfile.Receipts.Count;
        int distinct = store.CurrentProfile.Receipts.Select(item => item.ReceiptId)
            .Distinct(StringComparer.Ordinal).Count();
        Console.WriteLine(string.Join("\t", new object[]
        {
            store.CurrentRoot.Generation,
            store.CurrentRoot.NextRunOrdinal,
            store.CurrentRoot.Allocations.Count,
            store.CurrentProfile.Generation,
            receiptCount,
            distinct,
            run == null ? 0 : run.Generation,
            run == null ? "None" : run.Stage.ToString(),
        }));
        return 0;
    }

    static int PauseTransaction(string[] args)
    {
        if (args.Length != 5) return 64;
        string command = args[0];
        string domain = args[1];
        string runId = args[2];
        if (!Enum.TryParse(args[3], out GmHouseDurabilityPoint point)) return 64;
        var pausing = new PausingFileSystem(point, args[4], command);
        GmHouseMemoryStore store = OpenExisting(domain, pausing);
        pausing.Arm();

        if (command == "root-allocate-pause")
        {
            Require(store.TryAllocateCampaignRun(GmParlorAdaptiveMode.Ordinary, 9001,
                out _, out string error), error);
        }
        else if (command == "prepare-pause")
        {
            PrepareActive(store, runId);
        }
        else if (command == "profile-pause")
        {
            GmHouseRunGeneration run = Load(store, runId);
            Require(store.TryApplyReceipt(run.PreparedReceipt, store.CurrentProfile.Cas,
                out _, out string error), error);
        }
        else
        {
            GmHouseRunGeneration run = Load(store, runId);
            Require(store.TryAcknowledgeRun(run, out _, out string error), error);
        }
        throw new InvalidOperationException("target durability edge was not reached");
    }

    static GmHouseMemoryStore Open(string domain, IGmHouseFileSystem fileSystem = null)
    {
        var store = new GmHouseMemoryStore(domain, fileSystem);
        Require(store.TryOpenOrCreate(out _, out string error), error);
        return store;
    }

    static GmHouseMemoryStore OpenExisting(string domain, IGmHouseFileSystem fileSystem = null)
    {
        var store = new GmHouseMemoryStore(domain, fileSystem);
        Require(store.TryOpenExisting(out _, out string error), error);
        return store;
    }

    static GmHouseRunGeneration Load(GmHouseMemoryStore store, string runId)
    {
        Require(store.TryLoadRun(runId, out GmHouseRunGeneration run, out string error), error);
        return run;
    }

    static GmHouseRunGeneration PrepareActive(GmHouseMemoryStore store, string runId)
    {
        GmHouseRunGeneration run = Load(store, runId);
        if (run.Stage == GmHouseRunStage.Prepared || run.Stage == GmHouseRunStage.Acknowledged)
            return run;
        Require(run.Stage == GmHouseRunStage.Active, "run is not active");
        var behavior = new GmParlorBehaviorAccumulator(run.FrozenPackage);
        behavior.RecordPlayerLead(new GmCard(GmSuit.Flames, 7), 1, 7);
        behavior.RecordRead(GmTellObservation.Suspicious, GmParlorOutcomeKind.CheatCaught);
        behavior.SealCompletedMatch(run.FrozenPackage, 1, 0);
        Require(store.TryCreateReceipt(run, GmEndingType.TrappedLoop, behavior,
            out GmHouseTerminalReceipt receipt, out string error), error);
        var checkpoint = new GmSaveData
        {
            currentSceneId = "labyrinth",
            lastCheckpoint = "ending",
            houseRunId = run.Identity.RunId,
        };
        Require(store.TryCommitPreparedRun(run, receipt, checkpoint,
            out GmHouseRunGeneration prepared, out error), error);
        return prepared;
    }

    static void Require(bool condition, string error)
    {
        if (!condition) throw new InvalidOperationException(error);
    }

    sealed class PausingFileSystem : IGmHouseFileSystem
    {
        readonly GmHousePhysicalFileSystem inner = new GmHousePhysicalFileSystem();
        readonly GmHouseDurabilityPoint point;
        readonly string marker;
        readonly string command;
        bool armed;

        public PausingFileSystem(GmHouseDurabilityPoint point, string marker, string command)
        {
            this.point = point;
            this.marker = marker;
            this.command = command;
        }

        public void Arm() { armed = true; }
        public IDisposable AcquireExclusiveLease(string path) => inner.AcquireExclusiveLease(path);
        public bool FileExists(string path) => inner.FileExists(path);
        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public void CreateDirectory(string path) => inner.CreateDirectory(path);
        public string[] GetFiles(string path, string pattern) => inner.GetFiles(path, pattern);
        public byte[] ReadAllBytes(string path) => inner.ReadAllBytes(path);
        public void FlushDirectory(string path) => inner.FlushDirectory(path);

        public void WriteNewDurable(string path, byte[] bytes, GmHouseDurabilityPoint artifact,
            Action<GmHouseDurabilityEdge> observeEdge = null)
        {
            inner.WriteNewDurable(path, bytes, artifact, edge =>
            {
                observeEdge?.Invoke(edge);
                if (!armed || artifact != point || edge != GmHouseDurabilityEdge.ReopenValidation)
                    return;
                File.WriteAllText(marker, command + ":" + artifact + ":" + edge);
                while (true) Thread.Sleep(1000);
            });
        }
    }
}

public enum GmEndingType
{
    TrappedLoop, HostSuccession, DefiantSacrifice, CorruptedHost, Madness, TrueEscape,
}

public enum GmParlorOutcomeKind
{
    None, HonestAccepted, CheatMissed, CheatCaught, FalseReadPenalty,
}

public enum GmTellObservation { Calm, Suspicious }

[Serializable]
public sealed class GmSaveData
{
    public int corruptionTier = 1;
    public float sanity = 1f;
    public int defiance;
    public int compliance;
    public List<string> cheatsCaught = new List<string>();
    public List<string> discoveredClues = new List<string>();
    public List<string> completedRooms = new List<string>();
    public List<string> completedTableGames = new List<string>();
    public List<bool> mirrorShards = new List<bool> { false, false, false };
    public string currentSceneId = "wend-hill-prologue";
    public string lastCheckpoint = "spawn";
    public ulong parlorOutcomeNamespace;
    public ulong parlorAppliedOutcomeSequence;
    public int houseRunPointerVersion;
    public string houseRunId = string.Empty;
    public int accessibilitySettingsVersion;
    public bool accessibilityCaptions;
    public bool accessibilityReducedMotion;
    public bool accessibilityVibration = true;
    public bool accessibilityMonoAudio;
    public bool accessibilityHighContrast;
    public float accessibilityTextScale = 1f;
    public string timestampUtc = string.Empty;
}

public static class GmRunStore
{
    static GmSaveData data = new GmSaveData();
    public static void SetHouseRunPointer(string value) { data.houseRunId = value; }
    public static void LoadFromSaveData(GmSaveData value) { data = value; }
    public static GmSaveData ToSaveData() { return data; }
    public static void BeginNewRun() { data = new GmSaveData(); }
}

public static class GmSaveSystem
{
    public static string LastError { get { return string.Empty; } }
    public static bool Save() { return true; }
    public static bool HasSave() { return false; }
    public static bool Load() { return true; }
    public static bool DeleteSave() { return true; }
}
