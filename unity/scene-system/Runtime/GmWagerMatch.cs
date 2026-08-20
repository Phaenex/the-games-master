using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Nyx.GameCraft;

public enum GmWagerMatchPhase
{
    AwaitingDecision,
    AwaitingIntervention,
    Complete
}

[Serializable]
public sealed class GmWagerInterventionReceipt
{
    public int acceptedContractIndex;
    public int sourceContractIndex;
    public int honestValue;
    public int alteredValue;
    public int sovereignsAtAcceptance;
    public string response = string.Empty;

    public GmWagerInterventionReceipt DeepCopy() => new GmWagerInterventionReceipt
    {
        acceptedContractIndex = acceptedContractIndex,
        sourceContractIndex = sourceContractIndex,
        honestValue = honestValue,
        alteredValue = alteredValue,
        sovereignsAtAcceptance = sovereignsAtAcceptance,
        response = response
    };
}

[Serializable]
public sealed class GmWagerMatchSnapshot
{
    public int schemaVersion = 1;
    public ulong seed;
    public int[] contractValues = Array.Empty<int>();
    public bool[] contractRead = Array.Empty<bool>();
    public int currentContractIndex;
    public int sovereigns;
    public int startingSovereigns;
    public int acceptedContractIndex = -1;
    public GmWagerMatchPhase phase;
    public GmWagerMatchResult result;
    public bool hasResult;
    public string[] actionJournal = Array.Empty<string>();
    public GmWagerInterventionReceipt interventionReceipt;
    public string stateFingerprint = string.Empty;
    public GameSessionSnapshot session;

    public GmWagerMatchSnapshot DeepCopy() => new GmWagerMatchSnapshot
    {
        schemaVersion = schemaVersion,
        seed = seed,
        contractValues = (int[])(contractValues ?? Array.Empty<int>()).Clone(),
        contractRead = (bool[])(contractRead ?? Array.Empty<bool>()).Clone(),
        currentContractIndex = currentContractIndex,
        sovereigns = sovereigns,
        startingSovereigns = startingSovereigns,
        acceptedContractIndex = acceptedContractIndex,
        phase = phase,
        result = result,
        hasResult = hasResult,
        actionJournal = (string[])(actionJournal ?? Array.Empty<string>()).Clone(),
        interventionReceipt = interventionReceipt?.DeepCopy(),
        stateFingerprint = stateFingerprint,
        session = session?.DeepCopy()
    };
}

public sealed class GmWagerMatch
{
    const string ChallengeResponse = "intervention:challenge";
    const string ProceedResponse = "intervention:proceed";

    readonly ulong seed;
    readonly int[] contractValues;
    readonly bool[] contractRead = new bool[GmWagerRules.ContractCount];
    readonly List<string> actionJournal = new List<string>();
    DeterministicGameSession session;
    int currentContractIndex;
    int sovereigns;
    readonly int startingSovereigns;
    int acceptedContractIndex = -1;
    GmWagerMatchPhase phase;
    GmWagerMatchResult result;
    bool hasResult;
    GmWagerInterventionReceipt interventionReceipt;

    public GmWagerMatch(ulong seed, int startingSovereigns)
    {
        if (startingSovereigns < 0)
            throw new ArgumentOutOfRangeException(nameof(startingSovereigns));
        this.seed = seed;
        this.startingSovereigns = startingSovereigns;
        sovereigns = startingSovereigns;
        contractValues = GmWagerRules.ValuesForSeed(seed);
        phase = GmWagerMatchPhase.AwaitingDecision;
        session = new DeterministicGameSession(GmWagerRules.GameId, seed, ComputeFingerprint());
    }

    public ulong Seed => seed;
    public int CurrentContractIndex => currentContractIndex;
    public int Sovereigns => sovereigns;
    public int StartingSovereigns => startingSovereigns;
    public int AcceptedContractIndex => acceptedContractIndex;
    public GmWagerMatchPhase Phase => phase;
    public bool HasResult => hasResult;
    public GmWagerMatchResult Result => hasResult
        ? result
        : throw new InvalidOperationException("the Wager match has not completed");
    public bool CurrentContractIsRead => contractRead[currentContractIndex];
    public bool CanReadCurrentContract => phase == GmWagerMatchPhase.AwaitingDecision &&
        !contractRead[currentContractIndex] && sovereigns >= GmWagerRules.ReadCost[currentContractIndex];
    public bool CanPass => phase == GmWagerMatchPhase.AwaitingDecision &&
        currentContractIndex < GmWagerRules.ContractCount - 1;
    public string[] ActionJournal => actionJournal.ToArray();
    public GmWagerInterventionReceipt InterventionReceipt => interventionReceipt?.DeepCopy();
    public string StateFingerprint => ComputeFingerprint();

