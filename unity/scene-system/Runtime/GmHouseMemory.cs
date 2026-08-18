using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

public enum GmHouseRunStage { Active, Prepared, Acknowledged, Abandoned }
public enum GmHouseTerminalFault
{
    None,
    AfterPreparedRunCommit,
    AfterProfileCommit,
    AfterAcknowledgedRunCommit,
}

public enum GmHouseDurabilityPoint
{
    None,
    RootGeneration,
    RootCommitRecord,
    ProfileGeneration,
    ProfileCommitRecord,
    ReceiptBlob,
    RunGeneration,
    RunCommitRecord,
    DirectoryFlush,
}

public enum GmHouseDurabilityEdge
{
    None,
    TempCreate,
    PayloadWrite,
    PayloadFlush,
    AtomicRename,
    DirectoryFlush,
    ReopenValidation,
    LeaseRelease,
}

public interface IGmHouseFileSystem
{
    IDisposable AcquireExclusiveLease(string path);
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    string[] GetFiles(string path, string pattern);
    byte[] ReadAllBytes(string path);
    void WriteNewDurable(string path, byte[] bytes, GmHouseDurabilityPoint point,
        Action<GmHouseDurabilityEdge> observeEdge = null);
    void FlushDirectory(string path);
}

public sealed class GmHousePhysicalFileSystem : IGmHouseFileSystem
{
    public IDisposable AcquireExclusiveLease(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var stream=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return stream;
        int descriptor=stream.SafeFileHandle.DangerousGetHandle().ToInt32();
        if(flock(descriptor,6)==0) return new UnixLease(stream,descriptor);
        stream.Dispose();
        throw new IOException("House memory lease is held by another process");
    }

    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public string[] GetFiles(string path, string pattern) => Directory.Exists(path)
        ? Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly)
        : Array.Empty<string>();
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public void WriteNewDurable(string path, byte[] bytes, GmHouseDurabilityPoint point,
        Action<GmHouseDurabilityEdge> observeEdge = null)
    {
        if (File.Exists(path)) throw new IOException("immutable House artifact already exists: " + path);
        string directory = Path.GetDirectoryName(path);
        Directory.CreateDirectory(directory);
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None))
            {
                observeEdge?.Invoke(GmHouseDurabilityEdge.TempCreate);
                stream.Write(bytes, 0, bytes.Length);
                observeEdge?.Invoke(GmHouseDurabilityEdge.PayloadWrite);
                stream.Flush(true);
                observeEdge?.Invoke(GmHouseDurabilityEdge.PayloadFlush);
            }
            observeEdge?.Invoke(GmHouseDurabilityEdge.AtomicRename);
            File.Move(temporary, path);
            observeEdge?.Invoke(GmHouseDurabilityEdge.DirectoryFlush);
            FlushDirectory(directory);
            byte[] reopened = File.ReadAllBytes(path);
            if (!reopened.SequenceEqual(bytes))
                throw new IOException("durable House artifact did not reopen exactly: " + path);
            observeEdge?.Invoke(GmHouseDurabilityEdge.ReopenValidation);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public void FlushDirectory(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        int descriptor = open(path, 0);
        if (descriptor < 0) throw new IOException("could not open House directory for flush: " + path);
        try
        {
            if (fsync(descriptor) != 0)
                throw new IOException("could not flush House directory: " + path);
        }
        finally { close(descriptor); }
    }

    [DllImport("libc", SetLastError = true)] static extern int open(string path, int flags);
    [DllImport("libc", SetLastError = true)] static extern int fsync(int fd);
    [DllImport("libc", SetLastError = true)] static extern int close(int fd);
    [DllImport("libc", SetLastError = true)] static extern int flock(int fd,int operation);

    sealed class UnixLease : IDisposable
    {
        FileStream stream;
        readonly int descriptor;
        public UnixLease(FileStream stream,int descriptor)
        { this.stream=stream;this.descriptor=descriptor; }
        public void Dispose()
        {
            FileStream owned=stream;if(owned==null)return;stream=null;
            flock(descriptor,8);
            owned.Dispose();
        }
    }
}

public sealed class GmHouseFaultInjectingFileSystem : IGmHouseFileSystem
{
    readonly IGmHouseFileSystem inner;
    public GmHouseDurabilityPoint FailNext;
    public GmHouseDurabilityPoint FailArtifact;
    public GmHouseDurabilityEdge FailNextEdge;
    public bool MutateNextReceiptBlobAfterWrite;
    bool failLeaseReleaseArmed;

    public GmHouseFaultInjectingFileSystem(IGmHouseFileSystem inner) =>
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public IDisposable AcquireExclusiveLease(string path) =>
        new FaultLease(inner.AcquireExclusiveLease(path), this);
    public bool FileExists(string path) => inner.FileExists(path);
    public bool DirectoryExists(string path) => inner.DirectoryExists(path);
    public void CreateDirectory(string path) => inner.CreateDirectory(path);
    public string[] GetFiles(string path, string pattern) => inner.GetFiles(path, pattern);
    public byte[] ReadAllBytes(string path) => inner.ReadAllBytes(path);
    public void FlushDirectory(string path)
    {
        ThrowIf(GmHouseDurabilityPoint.DirectoryFlush);
        inner.FlushDirectory(path);
    }

    public void WriteNewDurable(string path, byte[] bytes, GmHouseDurabilityPoint point,
        Action<GmHouseDurabilityEdge> observeEdge = null)
    {
        ThrowIf(point);
        bool targeted = FailArtifact == point && FailNextEdge != GmHouseDurabilityEdge.None;
        inner.WriteNewDurable(path, bytes, point, edge =>
        {
            observeEdge?.Invoke(edge);
            if(!targeted||FailNextEdge!=edge) return;
            FailNextEdge=GmHouseDurabilityEdge.None;
            throw new IOException("injected House durability failure at "+point+"/"+edge);
        });
        if (targeted && FailNextEdge == GmHouseDurabilityEdge.LeaseRelease)
        {
            FailNextEdge = GmHouseDurabilityEdge.None;
            failLeaseReleaseArmed = true;
        }
        if (point == GmHouseDurabilityPoint.ReceiptBlob && MutateNextReceiptBlobAfterWrite)
        {
            MutateNextReceiptBlobAfterWrite = false;
            byte[] corrupted = File.ReadAllBytes(path);
            corrupted[Math.Max(0, corrupted.Length / 2)] ^= 0x5a;
            File.WriteAllBytes(path, corrupted);
        }
    }

    sealed class FaultLease : IDisposable
    {
        readonly IDisposable innerLease;
        readonly GmHouseFaultInjectingFileSystem owner;
        public FaultLease(IDisposable innerLease, GmHouseFaultInjectingFileSystem owner)
        { this.innerLease = innerLease; this.owner = owner; }
        public void Dispose()
        {
            innerLease.Dispose();
            if (!owner.failLeaseReleaseArmed) return;
            owner.failLeaseReleaseArmed = false;
            throw new IOException("injected House durability failure at lease release");
        }
    }

    void ThrowIf(GmHouseDurabilityPoint point)
    {
        if (FailNext != point) return;
        FailNext = GmHouseDurabilityPoint.None;
        throw new IOException("injected House durability failure at " + point);
    }
}

public readonly struct GmHouseRunIdentity
{
    public string LineageId { get; }
    public long Epoch { get; }
    public string RunId { get; }
    public long RunOrdinal { get; }

    public GmHouseRunIdentity(string lineageId, long epoch, string runId, long runOrdinal)
    {
        LineageId = lineageId ?? string.Empty;
        Epoch = epoch;
        RunId = runId ?? string.Empty;
        RunOrdinal = runOrdinal;
    }
}

public readonly struct GmHouseProfileCas : IEquatable<GmHouseProfileCas>
{
    public string LineageId { get; }
    public long Epoch { get; }
    public long Generation { get; }
    public string GenerationHash { get; }

    public GmHouseProfileCas(string lineageId, long epoch, long generation,
        string generationHash)
    {
        LineageId = lineageId ?? string.Empty;
        Epoch = epoch;
        Generation = generation;
        GenerationHash = generationHash ?? string.Empty;
    }

    public bool Equals(GmHouseProfileCas other) => LineageId == other.LineageId &&
        Epoch == other.Epoch && Generation == other.Generation &&
        GenerationHash == other.GenerationHash;
    public override bool Equals(object obj) => obj is GmHouseProfileCas other && Equals(other);
    public override int GetHashCode() => (LineageId, Epoch, Generation, GenerationHash).GetHashCode();
}

public sealed class GmHouseRunAllocation
{
    public long Epoch { get; internal set; }
    public long RunOrdinal { get; internal set; }
    public string RunId { get; internal set; }
    public GmParlorAdaptiveMode Mode { get; internal set; }
}

public sealed class GmHouseRootGeneration
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; internal set; } = CurrentSchemaVersion;
    public string LineageId { get; internal set; }
    public long Epoch { get; internal set; }
    public long Generation { get; internal set; }
    public long NextRunOrdinal { get; internal set; }
    public bool ResetPending { get; internal set; }
    public string PreviousGenerationHash { get; internal set; }
    public string GenerationHash { get; internal set; }
    public IReadOnlyList<GmHouseRunAllocation> Allocations { get; internal set; } =
        Array.Empty<GmHouseRunAllocation>();
}

public sealed class GmHousePackageBinding
{
    public int Version { get; internal set; } = 1;
    public string LineageId { get; internal set; } = string.Empty;
    public long Epoch { get; internal set; }
    public long ProfileGeneration { get; internal set; }
    public byte[] ProfileDigest { get; internal set; } = new byte[32];
    public byte[] HistoryDigest { get; internal set; } = new byte[32];
    public byte[] PackageHash { get; internal set; } = new byte[32];
    public IReadOnlyList<string> SelectedReceiptIds { get; internal set; } = Array.Empty<string>();
    public bool ConsultedProfile { get; internal set; }

    public byte[] CanonicalBytes => GmHouseBinary.EncodeBinding(this);
    public byte[] CanonicalHash => GmHouseBinary.Sha256(CanonicalBytes);
}

public sealed class GmHouseTerminalReceipt
{
    public const int CurrentCodecVersion = 1;
    public int CodecVersion { get; internal set; } = CurrentCodecVersion;
    public string LineageId { get; internal set; }
    public long Epoch { get; internal set; }
    public string RunId { get; internal set; }
    public long RunOrdinal { get; internal set; }
    public long OutcomeSequence { get; internal set; } = 1;
    public GmParlorAdaptiveMode Mode { get; internal set; }
    public GmEndingType Ending { get; internal set; }
    public bool ContributesToLearning { get; internal set; }
    public string ReceiptId { get; internal set; }
    public byte[] PayloadHash { get; internal set; }
    public GmParlorAdaptivePackage FrozenPackage { get; internal set; }
    public GmHousePackageBinding PackageBinding { get; internal set; }
    public GmParlorCompletedMatchSummary Summary { get; internal set; }

    internal byte[] CanonicalPayloadBytes() => GmHouseReceiptCodec.CanonicalPayloadBytes(this);
}

public sealed class GmHouseRunGeneration
{
    public int SchemaVersion { get; internal set; } = 1;
    public long Generation { get; internal set; }
    public string PreviousGenerationHash { get; internal set; } = string.Empty;
    public string GenerationHash { get; internal set; } = string.Empty;
    public GmHouseRunIdentity Identity { get; internal set; }
    public GmParlorAdaptiveMode Mode { get; internal set; }
    public int Seed { get; internal set; }
    public GmHouseRunStage Stage { get; internal set; }
    public GmParlorAdaptivePackage FrozenPackage { get; internal set; }
    public GmHousePackageBinding PackageBinding { get; internal set; }
    public GmHouseTerminalReceipt PreparedReceipt { get; internal set; }
    public GmSaveData TerminalCheckpoint { get; internal set; }
    public string AcknowledgedReceiptId { get; internal set; } = string.Empty;
    public long AcknowledgedProfileGeneration { get; internal set; }
    public string AcknowledgedProfileGenerationHash { get; internal set; } = string.Empty;
    public string AcknowledgedProfileCommitHash { get; internal set; } = string.Empty;
    public bool CanTeachProfile => Mode != GmParlorAdaptiveMode.Recollection;
}

public sealed class GmHouseKnownPackage
{
    public GmParlorAdaptivePackage Package { get; internal set; }
    public GmHousePackageBinding Binding { get; internal set; }
}

public sealed class GmHouseProfileGeneration
{
    public int SchemaVersion { get; internal set; } = 1;
    public string LineageId { get; internal set; }
    public long Epoch { get; internal set; }
    public long Generation { get; internal set; }
    public string GenerationHash { get; internal set; }
    public string PreviousGenerationHash { get; internal set; }
    public byte[] ProfileDigest { get; internal set; } = new byte[32];
    public IReadOnlyList<GmHouseTerminalReceipt> Receipts { get; internal set; } =
        Array.Empty<GmHouseTerminalReceipt>();
    public IReadOnlyList<GmHouseTerminalReceipt> AdaptiveReceipts => Receipts
        .Where(item => item.ContributesToLearning).ToArray();
    public IReadOnlyList<GmHouseKnownPackage> KnownMirrorPackages => Receipts
        .Where(item => item.Mode == GmParlorAdaptiveMode.Mirror)
        .Select(item => new GmHouseKnownPackage
        {
            Package = item.FrozenPackage.DeepCopy(), Binding = item.PackageBinding,
        }).ToArray();
    public bool MirrorUnlocked => Receipts.Count > 0;
    public GmHouseProfileCas Cas => new GmHouseProfileCas(LineageId, Epoch, Generation,
        GenerationHash);
}

public static class GmHouseModeDirector
{
    public static GmParlorAdaptivePackage FreezeOrdinary(int seed) =>
        GmParlorAdaptivePackage.Baseline(GmParlorAdaptiveMode.Ordinary);
}

public static class GmHouseReceiptCodec
{
    static readonly byte[] IdentityDomain = Encoding.ASCII.GetBytes("TGM/HOUSE/RECEIPT-ID/V1\0");
    static readonly byte[] PayloadDomain = Encoding.ASCII.GetBytes("TGM/HOUSE/RECEIPT-PAYLOAD/V1\0");

    public static byte[] CanonicalIdentityBytes(GmHouseRunIdentity identity,
        long outcomeSequence)
    {
        using (var stream = new MemoryStream())
        {
            stream.Write(IdentityDomain, 0, IdentityDomain.Length);
            GmHouseBinary.WriteString(stream, identity.LineageId);
            GmHouseBinary.WriteInt64(stream, identity.Epoch);
            GmHouseBinary.WriteString(stream, identity.RunId);
            GmHouseBinary.WriteInt64(stream, identity.RunOrdinal);
            GmHouseBinary.WriteInt64(stream, outcomeSequence);
            return stream.ToArray();
        }
    }

    public static string ComputeReceiptId(GmHouseRunIdentity identity, long outcomeSequence) =>
        GmHouseBinary.Hex(GmHouseBinary.Sha256(CanonicalIdentityBytes(identity, outcomeSequence)));

    public static byte[] ComputePayloadHash(GmHouseTerminalReceipt receipt)
    {
        if(receipt==null) throw new ArgumentNullException(nameof(receipt));
        return GmHouseBinary.Sha256(receipt.CanonicalPayloadBytes());
    }

    public static byte[] EncodeCanonicalPayload(GmHouseTerminalReceipt receipt)
    {
        if(receipt==null) throw new ArgumentNullException(nameof(receipt));
        return receipt.CanonicalPayloadBytes();
    }

    internal static byte[] CanonicalPayloadBytes(GmHouseTerminalReceipt receipt)
    {
        using (var stream = new MemoryStream())
        {
            stream.Write(PayloadDomain, 0, PayloadDomain.Length);
            GmHouseBinary.WriteInt32(stream, receipt.CodecVersion);
            GmHouseBinary.WriteString(stream, receipt.LineageId);
            GmHouseBinary.WriteInt64(stream, receipt.Epoch);
            GmHouseBinary.WriteString(stream, receipt.RunId);
            GmHouseBinary.WriteInt64(stream, receipt.RunOrdinal);
            GmHouseBinary.WriteInt64(stream, receipt.OutcomeSequence);
            GmHouseBinary.WriteInt32(stream, (int)receipt.Mode);
            GmHouseBinary.WriteInt32(stream, (int)receipt.Ending);
            stream.WriteByte(receipt.ContributesToLearning ? (byte)1 : (byte)0);
            GmHouseBinary.WriteBytes(stream,
                GmHouseBinary.EncodePackage(receipt.FrozenPackage));
            GmHouseBinary.WriteBytes(stream, receipt.PackageBinding.CanonicalBytes);
            GmHouseBinary.WriteBytes(stream,
                GmHouseBinary.EncodeSummary(receipt.Summary));
            return stream.ToArray();
        }
    }
}

static class GmHouseBinary
{
    const int MaxBlob = 4 * 1024 * 1024;
    const int MaxString = 4096;

    public static void WriteInt32(Stream stream, int value)
    {
        unchecked
        {
            stream.WriteByte((byte)(value >> 24)); stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value);
        }
    }

    public static void WriteInt64(Stream stream, long value)
    {
        unchecked
        {
            for (int shift = 56; shift >= 0; shift -= 8)
                stream.WriteByte((byte)(value >> shift));
        }
    }

    public static int ReadInt32(Stream stream)
    {
        int a = ReadByte(stream), b = ReadByte(stream), c = ReadByte(stream), d = ReadByte(stream);
        return unchecked((a << 24) | (b << 16) | (c << 8) | d);
    }

    public static long ReadInt64(Stream stream)
    {
        ulong value = 0;
        for (int index = 0; index < 8; index++) value = (value << 8) | (uint)ReadByte(stream);
        return unchecked((long)value);
    }

    public static void WriteString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        WriteBytes(stream, bytes);
    }

    public static string ReadString(Stream stream)
    {
        byte[] bytes = ReadBytes(stream, MaxString);
        return new UTF8Encoding(false, true).GetString(bytes);
    }

    public static void WriteBytes(Stream stream, byte[] bytes)
    {
        bytes = bytes ?? Array.Empty<byte>();
        WriteInt32(stream, bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    public static byte[] ReadBytes(Stream stream, int max = MaxBlob)
    {
        int length = ReadInt32(stream);
        if (length < 0 || length > max) throw new InvalidDataException("House blob length is invalid");
        byte[] bytes = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            int read = stream.Read(bytes, offset, length - offset);
            if (read <= 0) throw new EndOfStreamException();
            offset += read;
        }
        return bytes;
    }

    public static byte[] Sha256(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(bytes);
    }

    public static string Hex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2")));
    public static bool IsLowerHex(string value,int length)
    {
        if(value==null||value.Length!=length) return false;
        foreach(char character in value)
            if(!((character>='0'&&character<='9')||(character>='a'&&character<='f'))) return false;
        return true;
    }

    public static byte[] EncodeBinding(GmHousePackageBinding binding)
    {
        using (var stream = new MemoryStream())
        {
            WriteInt32(stream, binding.Version); WriteString(stream, binding.LineageId);
            WriteInt64(stream, binding.Epoch); WriteInt64(stream, binding.ProfileGeneration);
            WriteBytes(stream, binding.ProfileDigest); WriteBytes(stream, binding.HistoryDigest);
            WriteBytes(stream, binding.PackageHash);
            stream.WriteByte(binding.ConsultedProfile ? (byte)1 : (byte)0);
            WriteInt32(stream, binding.SelectedReceiptIds.Count);
            foreach (string id in binding.SelectedReceiptIds) WriteString(stream, id);
            return stream.ToArray();
        }
    }

    public static GmHousePackageBinding DecodeBinding(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes, false))
        {
            var result = new GmHousePackageBinding
            {
                Version = ReadInt32(stream), LineageId = ReadString(stream),
                Epoch = ReadInt64(stream), ProfileGeneration = ReadInt64(stream),
                ProfileDigest = ReadBytes(stream, 32), HistoryDigest = ReadBytes(stream, 32),
                PackageHash = ReadBytes(stream, 32), ConsultedProfile = ReadByte(stream) != 0,
            };
            int count = ReadCount(stream, 5);
            var ids = new string[count];
            for (int index = 0; index < count; index++) ids[index] = ReadString(stream);
            result.SelectedReceiptIds = ids;
            EnsureEnd(stream);
            ValidateBinding(result);
            return result;
        }
    }

    public static void ValidateBinding(GmHousePackageBinding binding)
    {
        if (binding == null || binding.Version != 1 || binding.Epoch < 0 ||
            binding.ProfileGeneration < 0 || binding.ProfileDigest?.Length != 32 ||
            binding.HistoryDigest?.Length != 32 || binding.PackageHash?.Length != 32 ||
            binding.SelectedReceiptIds == null || binding.SelectedReceiptIds.Count > 5 ||
            binding.SelectedReceiptIds.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("House package binding is invalid");
    }

    public static byte[] EncodePackage(GmParlorAdaptivePackage package)
    {
        if (package == null) throw new InvalidDataException("adaptive package is missing");
        if (!package.TryValidate(out string error))
            throw new InvalidDataException("invalid adaptive package: " + error);
        using (var stream = new MemoryStream())
        {
            WriteInt32(stream, package.schemaVersion); WriteInt32(stream, package.catalogVersion);
            WriteInt32(stream, (int)package.mode); WriteInt32(stream, (int)package.provenance);
            WriteInt32(stream, package.attentionTier); WriteInt32(stream, (int)package.honestStrategyId);
            WriteInt32(stream, (int)package.primaryCounterPlanId);
            WriteInt32(stream, (int)package.secondaryPresentationPlanId);
            WriteInt32(stream, (int)package.targetTendency); WriteInt32(stream, (int)package.secondaryTendency);
            WriteInt32(stream, (int)package.cheatGrammarId); WriteInt32(stream, (int)package.tellFamilyId);
            WriteInt32(stream, (int)package.evidenceFamilyId); WriteInt32(stream, (int)package.memoryTokenId);
            WriteBytes(stream, package.historyDigest); WriteInt64(stream, package.selectedFromReceiptOrdinal);
            WriteString(stream, package.fallbackReason);
            return stream.ToArray();
        }
    }

    public static GmParlorAdaptivePackage DecodePackage(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes, false))
        {
            var package = new GmParlorAdaptivePackage
            {
                schemaVersion = ReadInt32(stream), catalogVersion = ReadInt32(stream),
                mode = (GmParlorAdaptiveMode)ReadInt32(stream),
                provenance = (GmParlorPackageProvenance)ReadInt32(stream),
                attentionTier = ReadInt32(stream),
                honestStrategyId = (GmParlorHonestStrategyId)ReadInt32(stream),
                primaryCounterPlanId = (GmParlorCounterPlanId)ReadInt32(stream),
                secondaryPresentationPlanId = (GmParlorPresentationPlanId)ReadInt32(stream),
                targetTendency = (GmParlorTendency)ReadInt32(stream),
                secondaryTendency = (GmParlorTendency)ReadInt32(stream),
                cheatGrammarId = (GmParlorCheatGrammarId)ReadInt32(stream),
                tellFamilyId = (GmParlorTellFamilyId)ReadInt32(stream),
                evidenceFamilyId = (GmParlorEvidenceFamilyId)ReadInt32(stream),
                memoryTokenId = (GmParlorMemoryTokenId)ReadInt32(stream),
                historyDigest = ReadBytes(stream, 32), selectedFromReceiptOrdinal = ReadInt64(stream),
                fallbackReason = ReadString(stream),
            };
            EnsureEnd(stream);
            if (!package.TryValidate(out string error)) throw new InvalidDataException(error);
            return package;
        }
    }

    public static byte[] EncodeSummary(GmParlorCompletedMatchSummary item)
    {
        if (item == null) throw new InvalidDataException("completed summary is missing");
        if (!item.TryValidate(out string error))
            throw new InvalidDataException("invalid completed summary: " + error);
        using (var s = new MemoryStream())
        {
            WriteInt32(s, item.version);
            foreach (int value in new[] { item.judgementOpportunities, item.readAttempts,
                item.correctReads, item.falseReads, item.suspiciousObservations,
                item.acceptedSuspicious, item.acceptedCalm, item.firstReadOpportunityOrdinal,
                item.readFirstThird, item.readMiddleThird, item.readFinalThird,
                item.firstThirdOpportunityCapacity, item.middleThirdOpportunityCapacity,
                item.finalThirdOpportunityCapacity, item.firstReadMatchStartOrdinal,
                item.firstReadMatchJudgementOpportunities, item.firstReadMatchReadFirstThird,
                item.firstReadMatchReadMiddleThird, item.firstReadMatchReadFinalThird }) WriteInt32(s, value);
            WriteInts(s, item.judgementOpportunitiesByMatch); WriteInts(s, item.playerLeadCountBySuit);
            WriteInts(s, item.playerPlayedCountBySuit); WriteInt32(s, item.pressureLeads);
            WriteInt32(s, item.earlyHighRankSpends); WriteInt32(s, item.completedRematches);
            WriteInts(s, item.committedEvidenceClaimsByFamily); WriteInt32(s, item.matchOrdinal);
            WriteInt32(s, (int)item.packageId); WriteInt32(s, (int)item.honestStrategyId);
            WriteInt32(s, (int)item.packageTargetTendency); WriteBytes(s, item.packageHash);
            return s.ToArray();
        }
    }

    public static GmParlorCompletedMatchSummary DecodeSummary(byte[] bytes)
    {
        using (var s = new MemoryStream(bytes, false))
        {
            var item = new GmParlorCompletedMatchSummary { version = ReadInt32(s) };
            int[] v = new int[19]; for (int i = 0; i < v.Length; i++) v[i] = ReadInt32(s);
            item.judgementOpportunities=v[0]; item.readAttempts=v[1]; item.correctReads=v[2];
            item.falseReads=v[3]; item.suspiciousObservations=v[4]; item.acceptedSuspicious=v[5];
            item.acceptedCalm=v[6]; item.firstReadOpportunityOrdinal=v[7]; item.readFirstThird=v[8];
            item.readMiddleThird=v[9]; item.readFinalThird=v[10]; item.firstThirdOpportunityCapacity=v[11];
            item.middleThirdOpportunityCapacity=v[12]; item.finalThirdOpportunityCapacity=v[13];
            item.firstReadMatchStartOrdinal=v[14]; item.firstReadMatchJudgementOpportunities=v[15];
            item.firstReadMatchReadFirstThird=v[16]; item.firstReadMatchReadMiddleThird=v[17];
            item.firstReadMatchReadFinalThird=v[18];
            item.judgementOpportunitiesByMatch=ReadInts(s,32); item.playerLeadCountBySuit=ReadInts(s,4);
            item.playerPlayedCountBySuit=ReadInts(s,4); item.pressureLeads=ReadInt32(s);
            item.earlyHighRankSpends=ReadInt32(s); item.completedRematches=ReadInt32(s);
            item.committedEvidenceClaimsByFamily=ReadInts(s,3); item.matchOrdinal=ReadInt32(s);
            item.packageId=(GmParlorCounterPlanId)ReadInt32(s);
            item.honestStrategyId=(GmParlorHonestStrategyId)ReadInt32(s);
            item.packageTargetTendency=(GmParlorTendency)ReadInt32(s); item.packageHash=ReadBytes(s,32);
            EnsureEnd(s);
            if (!item.TryValidate(out string error)) throw new InvalidDataException(error);
            return item;
        }
    }

    static void WriteInts(Stream stream, int[] values)
    {
        WriteInt32(stream, values?.Length ?? -1);
        if (values != null) foreach (int value in values) WriteInt32(stream, value);
    }
    static int[] ReadInts(Stream stream, int max)
    {
        int count = ReadCount(stream, max); var values = new int[count];
        for (int i=0;i<count;i++) values[i]=ReadInt32(stream); return values;
    }
    static int ReadCount(Stream stream, int max)
    {
        int count=ReadInt32(stream); if(count<0||count>max) throw new InvalidDataException("House count invalid");
        return count;
    }
    static int ReadByte(Stream stream)
    {
        int value=stream.ReadByte(); if(value<0) throw new EndOfStreamException(); return value;
    }
    static void EnsureEnd(Stream stream)
    {
        if(stream.Position!=stream.Length) throw new InvalidDataException("House artifact has trailing bytes");
    }

    public static byte[] EncodeReceipt(GmHouseTerminalReceipt receipt)
    {
        byte[] payload = receipt.CanonicalPayloadBytes();
        byte[] hash = Sha256(payload);
        if (!hash.SequenceEqual(receipt.PayloadHash)) throw new InvalidDataException("receipt payload hash mismatch");
        using(var s=new MemoryStream())
        {
            WriteString(s,"TGM/HOUSE/RECEIPT/BLOB/V1"); WriteString(s,receipt.ReceiptId);
            WriteBytes(s,hash); WriteBytes(s,payload);
            return GmHouseEnvelope.Wrap(GmHouseArtifactKind.ReceiptBlob,1,s.ToArray());
        }
    }

    public static GmHouseTerminalReceipt DecodeReceipt(byte[] bytes)
    {
        bytes=GmHouseEnvelope.Unwrap(bytes,GmHouseArtifactKind.ReceiptBlob,1);
        using(var s=new MemoryStream(bytes,false))
        {
            if(ReadString(s)!="TGM/HOUSE/RECEIPT/BLOB/V1") throw new InvalidDataException("receipt blob domain invalid");
            string id=ReadString(s); byte[] expected=ReadBytes(s,32); byte[] payload=ReadBytes(s); EnsureEnd(s);
            if(!Sha256(payload).SequenceEqual(expected)) throw new InvalidDataException("receipt blob checksum invalid");
            GmHouseTerminalReceipt receipt=DecodeReceiptPayload(payload); receipt.ReceiptId=id; receipt.PayloadHash=expected;
            var identity=new GmHouseRunIdentity(receipt.LineageId,receipt.Epoch,receipt.RunId,receipt.RunOrdinal);
            if(GmHouseReceiptCodec.ComputeReceiptId(identity,receipt.OutcomeSequence)!=id)
                throw new InvalidDataException("receipt identity hash invalid");
            return receipt;
        }
    }

    static GmHouseTerminalReceipt DecodeReceiptPayload(byte[] bytes)
    {
        byte[] domain=Encoding.ASCII.GetBytes("TGM/HOUSE/RECEIPT-PAYLOAD/V1\0");
        using(var s=new MemoryStream(bytes,false))
        {
            byte[] actual=new byte[domain.Length]; if(s.Read(actual,0,actual.Length)!=actual.Length||!actual.SequenceEqual(domain))
                throw new InvalidDataException("receipt payload domain invalid");
            var r=new GmHouseTerminalReceipt
            {
                CodecVersion=ReadInt32(s),LineageId=ReadString(s),Epoch=ReadInt64(s),RunId=ReadString(s),
                RunOrdinal=ReadInt64(s),OutcomeSequence=ReadInt64(s),Mode=(GmParlorAdaptiveMode)ReadInt32(s),
                Ending=(GmEndingType)ReadInt32(s),ContributesToLearning=ReadByte(s)!=0,
                FrozenPackage=DecodePackage(ReadBytes(s)),PackageBinding=DecodeBinding(ReadBytes(s)),
                Summary=DecodeSummary(ReadBytes(s)),
            }; EnsureEnd(s);
            if(r.CodecVersion!=GmHouseTerminalReceipt.CurrentCodecVersion||r.OutcomeSequence!=1||
                !IsLowerHex(r.LineageId,32)||!IsLowerHex(r.RunId,32)||r.Epoch<1||r.RunOrdinal<1||
                (r.Mode!=GmParlorAdaptiveMode.Ordinary&&r.Mode!=GmParlorAdaptiveMode.Mirror)||
                !Enum.IsDefined(typeof(GmEndingType),r.Ending)||
                r.ContributesToLearning!=(r.Ending!=GmEndingType.Madness)||
                r.FrozenPackage==null||r.PackageBinding==null||r.Summary==null||
                !r.FrozenPackage.CanonicalHash.SequenceEqual(r.PackageBinding.PackageHash)||
                !r.Summary.packageHash.SequenceEqual(r.FrozenPackage.CanonicalHash))
                throw new InvalidDataException("receipt payload semantics are invalid");
            return r;
        }
    }
}