    /// <summary>Value at a contract slot as currently held (post-swap if one occurred). Never
    /// reveals a value the player has not read, except the accepted contract's post-resolution
    /// value, which is always public by then.</summary>
    public int ContractValue(int contractIndex)
    {
        GmWagerRules.ValidateContractIndex(contractIndex);
        return contractValues[contractIndex];
    }

    public bool IsContractRead(int contractIndex)
    {
        GmWagerRules.ValidateContractIndex(contractIndex);
        return contractRead[contractIndex];
    }

    public bool TryGetResult(out GmWagerMatchResult matchResult)
    {
        matchResult = result;
        return hasResult;
    }

    public bool TryReadCurrentContract(out string error)
    {
        if (phase != GmWagerMatchPhase.AwaitingDecision)
            return Refuse("the Wager match is not awaiting a decision", out error);
        if (contractRead[currentContractIndex])
            return Refuse("this contract has already been read", out error);
        int cost = GmWagerRules.ReadCost[currentContractIndex];
        if (sovereigns < cost)
            return Refuse("not enough sovereigns to read this contract", out error);

        string before = ComputeFingerprint();
        sovereigns -= cost;
        contractRead[currentContractIndex] = true;
        actionJournal.Add($"read:{currentContractIndex}");
        string after = ComputeFingerprint();
        if (!session.TryRecordDecision(actionJournal.Count - 1, $"read:{currentContractIndex}",
                before, after, out error))
            throw new InvalidOperationException($"Wager decision could not be recorded: {error}");
        error = string.Empty;
        return true;
    }

    public bool TryPass(out string error)
    {
        if (phase != GmWagerMatchPhase.AwaitingDecision)
            return Refuse("the Wager match is not awaiting a decision", out error);
        if (currentContractIndex >= GmWagerRules.ContractCount - 1)
            return Refuse("the final contract cannot be passed", out error);

        string before = ComputeFingerprint();
        currentContractIndex++;
        actionJournal.Add("pass");
        string after = ComputeFingerprint();
        if (!session.TryRecordDecision(actionJournal.Count - 1, "pass", before, after, out error))
            throw new InvalidOperationException($"Wager decision could not be recorded: {error}");
        error = string.Empty;
        return true;
    }

    public bool TryAccept(out string error)
    {
        if (phase != GmWagerMatchPhase.AwaitingDecision)
            return Refuse("the Wager match is not awaiting a decision", out error);

        string before = ComputeFingerprint();
        acceptedContractIndex = currentContractIndex;
        actionJournal.Add("accept");
        int wealth = sovereigns + contractValues[acceptedContractIndex];
        GmWagerMatchResult rawResult = GmWagerRules.ResolveResult(wealth);
        string honestAfter = ComputeFingerprint();

        if (rawResult != GmWagerMatchResult.PlayerWin || !TryFindSwapSource(out int sourceIndex))
        {
            Complete(rawResult);
            if (!session.TryRecordDecision(actionJournal.Count - 1, "accept", before, honestAfter, out error))
                throw new InvalidOperationException($"Wager decision could not be recorded: {error}");
            if (!session.TryComplete(ToTerminalResult(result), out error))
                throw new InvalidOperationException($"Wager result could not be recorded: {error}");
            error = string.Empty;
            return true;
        }

        // Record the decision with the HONEST post-accept fingerprint first (mirroring
        // GmStudyMatch.OpenIntervention), then perform the swap and open the intervention with a
        // fingerprint computed AFTER it -- TryOpenIntervention requires the two to differ, and
        // they only can if the swap happens between the two ComputeFingerprint() calls.
        if (!session.TryRecordDecision(actionJournal.Count - 1, "accept", before, honestAfter, out error))
            throw new InvalidOperationException($"Wager decision could not be recorded: {error}");

        int honestValue = contractValues[acceptedContractIndex];
        int alteredValue = contractValues[sourceIndex];
        interventionReceipt = new GmWagerInterventionReceipt
        {
            acceptedContractIndex = acceptedContractIndex,
            sourceContractIndex = sourceIndex,
            honestValue = honestValue,
            alteredValue = alteredValue,
            sovereignsAtAcceptance = sovereigns
        };
        contractValues[acceptedContractIndex] = alteredValue;
        contractValues[sourceIndex] = honestValue;
        phase = GmWagerMatchPhase.AwaitingIntervention;
        string alteredAfter = ComputeFingerprint();
        if (!session.TryOpenIntervention(GmWagerRules.InterventionId, alteredAfter, out error))
            throw new InvalidOperationException($"Wager intervention could not be recorded: {error}");
        error = string.Empty;
        return true;
    }