enum GmHouseArtifactKind
{
    RootGeneration = 1,
    ProfileGeneration = 2,
    RunGeneration = 3,
    ReceiptBlob = 4,
    CommitRecord = 5,
}

static class GmHouseEnvelope
{
    const int EnvelopeVersion = 1;
    const int MaxPayload = 4 * 1024 * 1024 - 64;
    static readonly byte[] Magic = Encoding.ASCII.GetBytes("TGMHOUSE");
    static readonly byte[] Domain = Encoding.ASCII.GetBytes("TGM/HOUSE/ENVELOPE/V1\0");

    public static byte[] Wrap(GmHouseArtifactKind kind,int schemaVersion,byte[] payload)
    {
        if(payload==null||payload.Length>MaxPayload) throw new InvalidDataException("House envelope payload is too large");
        using(var body=new MemoryStream())
        {
            body.Write(Magic,0,Magic.Length); GmHouseBinary.WriteInt32(body,(int)kind);
            GmHouseBinary.WriteInt32(body,EnvelopeVersion); GmHouseBinary.WriteInt32(body,schemaVersion);
            GmHouseBinary.WriteInt32(body,payload.Length); body.Write(payload,0,payload.Length);
            byte[] unsigned=body.ToArray();
            using(var hashed=new MemoryStream())
            {
                hashed.Write(Domain,0,Domain.Length); hashed.Write(unsigned,0,unsigned.Length);
                byte[] checksum=GmHouseBinary.Sha256(hashed.ToArray());
                body.Write(checksum,0,checksum.Length); return body.ToArray();
            }
        }
    }

    public static byte[] Unwrap(byte[] bytes,GmHouseArtifactKind expectedKind,int expectedSchema)
    {
        if(bytes==null||bytes.Length<Magic.Length+16+32||bytes.Length>4*1024*1024)
            throw new InvalidDataException("House envelope length is invalid");
        using(var stream=new MemoryStream(bytes,false))
        {
            byte[] magic=new byte[Magic.Length]; if(stream.Read(magic,0,magic.Length)!=magic.Length||!magic.SequenceEqual(Magic))
                throw new InvalidDataException("House envelope magic is invalid");
            int kind=GmHouseBinary.ReadInt32(stream); int version=GmHouseBinary.ReadInt32(stream);
            int schema=GmHouseBinary.ReadInt32(stream); int length=GmHouseBinary.ReadInt32(stream);
            if(kind!=(int)expectedKind||version!=EnvelopeVersion||schema!=expectedSchema||length<0||length>MaxPayload)
                throw new InvalidDataException("House envelope kind or schema is unsupported");
            long unsignedLength=Magic.Length+16L+length;
            if(bytes.Length!=unsignedLength+32) throw new InvalidDataException("House envelope has trailing or truncated bytes");
            byte[] payload=new byte[length]; if(stream.Read(payload,0,length)!=length) throw new EndOfStreamException();
            byte[] checksum=new byte[32]; if(stream.Read(checksum,0,32)!=32) throw new EndOfStreamException();
            using(var hashed=new MemoryStream())
            {
                hashed.Write(Domain,0,Domain.Length); hashed.Write(bytes,0,(int)unsignedLength);
                if(!GmHouseBinary.Sha256(hashed.ToArray()).SequenceEqual(checksum))
                    throw new InvalidDataException("House envelope checksum is invalid");
            }
            return payload;
        }
    }
}

sealed class GmHouseCommitRecord
{
    public string Kind;
    public long Sequence;
    public string LineageId;
    public long Epoch;
    public string GenerationFile;
    public string GenerationHash;
    public string PreviousCommitHash;
    public string CommitHash;
}

static class GmHouseArtifactCodec
{
    static byte[] EncodeCheckpoint(GmSaveData checkpoint)
    {
        if (checkpoint == null) return Array.Empty<byte>();
        GmSaveData owned = UnityEngine.JsonUtility.FromJson<GmSaveData>(
            UnityEngine.JsonUtility.ToJson(checkpoint));
        // Preferences have their own durable authority. The House terminal checkpoint contains
        // only resumable run state, never a second copy of accessibility or wall-clock evidence.
        owned.accessibilitySettingsVersion = 0;
        owned.accessibilityCaptions = false;
        owned.accessibilityReducedMotion = false;
        owned.accessibilityVibration = true;
        owned.accessibilityMonoAudio = false;
        owned.accessibilityHighContrast = false;
        owned.accessibilityTextScale = 1f;
        owned.timestampUtc = string.Empty;
        return Encoding.UTF8.GetBytes(UnityEngine.JsonUtility.ToJson(owned));
    }

    static GmSaveData DecodeCheckpoint(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;
        if (bytes.Length > 1024 * 1024) throw new InvalidDataException("terminal checkpoint is too large");
        string json = new UTF8Encoding(false, true).GetString(bytes);
        GmSaveData checkpoint = UnityEngine.JsonUtility.FromJson<GmSaveData>(json);
        if (checkpoint == null) throw new InvalidDataException("terminal checkpoint is invalid");
        return checkpoint;
    }

    public static byte[] EncodeRoot(GmHouseRootGeneration root)
    {
        using(var s=new MemoryStream())
        {
            GmHouseBinary.WriteString(s,"TGM/HOUSE/ROOT/V1"); GmHouseBinary.WriteInt32(s,root.SchemaVersion);
            GmHouseBinary.WriteString(s,root.LineageId); GmHouseBinary.WriteInt64(s,root.Epoch);
            GmHouseBinary.WriteInt64(s,root.Generation); GmHouseBinary.WriteInt64(s,root.NextRunOrdinal);
            s.WriteByte(root.ResetPending?(byte)1:(byte)0); GmHouseBinary.WriteString(s,root.PreviousGenerationHash);
            GmHouseBinary.WriteInt32(s,root.Allocations.Count);
            foreach(var a in root.Allocations)
            {
                GmHouseBinary.WriteInt64(s,a.Epoch); GmHouseBinary.WriteInt64(s,a.RunOrdinal);
                GmHouseBinary.WriteString(s,a.RunId); GmHouseBinary.WriteInt32(s,(int)a.Mode);
            }
            return GmHouseEnvelope.Wrap(GmHouseArtifactKind.RootGeneration,root.SchemaVersion,s.ToArray());
        }
    }

    public static GmHouseRootGeneration DecodeRoot(byte[] bytes)
    {
        bytes=GmHouseEnvelope.Unwrap(bytes,GmHouseArtifactKind.RootGeneration,1);
        using(var s=new MemoryStream(bytes,false))
        {
            if(GmHouseBinary.ReadString(s)!="TGM/HOUSE/ROOT/V1") throw new InvalidDataException("root domain invalid");
            var root=new GmHouseRootGeneration
            {
                SchemaVersion=GmHouseBinary.ReadInt32(s),LineageId=GmHouseBinary.ReadString(s),
                Epoch=GmHouseBinary.ReadInt64(s),Generation=GmHouseBinary.ReadInt64(s),
                NextRunOrdinal=GmHouseBinary.ReadInt64(s),ResetPending=s.ReadByte()!=0,
                PreviousGenerationHash=GmHouseBinary.ReadString(s),
            };
            int count=ReadBoundedCount(s,100000); var allocations=new List<GmHouseRunAllocation>(count);
            for(int i=0;i<count;i++) allocations.Add(new GmHouseRunAllocation
            {
                Epoch=GmHouseBinary.ReadInt64(s),RunOrdinal=GmHouseBinary.ReadInt64(s),
                RunId=GmHouseBinary.ReadString(s),Mode=(GmParlorAdaptiveMode)GmHouseBinary.ReadInt32(s),
            });
            root.Allocations=allocations; EnsureEnd(s); ValidateRoot(root); return root;
        }
    }

    public static void ValidateRoot(GmHouseRootGeneration root)
    {
        if(root.SchemaVersion!=1||!GmHouseBinary.IsLowerHex(root.LineageId,32)||root.Epoch<1||root.Generation<1||
            root.NextRunOrdinal<1||root.Allocations==null||root.Allocations.Count!=root.NextRunOrdinal-1||
            root.Allocations.Any(a=>a.Epoch<1||a.Epoch>root.Epoch||a.RunOrdinal<1||
                a.RunOrdinal>=root.NextRunOrdinal||!GmHouseBinary.IsLowerHex(a.RunId,32)||
                (a.Mode!=GmParlorAdaptiveMode.Ordinary&&a.Mode!=GmParlorAdaptiveMode.Mirror))||
            root.Allocations.GroupBy(a=>a.RunOrdinal).Any(g=>g.Count()!=1)||
            !root.Allocations.Select(a=>a.RunOrdinal).OrderBy(value=>value)
                .SequenceEqual(Enumerable.Range(1,checked((int)(root.NextRunOrdinal-1))).Select(value=>(long)value))||
            root.Allocations.GroupBy(a=>a.RunId,StringComparer.Ordinal).Any(g=>g.Count()!=1))
            throw new InvalidDataException("root allocation commitments are invalid");
    }

    public static byte[] EncodeProfile(GmHouseProfileGeneration profile)
    {
        using(var s=new MemoryStream())
        {
            GmHouseBinary.WriteString(s,"TGM/HOUSE/PROFILE/V1"); GmHouseBinary.WriteInt32(s,profile.SchemaVersion);
            GmHouseBinary.WriteString(s,profile.LineageId); GmHouseBinary.WriteInt64(s,profile.Epoch);
            GmHouseBinary.WriteInt64(s,profile.Generation); GmHouseBinary.WriteString(s,profile.PreviousGenerationHash);
            GmHouseBinary.WriteBytes(s,profile.ProfileDigest); GmHouseBinary.WriteInt32(s,profile.Receipts.Count);
            foreach(var r in profile.Receipts)
            {
                GmHouseBinary.WriteString(s,r.ReceiptId); GmHouseBinary.WriteBytes(s,r.PayloadHash);
                GmHouseBinary.WriteInt64(s,r.RunOrdinal); GmHouseBinary.WriteString(s,r.RunId);
                GmHouseBinary.WriteInt32(s,(int)r.Ending); s.WriteByte(r.ContributesToLearning?(byte)1:(byte)0);
                GmHouseBinary.WriteBytes(s,r.FrozenPackage.CanonicalHash);
            }
            return GmHouseEnvelope.Wrap(GmHouseArtifactKind.ProfileGeneration,profile.SchemaVersion,s.ToArray());
        }
    }

    public static GmHouseProfileGeneration DecodeProfile(byte[] bytes,
        Func<string,GmHouseTerminalReceipt> loadReceipt)
    {
        bytes=GmHouseEnvelope.Unwrap(bytes,GmHouseArtifactKind.ProfileGeneration,1);
        using(var s=new MemoryStream(bytes,false))
        {
            if(GmHouseBinary.ReadString(s)!="TGM/HOUSE/PROFILE/V1") throw new InvalidDataException("profile domain invalid");
            var p=new GmHouseProfileGeneration
            {
                SchemaVersion=GmHouseBinary.ReadInt32(s),LineageId=GmHouseBinary.ReadString(s),
                Epoch=GmHouseBinary.ReadInt64(s),Generation=GmHouseBinary.ReadInt64(s),
                PreviousGenerationHash=GmHouseBinary.ReadString(s),ProfileDigest=GmHouseBinary.ReadBytes(s,32),
            };
            int count=ReadBoundedCount(s,100000); var receipts=new List<GmHouseTerminalReceipt>(count);
            for(int i=0;i<count;i++)
            {
                string id=GmHouseBinary.ReadString(s); byte[] payloadHash=GmHouseBinary.ReadBytes(s,32);
                long ordinal=GmHouseBinary.ReadInt64(s); string runId=GmHouseBinary.ReadString(s);
                var ending=(GmEndingType)GmHouseBinary.ReadInt32(s); bool learns=s.ReadByte()!=0;
                byte[] packageHash=GmHouseBinary.ReadBytes(s,32);
                GmHouseTerminalReceipt receipt=loadReceipt(id);
                if(receipt.ReceiptId!=id||!receipt.PayloadHash.SequenceEqual(payloadHash)||
                    receipt.RunOrdinal!=ordinal||receipt.RunId!=runId||receipt.Ending!=ending||
                    receipt.ContributesToLearning!=learns||!receipt.FrozenPackage.CanonicalHash.SequenceEqual(packageHash))
                    throw new InvalidDataException("receipt blob effects do not match profile reference");
                receipts.Add(receipt);
            }
            EnsureEnd(s); p.Receipts=receipts; ValidateProfile(p); return p;
        }
    }

    public static void ValidateProfile(GmHouseProfileGeneration p)
    {
        if(p.SchemaVersion!=1||!GmHouseBinary.IsLowerHex(p.LineageId,32)||p.Epoch<1||p.Generation<1||
            p.ProfileDigest?.Length!=32||p.Receipts==null||
            p.Receipts.Any(r=>r.LineageId!=p.LineageId||r.Epoch!=p.Epoch)||
            p.Receipts.GroupBy(r=>r.ReceiptId,StringComparer.Ordinal).Any(g=>g.Count()!=1)||
            p.Receipts.GroupBy(r=>r.RunOrdinal).Any(g=>g.Count()!=1)||
            !p.ProfileDigest.SequenceEqual(ComputeProfileDigest(p.LineageId,p.Epoch,p.Receipts)))
            throw new InvalidDataException("profile generation is invalid");
    }

    public static byte[] ComputeProfileDigest(string lineage,long epoch,
        IEnumerable<GmHouseTerminalReceipt> receipts)
    {
        using(var s=new MemoryStream())
        {
            GmHouseBinary.WriteString(s,"TGM/HOUSE/PROFILE-DIGEST/V1"); GmHouseBinary.WriteString(s,lineage);
            GmHouseBinary.WriteInt64(s,epoch);
            foreach(var r in receipts.OrderBy(r=>r.RunOrdinal).ThenBy(r=>r.ReceiptId,StringComparer.Ordinal))
            { GmHouseBinary.WriteString(s,r.ReceiptId); GmHouseBinary.WriteBytes(s,r.PayloadHash); }
            return GmHouseBinary.Sha256(s.ToArray());
        }
    }

    public static byte[] EncodeRun(GmHouseRunGeneration run)
    {
        using(var s=new MemoryStream())
        {
            GmHouseBinary.WriteString(s,"TGM/HOUSE/RUN/V1"); GmHouseBinary.WriteInt32(s,run.SchemaVersion);
            GmHouseBinary.WriteInt64(s,run.Generation); GmHouseBinary.WriteString(s,run.PreviousGenerationHash);
            GmHouseBinary.WriteString(s,run.Identity.LineageId); GmHouseBinary.WriteInt64(s,run.Identity.Epoch);
            GmHouseBinary.WriteString(s,run.Identity.RunId); GmHouseBinary.WriteInt64(s,run.Identity.RunOrdinal);
            GmHouseBinary.WriteInt32(s,(int)run.Mode); GmHouseBinary.WriteInt32(s,run.Seed);
            GmHouseBinary.WriteInt32(s,(int)run.Stage); GmHouseBinary.WriteBytes(s,GmHouseBinary.EncodePackage(run.FrozenPackage));
            GmHouseBinary.WriteBytes(s,run.PackageBinding.CanonicalBytes);
            GmHouseBinary.WriteBytes(s,run.PreparedReceipt==null?Array.Empty<byte>():GmHouseBinary.EncodeReceipt(run.PreparedReceipt));
            GmHouseBinary.WriteBytes(s,EncodeCheckpoint(run.TerminalCheckpoint));
            GmHouseBinary.WriteString(s,run.AcknowledgedReceiptId);
            GmHouseBinary.WriteInt64(s,run.AcknowledgedProfileGeneration);
            GmHouseBinary.WriteString(s,run.AcknowledgedProfileGenerationHash);
            GmHouseBinary.WriteString(s,run.AcknowledgedProfileCommitHash);
            return GmHouseEnvelope.Wrap(GmHouseArtifactKind.RunGeneration,run.SchemaVersion,s.ToArray());
        }
    }

    public static GmHouseRunGeneration DecodeRun(byte[] bytes)
    {
        bytes=GmHouseEnvelope.Unwrap(bytes,GmHouseArtifactKind.RunGeneration,1);
        using(var s=new MemoryStream(bytes,false))
        {
            if(GmHouseBinary.ReadString(s)!="TGM/HOUSE/RUN/V1") throw new InvalidDataException("run domain invalid");
            var run=new GmHouseRunGeneration { SchemaVersion=GmHouseBinary.ReadInt32(s),Generation=GmHouseBinary.ReadInt64(s),
                PreviousGenerationHash=GmHouseBinary.ReadString(s) };
            string lineage=GmHouseBinary.ReadString(s); long epoch=GmHouseBinary.ReadInt64(s);
            string id=GmHouseBinary.ReadString(s); long ordinal=GmHouseBinary.ReadInt64(s);
            run.Identity=new GmHouseRunIdentity(lineage,epoch,id,ordinal);
            run.Mode=(GmParlorAdaptiveMode)GmHouseBinary.ReadInt32(s); run.Seed=GmHouseBinary.ReadInt32(s);
            run.Stage=(GmHouseRunStage)GmHouseBinary.ReadInt32(s); run.FrozenPackage=GmHouseBinary.DecodePackage(GmHouseBinary.ReadBytes(s));
            run.PackageBinding=GmHouseBinary.DecodeBinding(GmHouseBinary.ReadBytes(s));
            byte[] receipt=GmHouseBinary.ReadBytes(s); run.PreparedReceipt=receipt.Length==0?null:GmHouseBinary.DecodeReceipt(receipt);
            run.TerminalCheckpoint=DecodeCheckpoint(GmHouseBinary.ReadBytes(s,1024*1024));
            run.AcknowledgedReceiptId=GmHouseBinary.ReadString(s);
            run.AcknowledgedProfileGeneration=GmHouseBinary.ReadInt64(s);
            run.AcknowledgedProfileGenerationHash=GmHouseBinary.ReadString(s);
            run.AcknowledgedProfileCommitHash=GmHouseBinary.ReadString(s);
            EnsureEnd(s); ValidateRun(run); return run;
        }
    }

    static void ValidateRun(GmHouseRunGeneration run)
    {
        bool recollection=run.Mode==GmParlorAdaptiveMode.Recollection;
        bool validMode=run.Mode==GmParlorAdaptiveMode.Ordinary||
            run.Mode==GmParlorAdaptiveMode.Mirror||recollection;
        bool validStage=Enum.IsDefined(typeof(GmHouseRunStage),run.Stage);
        bool prepared=run.Stage==GmHouseRunStage.Prepared||run.Stage==GmHouseRunStage.Acknowledged;
        if(run.SchemaVersion!=1||run.Generation<0||!GmHouseBinary.IsLowerHex(run.Identity.LineageId,32)||
            !validMode||!validStage||
            run.Identity.Epoch<1||!GmHouseBinary.IsLowerHex(run.Identity.RunId,32)||
            (recollection?run.Identity.RunOrdinal!=0:run.Identity.RunOrdinal<1)||
            !run.FrozenPackage.CanonicalHash.SequenceEqual(run.PackageBinding.PackageHash)||
            (prepared&&run.PreparedReceipt==null)||(prepared&&run.TerminalCheckpoint==null)||
            (prepared&&run.TerminalCheckpoint.lastCheckpoint!="ending")||
            (prepared&&run.TerminalCheckpoint.houseRunId!=run.Identity.RunId)||
            (prepared&&!ReceiptMatchesRun(run.PreparedReceipt,run))||
            (run.Stage==GmHouseRunStage.Acknowledged&&
                (run.AcknowledgedReceiptId!=run.PreparedReceipt.ReceiptId||
                 run.AcknowledgedProfileGeneration<1||run.AcknowledgedProfileGenerationHash?.Length!=64||
                 run.AcknowledgedProfileCommitHash?.Length!=64)))
            throw new InvalidDataException("run generation is invalid");
    }

    internal static bool ReceiptMatchesRun(GmHouseTerminalReceipt receipt,GmHouseRunGeneration run)
    {
        if(receipt==null||run==null||receipt.CodecVersion!=1||receipt.OutcomeSequence!=1||
            !Enum.IsDefined(typeof(GmEndingType),receipt.Ending)||receipt.LineageId!=run.Identity.LineageId||
            receipt.Epoch!=run.Identity.Epoch||receipt.RunId!=run.Identity.RunId||
            receipt.RunOrdinal!=run.Identity.RunOrdinal||receipt.Mode!=run.Mode||
            receipt.ContributesToLearning!=(receipt.Ending!=GmEndingType.Madness)||
            receipt.ReceiptId!=GmHouseReceiptCodec.ComputeReceiptId(run.Identity,1)||
            receipt.PayloadHash==null||!receipt.PayloadHash.SequenceEqual(
                GmHouseBinary.Sha256(receipt.CanonicalPayloadBytes()))||
            receipt.FrozenPackage==null||receipt.PackageBinding==null||receipt.Summary==null||
            !receipt.FrozenPackage.CanonicalHash.SequenceEqual(run.FrozenPackage.CanonicalHash)||
            !receipt.PackageBinding.CanonicalHash.SequenceEqual(run.PackageBinding.CanonicalHash)||
            !receipt.Summary.packageHash.SequenceEqual(run.FrozenPackage.CanonicalHash)) return false;
        return true;
    }

    public static byte[] EncodeCommit(GmHouseCommitRecord record,bool includeHash)
    {
        using(var s=new MemoryStream())
        {
            GmHouseBinary.WriteString(s,"TGM/HOUSE/COMMIT/V1"); GmHouseBinary.WriteString(s,record.Kind);
            GmHouseBinary.WriteInt64(s,record.Sequence); GmHouseBinary.WriteString(s,record.LineageId);
            GmHouseBinary.WriteInt64(s,record.Epoch); GmHouseBinary.WriteString(s,record.GenerationFile);
            GmHouseBinary.WriteString(s,record.GenerationHash); GmHouseBinary.WriteString(s,record.PreviousCommitHash);
            if(includeHash) GmHouseBinary.WriteString(s,record.CommitHash);
            return GmHouseEnvelope.Wrap(GmHouseArtifactKind.CommitRecord,1,s.ToArray());
        }
    }

    public static GmHouseCommitRecord DecodeCommit(byte[] bytes)
    {
        bytes=GmHouseEnvelope.Unwrap(bytes,GmHouseArtifactKind.CommitRecord,1);
        using(var s=new MemoryStream(bytes,false))
        {
            if(GmHouseBinary.ReadString(s)!="TGM/HOUSE/COMMIT/V1") throw new InvalidDataException("commit domain invalid");
            var r=new GmHouseCommitRecord { Kind=GmHouseBinary.ReadString(s),Sequence=GmHouseBinary.ReadInt64(s),
                LineageId=GmHouseBinary.ReadString(s),Epoch=GmHouseBinary.ReadInt64(s),
                GenerationFile=GmHouseBinary.ReadString(s),GenerationHash=GmHouseBinary.ReadString(s),
                PreviousCommitHash=GmHouseBinary.ReadString(s),CommitHash=GmHouseBinary.ReadString(s) };
            EnsureEnd(s); string computed=GmHouseBinary.Hex(GmHouseBinary.Sha256(EncodeCommit(r,false)));
            if(r.CommitHash!=computed) throw new InvalidDataException("commit checksum invalid"); return r;
        }
    }

    static int ReadBoundedCount(Stream s,int max)
    { int c=GmHouseBinary.ReadInt32(s); if(c<0||c>max) throw new InvalidDataException("count invalid"); return c; }
    static void EnsureEnd(Stream s) { if(s.Position!=s.Length) throw new InvalidDataException("trailing bytes"); }
}

public sealed class GmHouseMemoryStore
{
    readonly string domain;
    readonly IGmHouseFileSystem fileSystem;
    readonly string leasePath;
    GmHouseRootGeneration currentRoot;
    GmHouseProfileGeneration currentProfile;
    string lastError = string.Empty;

    string RootDirectory => Path.Combine(domain,"root");
    string RootGenerationDirectory => Path.Combine(RootDirectory,"generations");
    string RootCommitDirectory => Path.Combine(RootDirectory,"commits");
    string ProfileDirectory => Path.Combine(domain,"profile");
    string ProfileGenerationDirectory => Path.Combine(ProfileDirectory,"generations");
    string ProfileCommitDirectory => Path.Combine(ProfileDirectory,"commits");
    string ReceiptDirectory => Path.Combine(domain,"receipts");
    string RunDirectory => Path.Combine(domain,"runs");

    public GmHouseRootGeneration CurrentRoot => currentRoot;
    public GmHouseProfileGeneration CurrentProfile => currentProfile;
    public string LastError => lastError;
    public IReadOnlyList<string> RootCommitRecords => Files(RootCommitDirectory,"*.commit");
    public IReadOnlyList<string> ProfileCommitRecords => Files(ProfileCommitDirectory,"*.commit");
    public string CurrentProfileCommitHash => CurrentCommitHash(ProfileCommitDirectory,"profile");