    public bool TryResolveIntervention(bool challenge, out string error)
    {
        if (phase != GmWagerMatchPhase.AwaitingIntervention || interventionReceipt == null)
            return Refuse("no Wager intervention is waiting", out error);
        if (!session.TryResolveIntervention(challenge, out string selectedFingerprint, out error))
            return false;

        actionJournal.Add(challenge ? ChallengeResponse : ProceedResponse);
        interventionReceipt.response = challenge ? "challenge" : "proceed";
        if (challenge)
        {
            contractValues[interventionReceipt.acceptedContractIndex] = interventionReceipt.honestValue;
            contractValues[interventionReceipt.sourceContractIndex] = interventionReceipt.alteredValue;
            Complete(GmWagerMatchResult.PlayerWin);
        }
        else
        {
            int wealth = sovereigns + contractValues[interventionReceipt.acceptedContractIndex];
            Complete(GmWagerRules.ResolveResult(wealth));
        }

        if (ComputeFingerprint() != selectedFingerprint)
            throw new InvalidOperationException("Wager intervention response disagrees with its authenticated branch");
        if (!session.TryComplete(ToTerminalResult(result), out error))
            throw new InvalidOperationException($"Wager result could not be recorded: {error}");
        error = string.Empty;
        return true;
    }

    // Canon: "Aldric may swap its clause with the lowest-valued other clause whose substitution
    // changes the result to tie or loss. Contract ID breaks a tie." The tie-break branch below is
    // provably unreachable with three always-distinct values (-2, 1, 3) -- two eligible candidates
    // can never share a value -- but it is kept, favoring the lower contract ID, because the canon
    // states the rule explicitly rather than leaving it as an unspecified edge case.
    bool TryFindSwapSource(out int sourceIndex)
    {
        sourceIndex = -1;
        int bestValue = int.MaxValue;
        for (int candidate = 0; candidate < GmWagerRules.ContractCount; candidate++)
        {
            if (candidate == acceptedContractIndex) continue;
            int candidateWealth = sovereigns + contractValues[candidate];
            if (GmWagerRules.ResolveResult(candidateWealth) == GmWagerMatchResult.PlayerWin) continue;
            if (contractValues[candidate] > bestValue) continue;
            if (contractValues[candidate] == bestValue && candidate > sourceIndex) continue;
            bestValue = contractValues[candidate];
            sourceIndex = candidate;
        }
        return sourceIndex >= 0;
    }

    void Complete(GmWagerMatchResult terminalResult)
    {
        result = terminalResult;
        hasResult = true;
        phase = GmWagerMatchPhase.Complete;
    }

    public GmWagerMatchSnapshot ExportSnapshot() => new GmWagerMatchSnapshot
    {
        seed = seed,
        contractValues = (int[])contractValues.Clone(),
        contractRead = (bool[])contractRead.Clone(),
        currentContractIndex = currentContractIndex,
        sovereigns = sovereigns,
        startingSovereigns = startingSovereigns,
        acceptedContractIndex = acceptedContractIndex,
        phase = phase,
        result = result,
        hasResult = hasResult,
        actionJournal = actionJournal.ToArray(),
        interventionReceipt = interventionReceipt?.DeepCopy(),
        stateFingerprint = ComputeFingerprint(),
        session = session.ExportSnapshot()
    };

    public static bool TryRestore(GmWagerMatchSnapshot snapshot,
        out GmWagerMatch match, out string error)
    {
        match = null;
        if (snapshot == null) return Refuse("snapshot is missing", out error);
        if (snapshot.schemaVersion != 1) return Refuse("snapshot schema is unsupported", out error);
        if (snapshot.actionJournal == null || snapshot.contractValues == null ||
            snapshot.contractRead == null || snapshot.session == null)
            return Refuse("snapshot state is missing", out error);
        if (snapshot.startingSovereigns < 0)
            return Refuse("snapshot starting sovereigns is invalid", out error);
        if (!Enum.IsDefined(typeof(GmWagerMatchPhase), snapshot.phase) ||
            !Enum.IsDefined(typeof(GmWagerMatchResult), snapshot.result))
            return Refuse("snapshot enum state is invalid", out error);

        GmWagerMatch replay = new GmWagerMatch(snapshot.seed, snapshot.startingSovereigns);
        foreach (string action in snapshot.actionJournal)
        {
            bool applied;
            if (action == ChallengeResponse) applied = replay.TryResolveIntervention(true, out _);
            else if (action == ProceedResponse) applied = replay.TryResolveIntervention(false, out _);
            else if (action == "pass") applied = replay.TryPass(out _);
            else if (action == "accept") applied = replay.TryAccept(out _);
            else if (action.StartsWith("read:", StringComparison.Ordinal)) applied = replay.TryReadCurrentContract(out _);
            else return Refuse("snapshot action journal contains an unrecognized action", out error);
            if (!applied) return Refuse("snapshot action journal cannot be replayed", out error);
        }

        GmWagerMatchSnapshot canonical = replay.ExportSnapshot();
        if (!SnapshotsEqual(canonical, snapshot))
            return Refuse("snapshot does not match deterministic Wager replay", out error);
        match = replay;
        error = string.Empty;
        return true;
    }