    public GmHouseMemoryStore(string domain, IGmHouseFileSystem fileSystem=null)
    {
        if(string.IsNullOrWhiteSpace(domain)) throw new ArgumentException("House domain is required",nameof(domain));
        this.domain=Path.GetFullPath(domain); this.fileSystem=fileSystem??new GmHousePhysicalFileSystem();
        leasePath=Path.Combine(this.domain,".house-memory.lease");
    }

    public bool TryOpenOrCreate(out GmHouseProfileGeneration profile,out string error)
    {
        profile=null;
        try
        {
            fileSystem.CreateDirectory(domain);
            using(fileSystem.AcquireExclusiveLease(leasePath))
            {
                if(Files(RootCommitDirectory,"*.commit").Count==0)
                {
                    string[] discoverable=Directory.GetFiles(domain,"*",SearchOption.AllDirectories)
                        .Where(path=>path!=leasePath).ToArray();
                    if(discoverable.Length==0) CreateGenesisUnlocked();
                    else ResumeInterruptedRootGenesisUnlocked(discoverable);
                }
                else if(Files(ProfileCommitDirectory,"*.commit").Count==0)
                    ResumeInterruptedGenesisUnlocked();
                LoadAllUnlocked();
                if(currentRoot.ResetPending) CompletePendingResetUnlocked();
                profile=currentProfile;
            }
            return Success(out error);
        }
        catch(Exception ex) { return Failure(ex,out error); }
    }

    public bool TryOpenExisting(out GmHouseProfileGeneration profile,out string error)
    {
        profile=null;
        try
        {
            if(!fileSystem.DirectoryExists(domain)) throw new InvalidDataException("House domain is missing");
            using(fileSystem.AcquireExclusiveLease(leasePath))
            {
                if(Files(ProfileCommitDirectory,"*.commit").Count==0)
                    ResumeInterruptedGenesisUnlocked();
                LoadAllUnlocked();
                if(currentRoot.ResetPending) CompletePendingResetUnlocked();
                profile=currentProfile;
            }
            return Success(out error);
        }
        catch(Exception ex) { return Failure(ex,out error); }
    }

    void CreateGenesisUnlocked()
    {
        CreateDirectories(); string lineage=Guid.NewGuid().ToString("N");
        var root=new GmHouseRootGeneration { LineageId=lineage,Epoch=1,Generation=1,
            NextRunOrdinal=1,ResetPending=false,PreviousGenerationHash=string.Empty,
            Allocations=Array.Empty<GmHouseRunAllocation>() };
        WriteRootUnlocked(root,null);
        var profile=new GmHouseProfileGeneration { LineageId=lineage,Epoch=1,Generation=1,
            PreviousGenerationHash=string.Empty,Receipts=Array.Empty<GmHouseTerminalReceipt>() };
        profile.ProfileDigest=GmHouseArtifactCodec.ComputeProfileDigest(lineage,1,profile.Receipts);
        WriteProfileUnlocked(profile,null);
    }

    void ResumeInterruptedRootGenesisUnlocked(string[] discoverable)
    {
        string[] generations=Files(RootGenerationDirectory,"*.bin").ToArray();
        if(generations.Length!=1||discoverable.Length!=1||
            Path.GetFullPath(discoverable[0])!=Path.GetFullPath(generations[0]))
            throw new InvalidDataException("root is missing from nonempty House domain");
        GmHouseRootGeneration root=GmHouseArtifactCodec.DecodeRoot(
            fileSystem.ReadAllBytes(generations[0]));
        if(root.Generation!=1||root.Epoch!=1||root.NextRunOrdinal!=1||root.ResetPending||
            root.PreviousGenerationHash!=string.Empty||root.Allocations.Count!=0)
            throw new InvalidDataException("orphan root is not an exact interrupted genesis");
        WriteRootUnlocked(root,null);
        ResumeInterruptedGenesisUnlocked();
    }

    void ResumeInterruptedGenesisUnlocked()
    {
        currentRoot=LoadRootUnlocked();
        if(currentRoot.Generation!=1||currentRoot.Epoch!=1||currentRoot.NextRunOrdinal!=1||
            currentRoot.ResetPending||currentRoot.Allocations.Count!=0||
            Files(ReceiptDirectory,"*").Count!=0||Files(RunDirectory,"*").Count!=0||
            (Directory.Exists(RunDirectory)&&Directory.GetDirectories(RunDirectory).Length!=0))
            throw new InvalidDataException("missing profile is not an exact interrupted genesis");
        foreach(string path in Files(ProfileGenerationDirectory,"*.bin"))
        {
            GmHouseProfileGeneration orphan=GmHouseArtifactCodec.DecodeProfile(
                fileSystem.ReadAllBytes(path),LoadReceiptBlobUnlocked);
            if(orphan.LineageId!=currentRoot.LineageId||orphan.Epoch!=1||orphan.Generation!=1||
                orphan.PreviousGenerationHash!=string.Empty||orphan.Receipts.Count!=0)
                throw new InvalidDataException("interrupted genesis contains foreign profile evidence");
        }
        var profile=new GmHouseProfileGeneration { LineageId=currentRoot.LineageId,Epoch=1,
            Generation=1,PreviousGenerationHash=string.Empty,
            Receipts=Array.Empty<GmHouseTerminalReceipt>() };
        profile.ProfileDigest=GmHouseArtifactCodec.ComputeProfileDigest(
            profile.LineageId,profile.Epoch,profile.Receipts);
        WriteProfileUnlocked(profile,null);
    }

    void CreateDirectories()
    {
        foreach(string path in new[]{RootGenerationDirectory,RootCommitDirectory,
            ProfileGenerationDirectory,ProfileCommitDirectory,ReceiptDirectory,RunDirectory})
            fileSystem.CreateDirectory(path);
    }

    void LoadAllUnlocked()
    {
        currentRoot=LoadRootUnlocked(); currentProfile=LoadProfileUnlocked();
        if(currentProfile.LineageId!=currentRoot.LineageId)
            throw new InvalidDataException("profile lineage does not match root");
        if(!currentRoot.ResetPending&&currentProfile.Epoch!=currentRoot.Epoch)
            throw new InvalidDataException("profile epoch does not match stable root");
        if(currentRoot.ResetPending&&currentProfile.Epoch>currentRoot.Epoch)
            throw new InvalidDataException("profile epoch is ahead of pending root");
    }

    GmHouseRootGeneration LoadRootUnlocked()
    {
        GmHouseCommitRecord commit=LoadCommitHead(RootCommitDirectory,"root");
        string path=Path.Combine(RootGenerationDirectory,commit.GenerationFile);
        byte[] bytes=fileSystem.ReadAllBytes(path); string hash=GmHouseBinary.Hex(GmHouseBinary.Sha256(bytes));
        if(hash!=commit.GenerationHash) throw new InvalidDataException("root generation checksum invalid");
        GmHouseRootGeneration root=GmHouseArtifactCodec.DecodeRoot(bytes);
        if(root.LineageId!=commit.LineageId||root.Epoch!=commit.Epoch||root.Generation!=commit.Sequence)
            throw new InvalidDataException("root commit does not bind generation");
        root.GenerationHash=hash; return root;
    }

    GmHouseProfileGeneration LoadProfileUnlocked(bool permitPreviousEpoch=false)
    {
        GmHouseCommitRecord commit=LoadCommitHead(ProfileCommitDirectory,"profile");
        string path=Path.Combine(ProfileGenerationDirectory,commit.GenerationFile);
        byte[] bytes=fileSystem.ReadAllBytes(path); string hash=GmHouseBinary.Hex(GmHouseBinary.Sha256(bytes));
        if(hash!=commit.GenerationHash) throw new InvalidDataException("profile generation checksum invalid");
        GmHouseProfileGeneration profile=GmHouseArtifactCodec.DecodeProfile(bytes,LoadReceiptBlobUnlocked);
        if(profile.LineageId!=commit.LineageId||profile.Epoch!=commit.Epoch||profile.Generation!=commit.Sequence)
            throw new InvalidDataException("profile commit does not bind generation");
        profile.GenerationHash=hash; return profile;
    }

    GmHouseCommitRecord LoadCommitHead(string directory,string kind)
    {
        try
        {
            IReadOnlyList<string> files=Files(directory,"*.commit");
            if(files.Count==0) throw new InvalidDataException(kind+" commit chain is missing");
            string previous=string.Empty; long priorSequence=0; GmHouseCommitRecord last=null;
            foreach(string path in files)
            {
                GmHouseCommitRecord item=GmHouseArtifactCodec.DecodeCommit(fileSystem.ReadAllBytes(path));
                if(item.Kind!=kind||item.Sequence!=priorSequence+1||item.PreviousCommitHash!=previous)
                    throw new InvalidDataException(kind+" commit chain is invalid");
                string expectedPrefix=$"{kind}-gen-{item.Sequence:D20}-";
                if(item.GenerationFile!=Path.GetFileName(item.GenerationFile)||
                    !item.GenerationFile.StartsWith(expectedPrefix,StringComparison.Ordinal)||
                    !item.GenerationFile.EndsWith(".bin",StringComparison.Ordinal)||
                    item.GenerationFile.Length!=expectedPrefix.Length+64+4||
                    !GmHouseBinary.IsLowerHex(item.GenerationFile.Substring(expectedPrefix.Length,64),64)||
                    !GmHouseBinary.IsLowerHex(item.LineageId,32)||
                    item.GenerationFile.Substring(expectedPrefix.Length,64)!=item.GenerationHash)
                    throw new InvalidDataException(kind+" commit generation path is invalid");
                string generations=Path.Combine(Path.GetDirectoryName(directory),"generations");
                string generationPath=Path.Combine(generations,item.GenerationFile);
                if(!fileSystem.FileExists(generationPath))
                    throw new InvalidDataException(kind+" generation is missing");
                byte[] generationBytes=fileSystem.ReadAllBytes(generationPath);
                if(GmHouseBinary.Hex(GmHouseBinary.Sha256(generationBytes))!=item.GenerationHash)
                    throw new InvalidDataException(kind+" generation checksum is invalid");
                string encodedPrevious=ReadPreviousGenerationHash(kind,generationBytes);
                if(encodedPrevious!=(last?.GenerationHash??string.Empty))
                    throw new InvalidDataException(kind+" predecessor generation hash is invalid");
                previous=item.CommitHash; priorSequence=item.Sequence; last=item;
            }
            return last;
        }
        catch(Exception ex) when(!(ex is InvalidDataException&&ex.Message.StartsWith(kind+" ")))
        { throw new InvalidDataException(kind+" commit is invalid: "+ex.Message,ex); }
    }

    string ReadPreviousGenerationHash(string kind,byte[] bytes)
    {
        if(kind=="root") return GmHouseArtifactCodec.DecodeRoot(bytes).PreviousGenerationHash;
        if(kind=="profile") return GmHouseArtifactCodec.DecodeProfile(bytes,LoadReceiptBlobUnlocked)
            .PreviousGenerationHash;
        if(kind=="run") return GmHouseArtifactCodec.DecodeRun(bytes).PreviousGenerationHash;
        throw new InvalidDataException("unknown House commit kind");
    }

    public bool TryAllocateCampaignRun(GmParlorAdaptiveMode mode,int seed,
        out GmHouseRunGeneration run,out string error)
    {
        run=null;
        try
        {
            if(mode!=GmParlorAdaptiveMode.Ordinary&&mode!=GmParlorAdaptiveMode.Mirror)
                throw new ArgumentException("campaign mode must be Ordinary or Mirror");
            using(fileSystem.AcquireExclusiveLease(leasePath))
            {
                LoadAllUnlocked(); if(currentRoot.ResetPending) CompletePendingResetUnlocked();
                if(mode==GmParlorAdaptiveMode.Mirror&&!currentProfile.MirrorUnlocked)
                    throw new InvalidOperationException("Mirror is not unlocked");
                GmParlorAdaptivePackage package; GmHousePackageBinding binding;
                if(mode==GmParlorAdaptiveMode.Ordinary)
                {
                    package=GmHouseModeDirector.FreezeOrdinary(seed);
                    binding=EmptyBinding(currentRoot,package);
                }
                else FreezeMirrorUnlocked(seed,out package,out binding);

                long ordinal=currentRoot.NextRunOrdinal; string runId=Guid.NewGuid().ToString("N");
                var allocations=currentRoot.Allocations.Select(CopyAllocation).ToList();
                allocations.Add(new GmHouseRunAllocation { Epoch=currentRoot.Epoch,RunOrdinal=ordinal,
                    RunId=runId,Mode=mode });
                var nextRoot=CopyRoot(currentRoot); nextRoot.Generation++; nextRoot.NextRunOrdinal++;
                nextRoot.PreviousGenerationHash=currentRoot.GenerationHash; nextRoot.Allocations=allocations;
                WriteRootUnlocked(nextRoot,CurrentCommitHash(RootCommitDirectory,"root")); currentRoot=nextRoot;

                run=new GmHouseRunGeneration { Generation=1,Identity=new GmHouseRunIdentity(
                    currentRoot.LineageId,currentRoot.Epoch,runId,ordinal),Mode=mode,Seed=seed,
                    Stage=GmHouseRunStage.Active,FrozenPackage=package,PackageBinding=binding };
                WriteRunUnlocked(run,null); run=LoadRunUnlocked(runId);
            }
            return Success(out error);
        }
        catch(Exception ex){ run=null; return Failure(ex,out error); }
    }

    void FreezeMirrorUnlocked(int seed,out GmParlorAdaptivePackage package,
        out GmHousePackageBinding binding)
    {
        GmHouseTerminalReceipt[] adaptive=currentProfile.AdaptiveReceipts
            .OrderBy(r=>r.RunOrdinal).ThenBy(r=>r.ReceiptId,StringComparer.Ordinal).ToArray();
        var view=new GmParlorProfileView(adaptive.Select(r=>new GmParlorCompletedRunReceipt
            { runOrdinal=r.RunOrdinal,receiptId=r.ReceiptId,summary=r.Summary.DeepCopy() }));
        package=GmParlorAdaptiveDirector.Select(view,seed,GmParlorAdaptiveMode.Mirror,
            GmParlorAdaptivePackage.CurrentCatalogVersion);
        string[] ids=adaptive.Skip(Math.Max(0,adaptive.Length-5)).Select(r=>r.ReceiptId).ToArray();
        binding=new GmHousePackageBinding { LineageId=currentRoot.LineageId,Epoch=currentRoot.Epoch,
            ProfileGeneration=currentProfile.Generation,ProfileDigest=(byte[])currentProfile.ProfileDigest.Clone(),
            HistoryDigest=(byte[])package.historyDigest.Clone(),PackageHash=package.CanonicalHash,
            SelectedReceiptIds=ids,ConsultedProfile=true };
    }

    static GmHousePackageBinding EmptyBinding(GmHouseRootGeneration root,
        GmParlorAdaptivePackage package) => new GmHousePackageBinding
    {
        LineageId=root.LineageId,Epoch=root.Epoch,ProfileGeneration=0,
        ProfileDigest=new byte[32],HistoryDigest=new byte[32],PackageHash=package.CanonicalHash,
        SelectedReceiptIds=Array.Empty<string>(),ConsultedProfile=false,
    };

    public bool TryBeginRecollection(GmParlorAdaptivePackage known,
        GmHousePackageBinding binding,out GmHouseRunGeneration run,out string error)
    {
        run=null;
        try
        {
            using(fileSystem.AcquireExclusiveLease(leasePath))
            {
                LoadAllUnlocked(); if(currentRoot.ResetPending) CompletePendingResetUnlocked();
                if(known==null||binding==null) throw new ArgumentNullException("known package");
                bool member=currentProfile.KnownMirrorPackages.Any(item=>
                    item.Package.CanonicalHash.SequenceEqual(known.CanonicalHash)&&
                    item.Binding.CanonicalHash.SequenceEqual(binding.CanonicalHash));
                if(!member) throw new InvalidOperationException("Recollection requires an exact known profile package");
                GmParlorAdaptivePackage recollection=GmParlorAdaptiveDirector.SelectKnownForRecollection(known);
                var recollectionBinding=GmHouseBinary.DecodeBinding(binding.CanonicalBytes);
                recollectionBinding.PackageHash=recollection.CanonicalHash;
                run=new GmHouseRunGeneration { Generation=0,Identity=new GmHouseRunIdentity(
                    currentRoot.LineageId,currentRoot.Epoch,Guid.NewGuid().ToString("N"),0),
                    Mode=GmParlorAdaptiveMode.Recollection,Seed=0,Stage=GmHouseRunStage.Active,
                    FrozenPackage=recollection,PackageBinding=recollectionBinding };
            }
            return Success(out error);
        }
        catch(Exception ex){ run=null; return Failure(ex,out error); }
    }

    public bool TryCreateReceipt(GmHouseRunGeneration run,GmEndingType ending,
        GmParlorBehaviorAccumulator behavior,out GmHouseTerminalReceipt receipt,out string error)
    {
        receipt=null;
        try
        {
            if(run==null||behavior==null) throw new ArgumentNullException();
            if(!run.CanTeachProfile||run.Identity.RunOrdinal<=0)
                throw new InvalidOperationException("Recollection cannot create a terminal receipt");
            if(run.Stage!=GmHouseRunStage.Active) throw new InvalidOperationException("run is not active");
            if(!Enum.IsDefined(typeof(GmEndingType),ending)) throw new ArgumentOutOfRangeException(nameof(ending));
            GmParlorCompletedMatchSummary summary=behavior.CreateCompletedRunSummary();
            if(!summary.packageHash.SequenceEqual(run.FrozenPackage.CanonicalHash))
                throw new InvalidDataException("sealed accumulator package does not match frozen run package");
            receipt=new GmHouseTerminalReceipt { LineageId=run.Identity.LineageId,Epoch=run.Identity.Epoch,
                RunId=run.Identity.RunId,RunOrdinal=run.Identity.RunOrdinal,Mode=run.Mode,Ending=ending,
                ContributesToLearning=ending!=GmEndingType.Madness,FrozenPackage=run.FrozenPackage.DeepCopy(),
                PackageBinding=GmHouseBinary.DecodeBinding(run.PackageBinding.CanonicalBytes),Summary=summary };
            receipt.ReceiptId=GmHouseReceiptCodec.ComputeReceiptId(run.Identity,1);
            receipt.PayloadHash=GmHouseBinary.Sha256(receipt.CanonicalPayloadBytes());
            GmHouseBinary.DecodeReceipt(GmHouseBinary.EncodeReceipt(receipt));
            return Success(out error);
        }
        catch(Exception ex){ receipt=null; return Failure(ex,out error); }
    }

    public bool TryApplyReceipt(GmHouseTerminalReceipt receipt,GmHouseProfileCas expected,
        out GmHouseProfileGeneration profile,out string error)
    {
        profile=null;
        try
        {
            using(fileSystem.AcquireExclusiveLease(leasePath))
            {
                LoadAllUnlocked(); if(currentRoot.ResetPending) CompletePendingResetUnlocked();
                ValidateReceiptForCurrentRoot(receipt);
                if(expected.LineageId!=currentProfile.LineageId||expected.Epoch!=currentProfile.Epoch)
                    throw new InvalidDataException("profile CAS lineage or epoch is stale");
                ValidateExpectedProfileCasUnlocked(expected);
                GmHouseTerminalReceipt existing=currentProfile.Receipts.FirstOrDefault(r=>r.ReceiptId==receipt.ReceiptId);
                if(existing!=null)
                {
                    if(!existing.PayloadHash.SequenceEqual(receipt.PayloadHash)||
                        !existing.CanonicalPayloadBytes().SequenceEqual(receipt.CanonicalPayloadBytes()))
                        throw new InvalidDataException("same receipt id has different payload");
                    profile=currentProfile; return Success(out error);
                }
                if(currentProfile.Receipts.Any(r=>r.RunOrdinal==receipt.RunOrdinal||r.RunId==receipt.RunId))
                    throw new InvalidDataException("run identity already has another receipt");

                string blobPath=Path.Combine(ReceiptDirectory,receipt.ReceiptId+".receipt");
                byte[] blob=GmHouseBinary.EncodeReceipt(receipt);
                if(!fileSystem.FileExists(blobPath))
                    fileSystem.WriteNewDurable(blobPath,blob,GmHouseDurabilityPoint.ReceiptBlob);
                GmHouseTerminalReceipt reopened=LoadReceiptBlobUnlocked(receipt.ReceiptId);
                if(!reopened.PayloadHash.SequenceEqual(receipt.PayloadHash)||
                    !reopened.CanonicalPayloadBytes().SequenceEqual(receipt.CanonicalPayloadBytes()))
                    throw new InvalidDataException("receipt blob did not reopen with exact effects");

                var receipts=currentProfile.Receipts.Concat(new[]{reopened})
                    .OrderBy(r=>r.RunOrdinal).ThenBy(r=>r.ReceiptId,StringComparer.Ordinal).ToArray();
                var next=new GmHouseProfileGeneration { LineageId=currentProfile.LineageId,Epoch=currentProfile.Epoch,
                    Generation=currentProfile.Generation+1,PreviousGenerationHash=currentProfile.GenerationHash,
                    Receipts=receipts };
                next.ProfileDigest=GmHouseArtifactCodec.ComputeProfileDigest(next.LineageId,next.Epoch,receipts);
                WriteProfileUnlocked(next,CurrentCommitHash(ProfileCommitDirectory,"profile"));
                currentProfile=LoadProfileUnlocked(); profile=currentProfile;
            }
            return Success(out error);
        }
        catch(Exception ex){ profile=currentProfile; return Failure(ex,out error); }
    }

    void ValidateExpectedProfileCasUnlocked(GmHouseProfileCas expected)
    {
        if(expected.Generation<1||expected.Generation>currentProfile.Generation)
            throw new InvalidDataException("profile CAS generation is impossible");
        string path=Path.Combine(ProfileCommitDirectory,
            $"profile-commit-{expected.Generation:D20}.commit");
        if(!fileSystem.FileExists(path)) throw new InvalidDataException("profile CAS commit is missing");
        GmHouseCommitRecord commit=GmHouseArtifactCodec.DecodeCommit(fileSystem.ReadAllBytes(path));
        if(commit.LineageId!=expected.LineageId||commit.Epoch!=expected.Epoch||
            commit.GenerationHash!=expected.GenerationHash)
            throw new InvalidDataException("profile CAS generation hash does not match committed history");
    }

    void ValidateReceiptForCurrentRoot(GmHouseTerminalReceipt receipt,bool requirePrepared=true)
    {
        if(receipt==null) throw new ArgumentNullException(nameof(receipt));
        GmHouseTerminalReceipt decoded=GmHouseBinary.DecodeReceipt(GmHouseBinary.EncodeReceipt(receipt));
        if(decoded.LineageId!=currentRoot.LineageId) throw new InvalidDataException("receipt lineage is stale");
        if(decoded.Epoch!=currentRoot.Epoch) throw new InvalidDataException("receipt epoch is stale");
        GmHouseRunAllocation allocation=currentRoot.Allocations.FirstOrDefault(a=>a.RunOrdinal==decoded.RunOrdinal);
        if(allocation==null||allocation.RunId!=decoded.RunId||allocation.Epoch!=decoded.Epoch||allocation.Mode!=decoded.Mode)
            throw new InvalidDataException("receipt does not match a root allocation commitment");
        GmHouseRunGeneration allocatedRun=LoadRunUnlocked(decoded.RunId);
        if(!GmHouseArtifactCodec.ReceiptMatchesRun(decoded,allocatedRun))
            throw new InvalidDataException("receipt effects do not match the allocated run");
        if(requirePrepared&&allocatedRun.Stage!=GmHouseRunStage.Prepared&&
            allocatedRun.Stage!=GmHouseRunStage.Acknowledged)
            throw new InvalidDataException("receipt has no durable prepared terminal run");
        if(requirePrepared&&(allocatedRun.PreparedReceipt.ReceiptId!=decoded.ReceiptId||
            !allocatedRun.PreparedReceipt.PayloadHash.SequenceEqual(decoded.PayloadHash)||
            !allocatedRun.PreparedReceipt.CanonicalPayloadBytes().SequenceEqual(
                decoded.CanonicalPayloadBytes())))
            throw new InvalidDataException(allocatedRun.PreparedReceipt.ReceiptId==decoded.ReceiptId
                ? "same receipt id has different payload"
                : "prepared run contains another terminal receipt");
    }

    GmHouseTerminalReceipt LoadReceiptBlobUnlocked(string id)
    {
        string path=Path.Combine(ReceiptDirectory,id+".receipt");
        if(!fileSystem.FileExists(path)) throw new InvalidDataException("receipt blob is missing: "+id);
        try { return GmHouseBinary.DecodeReceipt(fileSystem.ReadAllBytes(path)); }
        catch(Exception ex) { throw new InvalidDataException("receipt blob is invalid: "+ex.Message,ex); }
    }

    public bool TryCommitPreparedRun(GmHouseRunGeneration source,GmHouseTerminalReceipt receipt,
        GmSaveData terminalCheckpoint,out GmHouseRunGeneration prepared,out string error)
    {
        prepared=null;
        try
        {
            using(fileSystem.AcquireExclusiveLease(leasePath))
            {
                if(source==null) throw new ArgumentNullException(nameof(source));
                LoadAllUnlocked(); GmHouseRunGeneration current=LoadRunUnlocked(source.Identity.RunId);
                ValidateReceiptForCurrentRoot(receipt,requirePrepared:false);
                if(!GmHouseArtifactCodec.ReceiptMatchesRun(receipt,current))
                    throw new InvalidDataException("terminal receipt does not match the allocated run");
                if(terminalCheckpoint==null||terminalCheckpoint.lastCheckpoint!="ending"||
                    terminalCheckpoint.houseRunId!=current.Identity.RunId)
                    throw new InvalidDataException("terminal checkpoint does not bind the ending and House run");
                if(current.Stage==GmHouseRunStage.Prepared||current.Stage==GmHouseRunStage.Acknowledged)
                {
                    if(current.PreparedReceipt.ReceiptId!=receipt.ReceiptId||
                        !current.PreparedReceipt.PayloadHash.SequenceEqual(receipt.PayloadHash))
                        throw new InvalidDataException("prepared run contains another terminal receipt");
                    prepared=current; return Success(out error);
                }
                if(current.Stage!=GmHouseRunStage.Active) throw new InvalidOperationException("run is not active");
                prepared=CopyRun(current); prepared.Generation++; prepared.PreviousGenerationHash=current.GenerationHash;
                prepared.Stage=GmHouseRunStage.Prepared; prepared.PreparedReceipt=receipt;
                prepared.TerminalCheckpoint=CloneCheckpoint(terminalCheckpoint);
                WriteRunUnlocked(prepared,CurrentRunCommitHash(current.Identity.RunId));
                prepared=LoadRunUnlocked(current.Identity.RunId);
            }
            return Success(out error);
        }
        catch(Exception ex){ prepared=null; return Failure(ex,out error); }
    }

    internal bool TryAcknowledgeRun(GmHouseRunGeneration source,out GmHouseRunGeneration acknowledged,
        out string error)
    {
        acknowledged=null;
        try
        {
            using(fileSystem.AcquireExclusiveLease(leasePath))
            {
                LoadAllUnlocked(); GmHouseRunGeneration current=LoadRunUnlocked(source.Identity.RunId);
                if(current.Stage==GmHouseRunStage.Acknowledged)
                { acknowledged=current; return Success(out error); }
                if(current.Stage!=GmHouseRunStage.Prepared) throw new InvalidOperationException("run receipt is not prepared");
                GmHouseTerminalReceipt applied=currentProfile.Receipts.FirstOrDefault(r=>r.ReceiptId==current.PreparedReceipt.ReceiptId);
                if(applied==null||!applied.PayloadHash.SequenceEqual(current.PreparedReceipt.PayloadHash))
                    throw new InvalidOperationException("profile manifest is not durable for prepared receipt");
                acknowledged=CopyRun(current); acknowledged.Generation++; acknowledged.PreviousGenerationHash=current.GenerationHash;
                acknowledged.Stage=GmHouseRunStage.Acknowledged;
                acknowledged.AcknowledgedReceiptId=current.PreparedReceipt.ReceiptId;
                acknowledged.AcknowledgedProfileGeneration=currentProfile.Generation;
                acknowledged.AcknowledgedProfileGenerationHash=currentProfile.GenerationHash;
                acknowledged.AcknowledgedProfileCommitHash=CurrentCommitHash(
                    ProfileCommitDirectory,"profile");
                WriteRunUnlocked(acknowledged,CurrentRunCommitHash(current.Identity.RunId));
                acknowledged=LoadRunUnlocked(current.Identity.RunId);
            }
            return Success(out error);
        }
        catch(Exception ex){ acknowledged=null; return Failure(ex,out error); }
    }