    string ComputeFingerprint()
    {
        // The two UNaccepted contracts' values are deliberately excluded: they are authenticated
        // instead by TryRestore's full deterministic replay + snapshot compare, exactly as
        // GmStudyMatch excludes currentFen/phase from its own decision fingerprint. The ACCEPTED
        // contract's currently-held value is included, though, because it is the one thing the
        // swap changes that this fingerprint would otherwise be blind to -- without it, the
        // "honest" and "altered" fingerprints computed either side of TryAccept's swap are
        // identical, which trips DeterministicGameSession.TryOpenIntervention's "must produce a
        // different state fingerprint" guard every time.
        var data = new StringBuilder(160);
        data.Append(seed.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(sovereigns).Append('|').Append(currentContractIndex).Append('|')
            .Append(acceptedContractIndex).Append('|')
            .Append(acceptedContractIndex >= 0 ? contractValues[acceptedContractIndex] : 0).Append('|');
        foreach (string action in actionJournal)
            if (action != ChallengeResponse && action != ProceedResponse)
                data.Append(action).Append(';');
        ulong hash = 14695981039346656037UL;
        foreach (byte value in Encoding.UTF8.GetBytes(data.ToString()))
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    static bool SnapshotsEqual(GmWagerMatchSnapshot left, GmWagerMatchSnapshot right)
    {
        if (left.schemaVersion != right.schemaVersion || left.seed != right.seed ||
            left.currentContractIndex != right.currentContractIndex ||
            left.sovereigns != right.sovereigns || left.startingSovereigns != right.startingSovereigns ||
            left.acceptedContractIndex != right.acceptedContractIndex || left.phase != right.phase ||
            left.result != right.result || left.hasResult != right.hasResult ||
            left.stateFingerprint != right.stateFingerprint ||
            !left.contractValues.SequenceEqual(right.contractValues) ||
            !left.contractRead.SequenceEqual(right.contractRead) ||
            !left.actionJournal.SequenceEqual(right.actionJournal) ||
            !ReceiptsEqual(left.interventionReceipt, right.interventionReceipt)) return false;
        return SessionsEqual(left.session, right.session);
    }

    static bool ReceiptsEqual(GmWagerInterventionReceipt left, GmWagerInterventionReceipt right)
    {
        if (left == null || right == null) return left == right;
        return left.acceptedContractIndex == right.acceptedContractIndex &&
            left.sourceContractIndex == right.sourceContractIndex &&
            left.honestValue == right.honestValue && left.alteredValue == right.alteredValue &&
            left.sovereignsAtAcceptance == right.sovereignsAtAcceptance &&
            left.response == right.response;
    }

    static bool SessionsEqual(GameSessionSnapshot left, GameSessionSnapshot right)
    {
        if (left == null || right == null || left.schemaVersion != right.schemaVersion ||
            left.gameId != right.gameId || left.runSeed != right.runSeed ||
            left.initialFingerprint != right.initialFingerprint ||
            left.currentFingerprint != right.currentFingerprint || left.decisionIndex != right.decisionIndex ||
            left.phase != right.phase || left.terminalResult != right.terminalResult ||
            left.actions == null || right.actions == null || left.actions.Length != right.actions.Length)
            return false;
        for (int index = 0; index < left.actions.Length; index++)
        {
            GameActionRecord a = left.actions[index];
            GameActionRecord b = right.actions[index];
            if (a == null || b == null || a.decisionIndex != b.decisionIndex ||
                a.actionId != b.actionId || a.beforeFingerprint != b.beforeFingerprint ||
                a.afterFingerprint != b.afterFingerprint) return false;
        }
        GameInterventionRecord x = left.intervention;
        GameInterventionRecord y = right.intervention;
        if (x == null || y == null) return x == y;
        return x.decisionIndex == y.decisionIndex && x.interventionId == y.interventionId &&
            x.beforeFingerprint == y.beforeFingerprint && x.alteredFingerprint == y.alteredFingerprint &&
            x.resolved == y.resolved && x.challenged == y.challenged &&
            x.resolvedFingerprint == y.resolvedFingerprint;
    }

    static GameTerminalResult ToTerminalResult(GmWagerMatchResult value)
    {
        if (value == GmWagerMatchResult.PlayerWin) return GameTerminalResult.Win;
        if (value == GmWagerMatchResult.AldricWin) return GameTerminalResult.Loss;
        return GameTerminalResult.Tie;
    }

    static bool Refuse(string message, out string error)
    {
        error = message;
        return false;
    }
}