    internal bool TryLoadRun(string runId,out GmHouseRunGeneration run,out string error)
    {
        run=null;
        try
        {
            using(fileSystem.AcquireExclusiveLease(leasePath)) { LoadAllUnlocked(); run=LoadRunUnlocked(runId); }
            return Success(out error);
        }
        catch(Exception ex){ return Failure(ex,out error); }
    }

    internal bool TryListCurrentEpochRuns(out IReadOnlyList<GmHouseRunGeneration> runs,
        out string error)
    {
        runs=Array.Empty<GmHouseRunGeneration>();
        try
        {
            using(fileSystem.AcquireExclusiveLease(leasePath))
            {
                LoadAllUnlocked();
                if(currentRoot.ResetPending) CompletePendingResetUnlocked();
                runs=CurrentEpochRunsUnlocked().Select(CopyRun).ToArray();
            }
            return Success(out error);
        }
        catch(Exception ex){ return Failure(ex,out error); }
    }

    public bool TryAbandonRun(string runId,out string error)
    {
        try
        {
            using(fileSystem.AcquireExclusiveLease(leasePath))
            {
                LoadAllUnlocked(); GmHouseRunGeneration current=LoadRunUnlocked(runId);
                if(current.Stage==GmHouseRunStage.Prepared)
                    throw new InvalidOperationException("prepared terminal receipt cannot be abandoned");
                if(current.Stage==GmHouseRunStage.Acknowledged)
                    throw new InvalidOperationException("acknowledged run cannot be abandoned");
                if(current.Stage==GmHouseRunStage.Abandoned) return Success(out error);
                var abandoned=CopyRun(current); abandoned.Generation++; abandoned.PreviousGenerationHash=current.GenerationHash;
                abandoned.Stage=GmHouseRunStage.Abandoned;
                WriteRunUnlocked(abandoned,CurrentRunCommitHash(runId));
            }
            return Success(out error);
        }
        catch(Exception ex){ return Failure(ex,out error); }
    }

    public bool TryResetHouseMemory(bool requireNoActiveRun,bool explicitCombinedScope,
        out GmHouseProfileGeneration profile,out string error)
    {
        profile=null;
        try
        {
            using(fileSystem.AcquireExclusiveLease(leasePath))
            {
                LoadAllUnlocked();
                if(currentRoot.ResetPending)
                {
                    CompletePendingResetUnlocked(); profile=currentProfile;
                    return Success(out error);
                }
                bool active=CurrentEpochRunsUnlocked().Any(run=>run.Stage==GmHouseRunStage.Active||run.Stage==GmHouseRunStage.Prepared);
                if(active&&(requireNoActiveRun||!explicitCombinedScope))
                    throw new InvalidOperationException("active run requires explicit combined-scope reset confirmation");
                var pending=CopyRoot(currentRoot); pending.Generation++; pending.PreviousGenerationHash=currentRoot.GenerationHash;
                pending.Epoch++; pending.ResetPending=true;
                WriteRootUnlocked(pending,CurrentCommitHash(RootCommitDirectory,"root")); currentRoot=pending;
                CompletePendingResetUnlocked(); profile=currentProfile;
            }
            return Success(out error);
        }
        catch(Exception ex)
        {
            try { using(fileSystem.AcquireExclusiveLease(leasePath)) { currentRoot=LoadRootUnlocked(); } }
            catch { }
            profile=currentProfile; return Failure(ex,out error);
        }
    }

    public bool TryResumePendingReset(out GmHouseProfileGeneration profile,out string error)
    {
        profile=null;
        try
        {
            using(fileSystem.AcquireExclusiveLease(leasePath))
            {
                currentRoot=LoadRootUnlocked(); currentProfile=LoadProfileUnlocked();
                if(currentRoot.ResetPending) CompletePendingResetUnlocked();
                else if(currentProfile.Epoch!=currentRoot.Epoch)
                    throw new InvalidDataException("stable root and profile epoch differ");
                profile=currentProfile;
            }
            return Success(out error);
        }
        catch(Exception ex){ return Failure(ex,out error); }
    }

    void CompletePendingResetUnlocked()
    {
        if(!currentRoot.ResetPending) return;
        GmHouseProfileGeneration previous=currentProfile??LoadProfileUnlocked();
        if(previous.Epoch!=currentRoot.Epoch)
        {
            var empty=new GmHouseProfileGeneration { LineageId=currentRoot.LineageId,Epoch=currentRoot.Epoch,
                Generation=previous.Generation+1,PreviousGenerationHash=previous.GenerationHash,
                Receipts=Array.Empty<GmHouseTerminalReceipt>() };
            empty.ProfileDigest=GmHouseArtifactCodec.ComputeProfileDigest(empty.LineageId,empty.Epoch,empty.Receipts);
            WriteProfileUnlocked(empty,CurrentCommitHash(ProfileCommitDirectory,"profile"));
            currentProfile=LoadProfileUnlocked();
        }
        var stable=CopyRoot(currentRoot); stable.Generation++; stable.PreviousGenerationHash=currentRoot.GenerationHash;
        stable.ResetPending=false; WriteRootUnlocked(stable,CurrentCommitHash(RootCommitDirectory,"root"));
        currentRoot=LoadRootUnlocked();
    }

    IEnumerable<GmHouseRunGeneration> CurrentEpochRunsUnlocked()
    {
        if(!fileSystem.DirectoryExists(RunDirectory)) yield break;
        foreach(string directory in Directory.GetDirectories(RunDirectory))
        {
            string runId=Path.GetFileName(directory);
            string commits=Path.Combine(directory,"commits");
            // A root allocation can durably commit before its run genesis. An empty directory is
            // therefore a permitted burned ordinal. Once any run commit exists, corruption is not
            // a gap and must inhibit replacement/reset instead of silently skipping the run.
            if(Files(commits,"*.commit").Count==0) continue;
            GmHouseRunAllocation allocation=currentRoot.Allocations.FirstOrDefault(item=>
                item.RunId==runId);
            if(allocation==null)
                throw new InvalidDataException("committed run has no root allocation: "+runId);
            // Reset makes old-epoch runs permanently ineligible. Their bytes no longer govern
            // current replacement/reset availability, while the root commitment still prevents
            // the ordinal or run id from ever being reused.
            if(allocation.Epoch!=currentRoot.Epoch) continue;
            GmHouseRunGeneration run=LoadRunUnlocked(runId);
            if(run.Identity.RunOrdinal!=allocation.RunOrdinal||run.Mode!=allocation.Mode)
                throw new InvalidDataException("committed run differs from root allocation: "+runId);
            yield return run;
        }
    }

    void WriteRootUnlocked(GmHouseRootGeneration root,string previousCommitHash)
    {
        GmHouseArtifactCodec.ValidateRoot(root); byte[] bytes=GmHouseArtifactCodec.EncodeRoot(root);
        string digest=GmHouseBinary.Hex(GmHouseBinary.Sha256(bytes));
        string name=$"root-gen-{root.Generation:D20}-{digest}.bin";
        WriteNewOrVerify(Path.Combine(RootGenerationDirectory,name),bytes,GmHouseDurabilityPoint.RootGeneration);
        byte[] reopened=fileSystem.ReadAllBytes(Path.Combine(RootGenerationDirectory,name));
        GmHouseArtifactCodec.DecodeRoot(reopened);
        root.GenerationHash=GmHouseBinary.Hex(GmHouseBinary.Sha256(reopened));
        WriteCommitUnlocked("root",root.Generation,root.LineageId,root.Epoch,name,root.GenerationHash,
            previousCommitHash,RootCommitDirectory,GmHouseDurabilityPoint.RootCommitRecord);
    }

    void WriteProfileUnlocked(GmHouseProfileGeneration profile,string previousCommitHash)
    {
        GmHouseArtifactCodec.ValidateProfile(profile); byte[] bytes=GmHouseArtifactCodec.EncodeProfile(profile);
        string digest=GmHouseBinary.Hex(GmHouseBinary.Sha256(bytes));
        string name=$"profile-gen-{profile.Generation:D20}-{digest}.bin";
        WriteNewOrVerify(Path.Combine(ProfileGenerationDirectory,name),bytes,GmHouseDurabilityPoint.ProfileGeneration);
        byte[] reopened=fileSystem.ReadAllBytes(Path.Combine(ProfileGenerationDirectory,name));
        GmHouseArtifactCodec.DecodeProfile(reopened,LoadReceiptBlobUnlocked);
        profile.GenerationHash=GmHouseBinary.Hex(GmHouseBinary.Sha256(reopened));
        WriteCommitUnlocked("profile",profile.Generation,profile.LineageId,profile.Epoch,name,profile.GenerationHash,
            previousCommitHash,ProfileCommitDirectory,GmHouseDurabilityPoint.ProfileCommitRecord);
    }

    void WriteRunUnlocked(GmHouseRunGeneration run,string previousCommitHash)
    {
        string root=Path.Combine(RunDirectory,run.Identity.RunId); string generations=Path.Combine(root,"generations");
        string commits=Path.Combine(root,"commits"); fileSystem.CreateDirectory(generations);fileSystem.CreateDirectory(commits);
        byte[] bytes=GmHouseArtifactCodec.EncodeRun(run);
        string digest=GmHouseBinary.Hex(GmHouseBinary.Sha256(bytes));
        string name=$"run-gen-{run.Generation:D20}-{digest}.bin";
        WriteNewOrVerify(Path.Combine(generations,name),bytes,GmHouseDurabilityPoint.RunGeneration);
        byte[] reopened=fileSystem.ReadAllBytes(Path.Combine(generations,name));
        GmHouseArtifactCodec.DecodeRun(reopened);
        run.GenerationHash=GmHouseBinary.Hex(GmHouseBinary.Sha256(reopened));
        WriteCommitUnlocked("run",run.Generation,run.Identity.LineageId,run.Identity.Epoch,name,run.GenerationHash,
            previousCommitHash,commits,GmHouseDurabilityPoint.RunCommitRecord);
    }

    GmHouseRunGeneration LoadRunUnlocked(string runId)
    {
        string root=Path.Combine(RunDirectory,runId); string commits=Path.Combine(root,"commits");
        GmHouseCommitRecord commit=LoadCommitHead(commits,"run");
        byte[] bytes=fileSystem.ReadAllBytes(Path.Combine(root,"generations",commit.GenerationFile));
        string hash=GmHouseBinary.Hex(GmHouseBinary.Sha256(bytes));
        if(hash!=commit.GenerationHash) throw new InvalidDataException("run generation checksum invalid");
        GmHouseRunGeneration run=GmHouseArtifactCodec.DecodeRun(bytes);
        if(run.Identity.RunId!=runId||run.Generation!=commit.Sequence||run.Identity.LineageId!=commit.LineageId||
            run.Identity.Epoch!=commit.Epoch) throw new InvalidDataException("run commit does not bind generation");
        if(run.Stage==GmHouseRunStage.Acknowledged)
        {
            string proofPath=Path.Combine(ProfileCommitDirectory,
                $"profile-commit-{run.AcknowledgedProfileGeneration:D20}.commit");
            if(!fileSystem.FileExists(proofPath))
                throw new InvalidDataException("acknowledged profile commit proof is missing");
            GmHouseCommitRecord proof=GmHouseArtifactCodec.DecodeCommit(fileSystem.ReadAllBytes(proofPath));
            if(proof.GenerationHash!=run.AcknowledgedProfileGenerationHash||
                proof.CommitHash!=run.AcknowledgedProfileCommitHash||proof.LineageId!=run.Identity.LineageId||
                proof.Epoch!=run.Identity.Epoch)
                throw new InvalidDataException("acknowledged profile commit proof is invalid");
            string proofGenerationPath=Path.Combine(ProfileGenerationDirectory,proof.GenerationFile);
            if(!fileSystem.FileExists(proofGenerationPath))
                throw new InvalidDataException("acknowledged profile generation proof is missing");
            GmHouseProfileGeneration provedProfile=GmHouseArtifactCodec.DecodeProfile(
                fileSystem.ReadAllBytes(proofGenerationPath),LoadReceiptBlobUnlocked);
            GmHouseTerminalReceipt provedReceipt=provedProfile.Receipts.FirstOrDefault(item=>
                item.ReceiptId==run.AcknowledgedReceiptId);
            if(provedReceipt==null||!provedReceipt.PayloadHash.SequenceEqual(
                    run.PreparedReceipt.PayloadHash)||
                !provedReceipt.CanonicalPayloadBytes().SequenceEqual(
                    run.PreparedReceipt.CanonicalPayloadBytes()))
                throw new InvalidDataException("acknowledged profile generation lacks the exact receipt");
        }
        run.GenerationHash=hash; return run;
    }

    void WriteCommitUnlocked(string kind,long sequence,string lineage,long epoch,string generationFile,
        string generationHash,string previous,string directory,GmHouseDurabilityPoint point)
    {
        var record=new GmHouseCommitRecord { Kind=kind,Sequence=sequence,LineageId=lineage,Epoch=epoch,
            GenerationFile=generationFile,GenerationHash=generationHash,PreviousCommitHash=previous??string.Empty };
        record.CommitHash=GmHouseBinary.Hex(GmHouseBinary.Sha256(GmHouseArtifactCodec.EncodeCommit(record,false)));
        string path=Path.Combine(directory,$"{kind}-commit-{sequence:D20}.commit");
        WriteNewOrVerify(path,GmHouseArtifactCodec.EncodeCommit(record,true),point);
    }

    void WriteNewOrVerify(string path,byte[] bytes,GmHouseDurabilityPoint point)
    {
        if(fileSystem.FileExists(path))
        {
            if(!fileSystem.ReadAllBytes(path).SequenceEqual(bytes))
                throw new InvalidDataException("orphan immutable House artifact conflicts with retry: "+path);
            return;
        }
        fileSystem.WriteNewDurable(path,bytes,point);
    }

    string CurrentCommitHash(string directory,string kind) => Files(directory,"*.commit").Count==0
        ? string.Empty : LoadCommitHead(directory,kind).CommitHash;
    string CurrentRunCommitHash(string runId) => CurrentCommitHash(
        Path.Combine(RunDirectory,runId,"commits"),"run");
    IReadOnlyList<string> Files(string directory,string pattern) =>
        fileSystem.GetFiles(directory,pattern).OrderBy(path=>path,StringComparer.Ordinal).ToArray();

    static GmHouseRootGeneration CopyRoot(GmHouseRootGeneration root) => new GmHouseRootGeneration
    { SchemaVersion=root.SchemaVersion,LineageId=root.LineageId,Epoch=root.Epoch,Generation=root.Generation,
      NextRunOrdinal=root.NextRunOrdinal,ResetPending=root.ResetPending,
      PreviousGenerationHash=root.PreviousGenerationHash,GenerationHash=root.GenerationHash,
      Allocations=root.Allocations.Select(CopyAllocation).ToArray() };
    static GmHouseRunAllocation CopyAllocation(GmHouseRunAllocation a) => new GmHouseRunAllocation
    { Epoch=a.Epoch,RunOrdinal=a.RunOrdinal,RunId=a.RunId,Mode=a.Mode };
    static GmHouseRunGeneration CopyRun(GmHouseRunGeneration run) => new GmHouseRunGeneration
    { SchemaVersion=run.SchemaVersion,Generation=run.Generation,PreviousGenerationHash=run.PreviousGenerationHash,
      GenerationHash=run.GenerationHash,Identity=run.Identity,Mode=run.Mode,Seed=run.Seed,Stage=run.Stage,
      FrozenPackage=run.FrozenPackage.DeepCopy(),PackageBinding=GmHouseBinary.DecodeBinding(run.PackageBinding.CanonicalBytes),
      PreparedReceipt=run.PreparedReceipt,TerminalCheckpoint=run.TerminalCheckpoint,
      AcknowledgedReceiptId=run.AcknowledgedReceiptId,
      AcknowledgedProfileGeneration=run.AcknowledgedProfileGeneration,
      AcknowledgedProfileGenerationHash=run.AcknowledgedProfileGenerationHash,
      AcknowledgedProfileCommitHash=run.AcknowledgedProfileCommitHash };
    static GmSaveData CloneCheckpoint(GmSaveData checkpoint) => checkpoint==null?null:
        UnityEngine.JsonUtility.FromJson<GmSaveData>(UnityEngine.JsonUtility.ToJson(checkpoint));

    bool Success(out string error) { lastError=error=string.Empty; return true; }
    bool Failure(Exception ex,out string error) { lastError=error=ex.Message; return false; }
}

public sealed class GmHouseTerminalProtocol
{
    readonly GmHouseMemoryStore store;
    public GmHouseTerminalFault FaultAfter { get; set; }
    public GmHouseTerminalProtocol(GmHouseMemoryStore store) =>
        this.store=store??throw new ArgumentNullException(nameof(store));

    public bool TryComplete(GmHouseRunGeneration run,GmEndingType ending,
        GmParlorBehaviorAccumulator behavior,GmSaveData terminalCheckpoint,
        out GmHouseRunGeneration result,out string error)
    {
        result=null;
        if(!store.TryCreateReceipt(run,ending,behavior,out GmHouseTerminalReceipt receipt,out error)) return false;
        if(!store.TryCommitPreparedRun(run,receipt,terminalCheckpoint,
            out GmHouseRunGeneration prepared,out error)) return false;
        if(FaultAfter==GmHouseTerminalFault.AfterPreparedRunCommit) { error="injected crash after prepared run commit"; return false; }
        if(!store.TryApplyReceipt(receipt,store.CurrentProfile.Cas,out _,out error)) return false;
        if(FaultAfter==GmHouseTerminalFault.AfterProfileCommit) { error="injected crash after profile commit"; return false; }
        if(!store.TryAcknowledgeRun(prepared,out result,out error)) return false;
        if(FaultAfter==GmHouseTerminalFault.AfterAcknowledgedRunCommit)
        { error="injected crash after acknowledged run commit"; return false; }
        return true;
    }

    public bool TryRecover(string runId,out GmHouseRunGeneration result,out string error)
    {
        result=null;
        if(!store.TryLoadRun(runId,out GmHouseRunGeneration run,out error)) return false;
        if(run.Stage==GmHouseRunStage.Acknowledged) { result=run; return true; }
        if(run.Stage!=GmHouseRunStage.Prepared)
        { error="run has no prepared terminal receipt"; return false; }
        if(!store.TryApplyReceipt(run.PreparedReceipt,store.CurrentProfile.Cas,out _,out error)) return false;
        return store.TryAcknowledgeRun(run,out result,out error);
    }
}

public static class GmHousePersistenceCoordinator
{
    static string directoryOverride;
    static IGmHouseFileSystem fileSystemOverride;
    static bool configuredForTests;
    static GmHouseMemoryStore store;
    static GmHouseRunGeneration activeRun;
    static string lastError = string.Empty;

    public static GmHouseRunGeneration ActiveRun => activeRun;
    public static GmHouseProfileGeneration Profile => store?.CurrentProfile;
    public static string LastError => lastError;
    public static bool IsRecollectionActive => activeRun?.Mode==GmParlorAdaptiveMode.Recollection;
    // Unity Test Framework runs the editor in batch mode against the developer's real
    // persistentDataPath. Focused persistence tests opt in with ConfigureForTests; an ordinary
    // batch PlayMode run must never create or learn from the developer's House profile.
    public static bool IsEnabled => configuredForTests ||
        (UnityEngine.Application.isPlaying && !UnityEngine.Application.isBatchMode);

    static string DefaultDirectory => Path.Combine(UnityEngine.Application.persistentDataPath,
        "the_games_master_house");

    public static void ConfigureForTests(string directory,IGmHouseFileSystem fileSystem=null)
    {
        directoryOverride=directory??throw new ArgumentNullException(nameof(directory));
        fileSystemOverride=fileSystem; configuredForTests=true; store=null;activeRun=null;lastError=string.Empty;
    }

    public static void ResetForTests()
    { directoryOverride=null;fileSystemOverride=null;configuredForTests=false;store=null;activeRun=null;lastError=string.Empty; }
    public static void ForgetActiveForTests() => activeRun=null;

    static GmHouseMemoryStore Store => store??(store=new GmHouseMemoryStore(
        directoryOverride??DefaultDirectory,fileSystemOverride));

    public static bool TryBeginOrdinaryRun(int seed,out string error) =>
        TryBeginCampaign(GmParlorAdaptiveMode.Ordinary,seed,out error);
    public static bool TryBeginMirrorRun(int seed,out string error) =>
        TryBeginCampaign(GmParlorAdaptiveMode.Mirror,seed,out error);

    public static bool TryBeginRecollection(GmParlorAdaptivePackage known,
        GmHousePackageBinding binding,out string error)
    {
        activeRun=null;
        if(!IsEnabled) { error=lastError="House memory is unavailable for Recollection";return false; }
        if(!Store.TryOpenExisting(out _,out error)||
            !Store.TryBeginRecollection(known,binding,out activeRun,out error))
        { lastError=error;activeRun=null;return false; }
        GmRunStore.SetHouseRunPointer(string.Empty);
        lastError=string.Empty;return true;
    }

    public static bool TryGetTitleUnlocks(out bool mirrorUnlocked,
        out IReadOnlyList<GmHouseKnownPackage> recollections,out string error)
    {
        mirrorUnlocked=false;recollections=Array.Empty<GmHouseKnownPackage>();
        if(!IsEnabled) { error=string.Empty;return true; }
        if(!Store.TryOpenOrCreate(out GmHouseProfileGeneration profile,out error))
        { lastError=error;return false; }
        mirrorUnlocked=profile.MirrorUnlocked;
        recollections=profile.KnownMirrorPackages;
        lastError=string.Empty;return true;
    }

    static bool TryBeginCampaign(GmParlorAdaptiveMode mode,int seed,out string error)
    {
        if(!IsEnabled) { error=string.Empty;return true; }
        if(!Store.TryOpenOrCreate(out _,out error)||
            !Store.TryAllocateCampaignRun(mode,seed,out activeRun,out error))
        { lastError=error;activeRun=null;return false; }
        GmRunStore.SetHouseRunPointer(activeRun.Identity.RunId);
        lastError=string.Empty;return true;
    }

    public static bool TryResume(string runId,out string error)
    {
        activeRun=null;
        if(string.IsNullOrWhiteSpace(runId)) { error="House run pointer is missing";lastError=error;return false; }
        if(!Store.TryOpenExisting(out _,out error)||!Store.TryLoadRun(runId,out activeRun,out error))
        { lastError=error;return false; }
        if(activeRun.Identity.LineageId!=Store.CurrentRoot.LineageId||activeRun.Identity.Epoch!=Store.CurrentRoot.Epoch)
        { activeRun=null;error=lastError="House run pointer is from a stale lineage or epoch";return false; }
        GmHouseRunAllocation allocation=Store.CurrentRoot.Allocations.FirstOrDefault(item=>
            item.RunId==activeRun.Identity.RunId&&item.RunOrdinal==activeRun.Identity.RunOrdinal&&
            item.Epoch==activeRun.Identity.Epoch&&item.Mode==activeRun.Mode);
        if(activeRun.Mode!=GmParlorAdaptiveMode.Recollection&&allocation==null)
        { activeRun=null;error=lastError="House run has no root allocation commitment";return false; }
        if(activeRun.Stage==GmHouseRunStage.Abandoned)
        { activeRun=null;error=lastError="House run was abandoned and cannot Continue";return false; }
        if(activeRun.Stage==GmHouseRunStage.Prepared)
        {
            var recovery=new GmHouseTerminalProtocol(Store);
            if(!recovery.TryRecover(runId,out activeRun,out error))
            { lastError=error;return false; }
        }
        if(activeRun.Stage==GmHouseRunStage.Acknowledged&&activeRun.TerminalCheckpoint!=null)
        {
            GmRunStore.LoadFromSaveData(activeRun.TerminalCheckpoint);
            if(!GmSaveSystem.Save())
            { error=lastError="terminal checkpoint recovered but mutable Continue save failed: "+GmSaveSystem.LastError;return false; }
        }
        lastError=string.Empty;return true;
    }

    public static bool TryCompleteEnding(GmEndingType ending,GmParlorBehaviorAccumulator behavior,
        out string error)
    {
        if(activeRun==null) { error=lastError="No active House run";return false; }
        var protocol=new GmHouseTerminalProtocol(Store);
        if(activeRun.Stage==GmHouseRunStage.Prepared)
        {
            if(!protocol.TryRecover(activeRun.Identity.RunId,out activeRun,out error))
            { lastError=error;return false; }
            if(activeRun.PreparedReceipt.Ending!=ending)
            { error=lastError="prepared House ending differs from retry";return false; }
            GmRunStore.LoadFromSaveData(activeRun.TerminalCheckpoint);
            lastError=error=string.Empty;return true;
        }
        if(activeRun.Stage==GmHouseRunStage.Acknowledged)
        {
            if(activeRun.PreparedReceipt.Ending!=ending)
            { error=lastError="acknowledged House ending differs from retry";return false; }
            GmRunStore.LoadFromSaveData(activeRun.TerminalCheckpoint);
            lastError=error=string.Empty;return true;
        }
        GmSaveData checkpoint=GmRunStore.ToSaveData();
        if(checkpoint.lastCheckpoint!="ending")
        { error=lastError="terminal checkpoint has not reached ending";return false; }
        if(!protocol.TryComplete(activeRun,ending,behavior,checkpoint,out activeRun,out error))
        { lastError=error;return false; }
        lastError=string.Empty;return true;
    }

    public static bool TryRecoverTerminal(out string error)
    {
        if(activeRun==null) { error=lastError="No active House run";return false; }
        var protocol=new GmHouseTerminalProtocol(Store);
        if(!protocol.TryRecover(activeRun.Identity.RunId,out activeRun,out error))
        { lastError=error;return false; }
        lastError=string.Empty;return true;
    }

    public static bool TryAbandonActiveRun(out string error)
    {
        if(activeRun==null)
        {
            GmRunStore.SetHouseRunPointer(string.Empty);
            lastError=error=string.Empty;return true;
        }
        if(activeRun.Stage!=GmHouseRunStage.Active)
        { error=lastError="only an active, unterminated House run can be abandoned";return false; }
        if(!Store.TryAbandonRun(activeRun.Identity.RunId,out error))
        { lastError=error;return false; }
        activeRun=null;
        GmRunStore.SetHouseRunPointer(string.Empty);
        lastError=string.Empty;return true;
    }

    public static bool TryRetireActiveRunForReplacement(out string error)
    {
        if(!IsEnabled) { activeRun=null;GmRunStore.SetHouseRunPointer(string.Empty);
            lastError=error=string.Empty;return true; }
        if(!Store.TryOpenOrCreate(out _,out error)||
            !Store.TryListCurrentEpochRuns(out IReadOnlyList<GmHouseRunGeneration> runs,out error))
        { lastError=error;return false; }
        foreach(GmHouseRunGeneration run in runs)
        {
            if(run.Stage==GmHouseRunStage.Active)
            {
                if(!Store.TryAbandonRun(run.Identity.RunId,out error))
                { lastError=error;return false; }
            }
            else if(run.Stage==GmHouseRunStage.Prepared)
            {
                var protocol=new GmHouseTerminalProtocol(Store);
                if(!protocol.TryRecover(run.Identity.RunId,out _,out error))
                { lastError=error;return false; }
            }
        }
        activeRun=null;GmRunStore.SetHouseRunPointer(string.Empty);
        lastError=error=string.Empty;return true;
    }

    public static void EndRecollectionShell()
    {
        if(activeRun?.Mode!=GmParlorAdaptiveMode.Recollection) return;
        activeRun=null;GmRunStore.SetHouseRunPointer(string.Empty);
        if(GmSaveSystem.HasSave())
        {
            if(!GmSaveSystem.Load())
                UnityEngine.Debug.LogError("[GmHouse] preserved campaign failed to restore after Recollection: "+
                    GmSaveSystem.LastError);
        }
        else GmRunStore.BeginNewRun();
    }

    public static bool TryResetHouseMemory(bool explicitCombinedScope,out string error)
    {
        if(!Store.TryOpenOrCreate(out _,out error)||!Store.TryResetHouseMemory(
            requireNoActiveRun:false,explicitCombinedScope:explicitCombinedScope,out _,out error))
        { lastError=error;return false; }
        activeRun=null;GmRunStore.SetHouseRunPointer(string.Empty);
        if(!GmSaveSystem.DeleteSave()) { error=lastError=GmSaveSystem.LastError;return false; }
        lastError=string.Empty;return true;
    }
}
