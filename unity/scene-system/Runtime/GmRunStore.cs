using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Cross-scene run state store. Manages catches, corruption, sanity, and ending metrics across
/// all seven game rooms. Persists in static memory without requiring DontDestroyOnLoad hierarchy drags.
/// </summary>
public static class GmRunStore
{
    public const int MinCorruptionTier = 1;
    public const int MaxCorruptionTier = 5;

    public static event Action OnStateChanged;

    static readonly HashSet<string> cheatsCaught = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static readonly bool[] mirrorShards = new bool[3];
    static GmParlorMatchSnapshot parlorMatch;
    static GmParlorPresentationState parlorPresentation = GmParlorPresentationState.Empty();
    static ulong parlorOutcomeNamespace;
    static ulong parlorAppliedOutcomeSequence;
    static string parlorRestoreError = string.Empty;
    static GmBonesMatchSnapshot bonesMatch;
    static bool bonesMatchPresent;
    static string bonesRestoreError = string.Empty;
    static string houseRunId = string.Empty;

    // A room being complete and a table game being complete are deliberately separate facts.
    // Court is a trial, the Hidden Room is a secret, and the Labyrinth is a chase; counting any of
    // them toward Aldric's seven games would make the run look farther through the night than it is.
    // Both sets are keyed so returning through a room cannot advance the night a second time.
    static readonly HashSet<string> completedRooms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> completedTableGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Everything the player has learned, catches included. Kept apart from cheatsCaught because the
    // true ending counts catches and only catches: a ledger line or a shard is progress, not a catch,
    // and folding the two sets together would inflate the 8-catch threshold with reading material.
    static readonly HashSet<string> discoveredClues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static int CorruptionTier { get; private set; } = MinCorruptionTier;
    public static float Sanity { get; private set; } = 1.0f;
    public static int Defiance { get; private set; } = 0;
    public static int Compliance { get; private set; } = 0;
    public static string CurrentSceneId { get; set; } = "wend-hill-prologue";
    public static string LastCheckpoint { get; set; } = "spawn";

    public static int CheatsCaughtCount => cheatsCaught.Count;
    public static IReadOnlyCollection<string> CheatsCaught => cheatsCaught;
    public static int DiscoveredCluesCount => discoveredClues.Count;
    public static IReadOnlyCollection<string> DiscoveredClues => discoveredClues;
    public static IReadOnlyList<bool> MirrorShards => mirrorShards;
    public static int DefianceCount => Defiance;
    public static int ComplianceCount => Compliance;
    public static int TableGameIndex => completedTableGames.Count;
    public static IReadOnlyCollection<string> CompletedRooms => completedRooms;
    public static bool AllShardsCollected => ShardsCount >= 3;
    public static bool HasParlorMatch => parlorMatch != null;
    public static ulong ParlorOutcomeNamespace => parlorOutcomeNamespace;
    public static ulong ParlorAppliedOutcomeSequence => parlorAppliedOutcomeSequence;
    public static string ParlorRestoreError => parlorRestoreError;
    public static string ParlorPresentationRestoreError { get; private set; } = string.Empty;
    public static bool HasBonesMatch => bonesMatchPresent;
    public static string BonesRestoreError => bonesRestoreError;
    public static string HouseRunId => houseRunId;

    public static void SetHouseRunPointer(string runId)
    {
        if (!string.IsNullOrEmpty(runId) &&
            (runId.Length != 32 || runId.Any(character =>
                !((character >= '0' && character <= '9') ||
                  (character >= 'a' && character <= 'f')))))
            throw new ArgumentException("House run pointer must be an empty or 32-character hex id",
                nameof(runId));
        houseRunId = runId ?? string.Empty;
    }

    /// <summary>Returns an owned copy so callers cannot mutate the run behind the store.</summary>
    public static GmParlorMatchSnapshot GetParlorMatchSnapshot() => parlorMatch?.DeepCopy();

    public static GmBonesMatchSnapshot GetBonesMatchSnapshot() => CloneBonesSnapshot(bonesMatch);

    internal static bool TryCreateBonesSaveData(GmBonesMatchSnapshot snapshot,
        out GmSaveData candidate, out string error)
    {
        candidate = null;
        if (!GmBonesMatch.TryRestore(snapshot, out GmBonesMatch restored, out error)) return false;
        GmBonesMatchSnapshot current = restored.ExportSnapshot();
        candidate = ToSaveData();
        candidate.bonesMatch = CloneBonesSnapshot(current);
        candidate.bonesEnvelopeVersion = 1;
        candidate.bonesPayloadPresent = true;
        candidate.bonesTurnEvidencePresent = current.interventionReceipt != null;
        candidate.bonesSessionEventPresent = current.session?.intervention != null;

        bool challenged = current.session != null && current.session.intervention != null &&
            current.session.intervention.resolved && current.session.intervention.challenged;
        if (challenged && !candidate.discoveredClues.Any(value =>
                string.Equals(value, "bones-loaded-six-intervention", StringComparison.OrdinalIgnoreCase)))
            candidate.discoveredClues.Add("bones-loaded-six-intervention");

        if (restored.HasResult)
        {
            if (!candidate.completedRooms.Any(value =>
                    string.Equals(value, "bones", StringComparison.OrdinalIgnoreCase)))
                candidate.completedRooms.Add("bones");

            if (!candidate.completedTableGames.Any(value =>
                    string.Equals(value, "bones", StringComparison.OrdinalIgnoreCase)))
            {
                candidate.completedTableGames.Add("bones");
                if (restored.Result == GmBonesMatchResult.PlayerWin) candidate.defiance += 2;
                else if (restored.Result == GmBonesMatchResult.AldricWin)
                {
                    candidate.compliance += 2;
                    candidate.sanity = Mathf.Clamp01(candidate.sanity - 0.05f);
                }
                else
                {
                    candidate.defiance += 1;
                    candidate.compliance += 1;
                }
            }
        }
        error = string.Empty;
        return true;
    }

    internal static void CommitBonesSaveData(GmSaveData candidate)
    {
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));
        LoadFromSaveData(candidate);
    }

    public static GmParlorPresentationState GetParlorPresentationState() =>
        parlorPresentation?.DeepCopy() ?? GmParlorPresentationState.Empty();

    public static bool TrySetParlorPresentationState(GmParlorPresentationState state,
        out string error)
    {
        if (state == null)
        {
            error = "Parlor presentation state cannot be null";
            return false;
        }
        if (parlorMatch == null)
        {
            error = "Parlor presentation state cannot exist without a match";
            return false;
        }
        GmParlorPresentationState owned = state.DeepCopy();
        if (!owned.TryValidate(out error) ||
            !TryValidatePresentationIdentity(owned, parlorMatch, out error)) return false;
        parlorPresentation = owned;
        ParlorPresentationRestoreError = string.Empty;
        OnStateChanged?.Invoke();
        return true;
    }

    internal static bool TryCreateParlorPresentationSaveData(
        GmParlorPresentationState state, out GmSaveData candidate, out string error)
    {
        candidate = null;
        if (state == null)
        {
            error = "Parlor presentation state cannot be null";
            return false;
        }
        if (parlorMatch == null)
        {
            error = "Parlor presentation state cannot exist without a match";
            return false;
        }
        GmParlorPresentationState owned = state.DeepCopy();
        if (!owned.hasMatchIdentity) owned.BindTo(parlorMatch.seed, parlorOutcomeNamespace);
        if (!owned.TryValidate(out error) ||
            !TryValidatePresentationIdentity(owned, parlorMatch, out error)) return false;
        candidate = ToSaveData();
        candidate.parlorPresentation = owned;
        error = string.Empty;
        return true;
    }

    internal static void CommitParlorPresentationSaveData(GmSaveData candidate)
    {
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));
        LoadFromSaveData(candidate);
    }

    public static bool TrySetParlorMatch(GmParlorMatchSnapshot snapshot, out string error)
    {
        if (!GmParlorMatch.TryRestore(snapshot, out GmParlorMatch restored, out error)) return false;
        GmParlorMatchSnapshot current = restored.ExportSnapshot();
        if (current.outcomePending &&
            current.highestDurableOutcomeSequence < current.outcomeSequence)
        {
            error = "pending outcome has not been marked durable";
            return false;
        }
        parlorMatch = current;
        if (!parlorPresentation.Matches(current.seed, parlorOutcomeNamespace))
        {
            parlorPresentation = GmParlorPresentationState.Empty();
            parlorPresentation.BindTo(current.seed, parlorOutcomeNamespace);
        }
        parlorRestoreError = string.Empty;
        OnStateChanged?.Invoke();
        return true;
    }

    public static bool TryApplyParlorOutcome(ulong sequence, GmParlorOutcome outcome,
        out string error)
    {
        if (sequence <= parlorAppliedOutcomeSequence)
        {
            error = string.Empty;
            return true;
        }
        if (sequence != parlorAppliedOutcomeSequence + 1)
        {
            error = $"outcome sequence {sequence} skipped applied sequence {parlorAppliedOutcomeSequence}";
            return false;
        }

        if (outcome.CatchDelta > 0)
        {
            string catchKey = parlorOutcomeNamespace == 0
                ? $"parlor-outcome-{sequence}"
                : $"parlor-outcome-{parlorOutcomeNamespace}-{sequence}";
            cheatsCaught.Add(catchKey);
        }
        CorruptionTier = Mathf.Clamp(CorruptionTier + outcome.CorruptionDelta,
            MinCorruptionTier, MaxCorruptionTier);
        Sanity = Mathf.Clamp01(Sanity + outcome.SanityDelta / 100f);
        Defiance = Mathf.Max(0, Defiance + outcome.DefianceDelta);
        Compliance = Mathf.Max(0, Compliance + outcome.ComplianceDelta);
        parlorAppliedOutcomeSequence = sequence;
        OnStateChanged?.Invoke();
        error = string.Empty;
        return true;
    }

    internal static void RestoreParlorSnapshotForTransaction(GmParlorMatchSnapshot snapshot)
    {
        parlorMatch = snapshot?.DeepCopy();
        parlorRestoreError = string.Empty;
        OnStateChanged?.Invoke();
    }

    internal static bool TryCreateParlorSaveData(GmParlorMatchSnapshot snapshot,
        bool completeRoom, out GmSaveData candidate, out string error)
    {
        candidate = null;
        if (!GmParlorMatch.TryRestore(snapshot, out GmParlorMatch restored, out error))
            return false;
        GmParlorMatchSnapshot current = restored.ExportSnapshot();
        if (current.outcomePending &&
            current.highestDurableOutcomeSequence < current.outcomeSequence)
        {
            error = "pending outcome has not been marked durable";
            return false;
        }

        candidate = ToSaveData();
        candidate.parlorMatch = current;
        if (candidate.parlorPresentation == null ||
            !candidate.parlorPresentation.Matches(current.seed, parlorOutcomeNamespace))
        {
            candidate.parlorPresentation = GmParlorPresentationState.Empty();
            candidate.parlorPresentation.BindTo(current.seed, parlorOutcomeNamespace);
        }
        if (completeRoom)
        {
            if (!candidate.completedRooms.Contains("parlor"))
                candidate.completedRooms.Add("parlor");
            if (!candidate.completedTableGames.Contains("parlor"))
                candidate.completedTableGames.Add("parlor");
        }
        error = string.Empty;
        return true;
    }

    internal static void CommitParlorSaveData(GmSaveData candidate)
    {
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));
        LoadFromSaveData(candidate);
    }

    internal static bool TryCreateParlorAbandonSaveData(out GmSaveData candidate,
        out string error)
    {
        candidate = null;
        if (parlorOutcomeNamespace == ulong.MaxValue)
        {
            error = "Parlor outcome namespace is exhausted";
            return false;
        }

        candidate = ToSaveData();
        candidate.parlorMatch = null;
        candidate.parlorPresentation = GmParlorPresentationState.Empty();
        candidate.parlorAppliedOutcomeSequence = 0;
        candidate.parlorOutcomeNamespace = parlorOutcomeNamespace + 1;
        error = string.Empty;
        return true;
    }

    internal static void CommitParlorAbandonSaveData(GmSaveData candidate)
    {
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));
        if (candidate.parlorMatch != null || candidate.parlorAppliedOutcomeSequence != 0 ||
            candidate.parlorOutcomeNamespace != parlorOutcomeNamespace + 1)
            throw new ArgumentException("candidate is not the staged Parlor abandon transaction",
                nameof(candidate));
        LoadFromSaveData(candidate);
    }

    /// <summary>
    /// Explicit abandonment only. Leaving the scene keeps a live match, reaching MatchResult keeps
    /// its inspectable result, and a rematch replaces it. BeginNewRun also clears it.
    /// </summary>
    public static void ClearParlorMatch()
    {
        bool changed = parlorMatch != null || parlorPresentation.observedFacts.Count > 0 ||
            parlorPresentation.hasMatchIdentity || ParlorPresentationRestoreError.Length > 0 ||
            parlorRestoreError.Length > 0;
        parlorMatch = null;
        parlorPresentation = GmParlorPresentationState.Empty();
        ParlorPresentationRestoreError = string.Empty;
        parlorRestoreError = string.Empty;
        if (changed) OnStateChanged?.Invoke();
    }

    public static int ShardsCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < mirrorShards.Length; i++)
                if (mirrorShards[i]) count++;
            return count;
        }
    }

    public static bool HasShard(int shardIndex)
    {
        if (shardIndex < 0 || shardIndex >= mirrorShards.Length) return false;
        return mirrorShards[shardIndex];
    }

    public static bool CollectShard(int shardIndex)
    {
        if (shardIndex < 0 || shardIndex >= mirrorShards.Length) return false;
        if (mirrorShards[shardIndex]) return false;
        mirrorShards[shardIndex] = true;
        OnStateChanged?.Invoke();
        return true;
    }

    public static bool RecordCatch(string clueId)
    {
        if (string.IsNullOrWhiteSpace(clueId)) return false;
        bool added = cheatsCaught.Add(clueId.Trim());
        if (added) OnStateChanged?.Invoke();
        return added;
    }

    public static bool HasCatch(string clueId)
    {
        if (string.IsNullOrWhiteSpace(clueId)) return false;
        return cheatsCaught.Contains(clueId.Trim());
    }

    public static bool HasClue(string clueId)
    {
        if (string.IsNullOrWhiteSpace(clueId)) return false;
        return discoveredClues.Contains(clueId.Trim());
    }

    public static bool RecordClue(string clueId)
    {
        if (string.IsNullOrWhiteSpace(clueId)) return false;
        bool added = discoveredClues.Add(clueId.Trim());
        if (added) OnStateChanged?.Invoke();
        return added;
    }

    public static bool IsRoomComplete(string sceneId)
    {
        if (string.IsNullOrWhiteSpace(sceneId)) return false;
        return completedRooms.Contains(sceneId.Trim());
    }

    /// <summary>
    /// Banks a room's one completion for this run. The caller must say whether this room is one of
    /// Aldric's table games; that classification is authored where the room is authored rather than
    /// inferred from its name. Returns false for a repeat completion, so rewards and transitions can
    /// share the same one-shot guard.
    /// </summary>
    public static bool CompleteRoom(string sceneId, bool countsAsTableGame)
    {
        if (string.IsNullOrWhiteSpace(sceneId)) return false;
        string id = sceneId.Trim();
        if (!completedRooms.Add(id)) return false;
        if (countsAsTableGame) completedTableGames.Add(id);
        OnStateChanged?.Invoke();
        Debug.Log($"[GmRunStore] Room complete: {id} (table {TableGameIndex}/7)");
        return true;
    }

    public static void RecordMiss()
    {
        // ApplySanityDelta raises OnStateChanged itself; a second invoke here would double-fire
        // every subscriber for one miss.
        ApplySanityDelta(-0.05f);
    }

    public static void RaiseCorruption(string reason)
    {
        if (CorruptionTier < MaxCorruptionTier)
        {
            CorruptionTier++;
            Debug.Log($"[GmRunStore] Corruption raised to Tier {CorruptionTier} ({reason})");
            OnStateChanged?.Invoke();
        }
    }

    public static void LowerCorruption(string reason)
    {
        if (CorruptionTier > MinCorruptionTier)
        {
            CorruptionTier--;
            Debug.Log($"[GmRunStore] Corruption lowered to Tier {CorruptionTier} ({reason})");
            OnStateChanged?.Invoke();
        }
    }

    public static void ApplySanityDelta(float delta)
    {
        Sanity = Mathf.Clamp01(Sanity + delta);
        OnStateChanged?.Invoke();
    }

    public static void RecordDefiance()
    {
        Defiance++;
        OnStateChanged?.Invoke();
    }

    public static void RecordCompliance()
    {
        Compliance++;
        OnStateChanged?.Invoke();
    }

    public static void BeginNewRun()
    {
        cheatsCaught.Clear();
        discoveredClues.Clear();
        completedRooms.Clear();
        completedTableGames.Clear();
        for (int i = 0; i < mirrorShards.Length; i++) mirrorShards[i] = false;
        CorruptionTier = MinCorruptionTier;
        Sanity = 1.0f;
        Defiance = 0;
        Compliance = 0;
        parlorMatch = null;
        parlorPresentation = GmParlorPresentationState.Empty();
        parlorOutcomeNamespace = 0;
        parlorAppliedOutcomeSequence = 0;
        parlorRestoreError = string.Empty;
        bonesMatch = null;
        bonesMatchPresent = false;
        bonesRestoreError = string.Empty;
        ParlorPresentationRestoreError = string.Empty;
        houseRunId = string.Empty;
        CurrentSceneId = "wend-hill-prologue";
        LastCheckpoint = "spawn";
        OnStateChanged?.Invoke();
        Debug.Log("[GmRunStore] Began fresh run (Tier 1 floor, full sanity, 0 catches, 0 shards)");
    }

    public static GmSaveData ToSaveData()
    {
        var data = new GmSaveData
        {
            corruptionTier = CorruptionTier,
            sanity = Sanity,
            defiance = Defiance,
            compliance = Compliance,
            cheatsCaught = new List<string>(cheatsCaught),
            discoveredClues = new List<string>(discoveredClues),
            completedRooms = new List<string>(completedRooms),
            completedTableGames = new List<string>(completedTableGames),
            mirrorShards = new List<bool>(mirrorShards),
            currentSceneId = CurrentSceneId,
            lastCheckpoint = LastCheckpoint,
            parlorMatch = parlorMatch?.DeepCopy(),
            parlorPresentation = PresentationForSave(),
            parlorOutcomeNamespace = parlorOutcomeNamespace,
            parlorAppliedOutcomeSequence = parlorAppliedOutcomeSequence,
            bonesEnvelopeVersion = 1,
            bonesPayloadPresent = bonesMatchPresent,
            bonesTurnEvidencePresent = bonesMatch?.interventionReceipt != null,
            bonesSessionEventPresent = bonesMatch?.session?.intervention != null,
            bonesMatch = CloneBonesSnapshot(bonesMatch),
            houseRunPointerVersion = string.IsNullOrEmpty(houseRunId) ? 0 : 1,
            houseRunId = houseRunId,
            timestampUtc = DateTime.UtcNow.ToString("o")
        };
        GmAccessibilitySettings.WriteTo(data);
        return data;
    }

    public static void LoadFromSaveData(GmSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        string validatedHouseRunId;
        if (data.houseRunPointerVersion == 0 && string.IsNullOrEmpty(data.houseRunId))
            validatedHouseRunId = string.Empty;
        else if (data.houseRunPointerVersion == 1 && data.houseRunId != null &&
            data.houseRunId.Length == 32 && data.houseRunId.All(character =>
                (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f')))
            validatedHouseRunId = data.houseRunId;
        else throw new InvalidDataException("save contains an invalid or unsupported House run pointer");
        cheatsCaught.Clear();
        if (data.cheatsCaught != null)
        {
            foreach (string clue in data.cheatsCaught)
                if (!string.IsNullOrWhiteSpace(clue)) cheatsCaught.Add(clue.Trim());
        }

        discoveredClues.Clear();
        if (data.discoveredClues != null)
        {
            foreach (string clue in data.discoveredClues)
                if (!string.IsNullOrWhiteSpace(clue)) discoveredClues.Add(clue.Trim());
        }


        completedRooms.Clear();
        if (data.completedRooms != null)
        {
            foreach (string room in data.completedRooms)
                if (!string.IsNullOrWhiteSpace(room)) completedRooms.Add(room.Trim());
        }

        completedTableGames.Clear();
        if (data.completedTableGames != null)
        {
            foreach (string room in data.completedTableGames)
            {
                if (string.IsNullOrWhiteSpace(room)) continue;
                string id = room.Trim();
                // A table completion is necessarily also a room completion. Repairing this relation
                // on load makes a hand-edited or older partial save safe rather than contradictory.
                completedTableGames.Add(id);
                completedRooms.Add(id);
            }
        }

        for (int i = 0; i < mirrorShards.Length; i++)
            mirrorShards[i] = data.mirrorShards != null && i < data.mirrorShards.Count && data.mirrorShards[i];

        CorruptionTier = Mathf.Clamp(data.corruptionTier, MinCorruptionTier, MaxCorruptionTier);
        Sanity = Mathf.Clamp01(data.sanity);
        Defiance = Mathf.Max(0, data.defiance);
        Compliance = Mathf.Max(0, data.compliance);
        CurrentSceneId = !string.IsNullOrEmpty(data.currentSceneId) ? data.currentSceneId : "wend-hill-prologue";
        LastCheckpoint = !string.IsNullOrEmpty(data.lastCheckpoint) ? data.lastCheckpoint : "spawn";
        // Keep even an invalid payload intact. The Parlor adapter validates and refuses it with a
        // precise error; dropping it here would turn corruption into a silent new deal.
        parlorMatch = IsUnityNullSnapshotPlaceholder(data.parlorMatch)
            ? null : data.parlorMatch?.DeepCopy();
        parlorOutcomeNamespace = data.parlorOutcomeNamespace;
        parlorAppliedOutcomeSequence = data.parlorAppliedOutcomeSequence;
        houseRunId = validatedHouseRunId;
        ParlorPresentationRestoreError = string.Empty;
        if (data.parlorPresentation == null || data.parlorPresentation.version == 0)
        {
            parlorPresentation = GmParlorPresentationState.Empty();
            if (parlorMatch != null)
                parlorPresentation.BindTo(parlorMatch.seed, parlorOutcomeNamespace);
        }
        else if (data.parlorPresentation.version == 1)
        {
            parlorPresentation = MigrateLegacyPresentation(data.parlorPresentation,
                parlorMatch, parlorOutcomeNamespace);
        }
        else
        {
            GmParlorPresentationState candidatePresentation = data.parlorPresentation.DeepCopy();
            if (candidatePresentation.TryValidate(out string presentationError) &&
                TryValidatePresentationIdentity(candidatePresentation, parlorMatch,
                    out presentationError))
                parlorPresentation = candidatePresentation;
            else
            {
                parlorPresentation = GmParlorPresentationState.Empty();
                ParlorPresentationRestoreError = presentationError;
            }
        }
        parlorRestoreError = string.Empty;
        if (parlorMatch == null)
        {
            if (parlorAppliedOutcomeSequence != 0)
                parlorRestoreError = $"Parlor outcome cursor {parlorAppliedOutcomeSequence} exists without a snapshot";
        }
        else if (!GmParlorMatch.TryRestore(parlorMatch, out GmParlorMatch migrated,
            out string migrationError))
        {
            parlorRestoreError = migrationError;
        }
        else
        {
            ulong acknowledged = migrated.HighestAcknowledgedOutcomeSequence;
            if (parlorAppliedOutcomeSequence == 0)
                parlorAppliedOutcomeSequence = acknowledged;
            else if (parlorAppliedOutcomeSequence > acknowledged)
                parlorRestoreError = $"Parlor outcome cursor {parlorAppliedOutcomeSequence} is ahead of acknowledged sequence {acknowledged}";
            else if (parlorAppliedOutcomeSequence < acknowledged)
                parlorRestoreError = $"Parlor outcome cursor {parlorAppliedOutcomeSequence} is behind acknowledged sequence {acknowledged}";

            if (parlorRestoreError.Length == 0 && migrated.TryPeekOutcome(out _, out ulong pending) &&
                (migrated.HighestDurableOutcomeSequence != pending ||
                 pending != acknowledged + 1))
                parlorRestoreError = $"Parlor pending outcome {pending} is not the next durable sequence after acknowledged {acknowledged}";
        }
        bonesMatchPresent = data.bonesMatch != null;
        bonesMatch = CloneBonesSnapshot(data.bonesMatch);
        bonesRestoreError = string.Empty;
        if (data.bonesEnvelopeVersion != 0 && data.bonesEnvelopeVersion != 1)
            bonesRestoreError = $"Bones save envelope {data.bonesEnvelopeVersion} is unsupported";
        else if (data.bonesEnvelopeVersion == 1 &&
                 data.bonesPayloadPresent != bonesMatchPresent)
            bonesRestoreError = "Bones save envelope payload presence disagrees with its payload";
        else if (bonesMatchPresent &&
                 !GmBonesMatch.TryRestore(bonesMatch, out _, out string bonesError))
            bonesRestoreError = bonesError;
        OnStateChanged?.Invoke();
    }

    static GmParlorPresentationState PresentationForSave()
    {
        if (parlorMatch == null) return GmParlorPresentationState.Empty();
        GmParlorPresentationState owned = parlorPresentation?.DeepCopy() ??
            GmParlorPresentationState.Empty();
        if (!owned.hasMatchIdentity) owned.BindTo(parlorMatch.seed, parlorOutcomeNamespace);
        return owned;
    }

    static bool TryValidatePresentationIdentity(GmParlorPresentationState state,
        GmParlorMatchSnapshot match, out string error)
    {
        if (match == null)
        {
            if (state.hasMatchIdentity || state.observedFacts.Count > 0)
            {
                error = "Parlor presentation identity exists without a match";
                return false;
            }
            error = string.Empty;
            return true;
        }
        if (!state.hasMatchIdentity ||
            !state.Matches(match.seed, parlorOutcomeNamespace))
        {
            error = $"Parlor presentation identity does not match seed {match.seed} " +
                $"and outcome namespace {parlorOutcomeNamespace}";
            return false;
        }
        error = string.Empty;
        return true;
    }

    static GmParlorPresentationState MigrateLegacyPresentation(
        GmParlorPresentationState legacy, GmParlorMatchSnapshot match,
        ulong outcomeNamespace)
    {
        var migrated = GmParlorPresentationState.Empty();
        if (match == null)
        {
            if (legacy.observedFacts != null && legacy.observedFacts.Count > 0)
                ParlorPresentationRestoreError =
                    "legacy Parlor presentation facts exist without a match";
            return migrated;
        }
        migrated.BindTo(match.seed, outcomeNamespace);
        if (legacy.observedFacts == null) return migrated;
        if (legacy.observedFacts.Count > GmParlorEvidenceLog.Capacity)
            return RejectLegacyPresentation("legacy Parlor observed facts exceed capacity");

        var order = new List<ulong>();
        var groups = new Dictionary<ulong, HashSet<GmParlorObservedFact>>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < legacy.observedFacts.Count; index++)
        {
            GmParlorObservedFactData item = legacy.observedFacts[index];
            if (item == null)
                return RejectLegacyPresentation($"legacy Parlor observed fact {index} is null");
            if (item.commandId == 0)
                return RejectLegacyPresentation(
                    $"legacy Parlor observed fact {index} has an empty command id");
            if (!Enum.IsDefined(typeof(GmParlorObservedFact), item.fact))
                return RejectLegacyPresentation(
                    $"legacy Parlor observed fact {index} has invalid enum value {(int)item.fact}");
            string key = item.commandId + "/" + (int)item.fact;
            if (!keys.Add(key))
                return RejectLegacyPresentation(
                    $"duplicate legacy Parlor observed fact {item.commandId}/{item.fact}");
            if (!groups.TryGetValue(item.commandId, out HashSet<GmParlorObservedFact> facts))
            {
                facts = new HashSet<GmParlorObservedFact>();
                groups.Add(item.commandId, facts);
                order.Add(item.commandId);
            }
            facts.Add(item.fact);
        }
        for (int index = 0; index < order.Count; index++)
        {
            ulong commandId = order[index];
            HashSet<GmParlorObservedFact> facts = groups[commandId];
            bool hasSuspicious = facts.Contains(GmParlorObservedFact.RightHandPausedAboveDeck) ||
                facts.Contains(GmParlorObservedFact.CardContactBroke);
            bool hasCalm = facts.Contains(GmParlorObservedFact.CardPlacedWithoutPause);
            bool hasObsoleteSleeve = facts.Contains(GmParlorObservedFact.SleeveBrushedTable);
            if ((hasSuspicious && (hasCalm || hasObsoleteSleeve)) ||
                (hasCalm && hasObsoleteSleeve))
                return RejectLegacyPresentation(
                    $"mixed legacy Parlor fact set for command {commandId}");
            GmParlorObservedFactData[] repaired;
            if (hasSuspicious)
            {
                repaired = new[]
                {
                    new GmParlorObservedFactData(commandId,
                        GmParlorObservedFact.RightHandPausedAboveDeck),
                    new GmParlorObservedFactData(commandId,
                        GmParlorObservedFact.CardContactBroke),
                };
            }
            else if (hasCalm)
            {
                repaired = new[]
                {
                    new GmParlorObservedFactData(commandId,
                        GmParlorObservedFact.CardPlacedWithoutPause),
                };
            }
            else continue;

            while (migrated.observedFacts.Count + repaired.Length >
                GmParlorEvidenceLog.Capacity)
                RemoveOldestPresentationCommand(migrated.observedFacts);
            migrated.observedFacts.AddRange(repaired);
        }
        if (!migrated.TryValidate(out string error))
        {
            ParlorPresentationRestoreError = $"legacy Parlor presentation migration failed: {error}";
            return GmParlorPresentationState.Empty();
        }
        return migrated;
    }

    static GmParlorPresentationState RejectLegacyPresentation(string error)
    {
        ParlorPresentationRestoreError = error;
        return GmParlorPresentationState.Empty();
    }

    static void RemoveOldestPresentationCommand(List<GmParlorObservedFactData> facts)
    {
        if (facts.Count == 0) return;
        ulong commandId = facts[0].commandId;
        for (int index = facts.Count - 1; index >= 0; index--)
            if (facts[index].commandId == commandId) facts.RemoveAt(index);
    }

    static bool IsUnityNullSnapshotPlaceholder(GmParlorMatchSnapshot snapshot)
    {
        // JsonUtility materializes a missing/null nested serializable class as an all-default
        // instance. Treat only that exact impossible shape as the old-save "no Parlor match" value.
        return snapshot != null && snapshot.seed == 0 && snapshot.randomState == 0 &&
            snapshot.phase == GmParlorMatchPhase.NotStarted && snapshot.roundNumber == 0 &&
            snapshot.trickNumber == 0 && snapshot.outcomeSequence == 0 &&
            !snapshot.hasCurrentLeadCard && !snapshot.hasCurrentFollowCard &&
            !snapshot.hasAldricPaidCard &&
            (snapshot.playerHand == null || snapshot.playerHand.Count == 0) &&
            (snapshot.aldricHand == null || snapshot.aldricHand.Count == 0) &&
            (snapshot.revealedAldricCards == null || snapshot.revealedAldricCards.Count == 0);
    }

    static GmBonesMatchSnapshot CloneBonesSnapshot(GmBonesMatchSnapshot snapshot)
        => snapshot?.DeepCopy();
}

[Serializable]
public sealed class GmSaveData
{
    public int corruptionTier = 1;
    public float sanity = 1.0f;
    public int defiance = 0;
    public int compliance = 0;
    public List<string> cheatsCaught = new List<string>();
    public List<string> discoveredClues = new List<string>();
    public List<string> completedRooms = new List<string>();
    public List<string> completedTableGames = new List<string>();
    public List<bool> mirrorShards = new List<bool> { false, false, false };
    public string currentSceneId = "wend-hill-prologue";
    public string lastCheckpoint = "spawn";
    public GmParlorMatchSnapshot parlorMatch;
    public GmParlorPresentationState parlorPresentation;
    public ulong parlorOutcomeNamespace;
    public ulong parlorAppliedOutcomeSequence;
    public int bonesEnvelopeVersion;
    public bool bonesPayloadPresent;
    public bool bonesTurnEvidencePresent;
    public bool bonesSessionEventPresent;
    public GmBonesMatchSnapshot bonesMatch;
    public int houseRunPointerVersion;
    public string houseRunId = "";
    public int accessibilitySettingsVersion;
    public bool accessibilityCaptions;
    public bool accessibilityReducedMotion;
    public bool accessibilityVibration = true;
    public bool accessibilityMonoAudio;
    public bool accessibilityHighContrast;
    public float accessibilityTextScale = 1f;
    public string timestampUtc = "";

    public static GmSaveData FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        GmSaveData data = JsonUtility.FromJson<GmSaveData>(json);
        if (data == null) return null;

        bool hasBonesObject = TryFindObjectProperty(json, "bonesMatch", 0, json.Length,
            out int bonesStart, out int bonesEnd);
        if (!hasBonesObject && !data.bonesPayloadPresent)
        {
            data.bonesMatch = null;
            return data;
        }
        if (data.bonesMatch == null) return data;

        bool hasTurnEvidence = hasBonesObject && TryFindObjectProperty(json,
            "interventionReceipt", bonesStart, bonesEnd, out _, out _);
        if (!hasTurnEvidence && !data.bonesTurnEvidencePresent)
            data.bonesMatch.interventionReceipt = null;

        int sessionStart = -1;
        int sessionEnd = -1;
        bool hasSession = hasBonesObject && TryFindObjectProperty(json, "session",
            bonesStart, bonesEnd, out sessionStart, out sessionEnd);
        if (!hasSession)
            data.bonesMatch.session = null;
        else if (data.bonesMatch.session != null)
        {
            bool hasSessionEvent = TryFindObjectProperty(json, "intervention",
                sessionStart, sessionEnd, out _, out _);
            if (!hasSessionEvent && !data.bonesSessionEventPresent)
                data.bonesMatch.session.intervention = null;
        }
        return data;
    }

    public string ToJson(bool pretty = false)
    {
        string json = JsonUtility.ToJson(this, pretty);
        if (bonesEnvelopeVersion != 1) return json;
        if (!bonesPayloadPresent)
            return RemoveObjectProperty(json, "bonesMatch", 0, json.Length);

        if (!TryFindObjectProperty(json, "bonesMatch", 0, json.Length,
            out int bonesStart, out int bonesEnd)) return json;
        if (!bonesTurnEvidencePresent)
            json = RemoveObjectProperty(json, "interventionReceipt", bonesStart, bonesEnd);
        if (!TryFindObjectProperty(json, "bonesMatch", 0, json.Length,
            out bonesStart, out bonesEnd) ||
            !TryFindObjectProperty(json, "session", bonesStart, bonesEnd,
                out int sessionStart, out int sessionEnd)) return json;
        if (!bonesSessionEventPresent)
            json = RemoveObjectProperty(json, "intervention", sessionStart, sessionEnd);
        return json;
    }

    static string RemoveObjectProperty(string json, string property, int start, int end)
    {
        if (!TryFindObjectProperty(json, property, start, end,
            out _, out int objectEnd)) return json;
        string token = "\"" + property + "\"";
        int propertyStart = json.IndexOf(token, start, end - start, StringComparison.Ordinal);
        if (propertyStart < 0) return json;
        int removeStart = propertyStart;
        while (removeStart > start && char.IsWhiteSpace(json[removeStart - 1])) removeStart--;
        int removeEnd = objectEnd + 1;
        while (removeEnd < end && char.IsWhiteSpace(json[removeEnd])) removeEnd++;
        if (removeEnd < end && json[removeEnd] == ',') removeEnd++;
        else if (removeStart > start && json[removeStart - 1] == ',') removeStart--;
        return json.Remove(removeStart, removeEnd - removeStart);
    }

    static bool TryFindObjectProperty(string json, string property, int start, int end,
        out int objectStart, out int objectEnd)
    {
        objectStart = objectEnd = -1;
        string token = "\"" + property + "\"";
        int cursor = start;
        while (cursor < end)
        {
            int found = json.IndexOf(token, cursor, end - cursor, StringComparison.Ordinal);
            if (found < 0) return false;
            int value = found + token.Length;
            while (value < end && char.IsWhiteSpace(json[value])) value++;
            if (value < end && json[value] == ':')
            {
                value++;
                while (value < end && char.IsWhiteSpace(json[value])) value++;
                if (value < end && json[value] == '{')
                {
                    int close = FindObjectEnd(json, value, end);
                    if (close >= 0)
                    {
                        objectStart = value + 1;
                        objectEnd = close;
                        return true;
                    }
                }
            }
            cursor = found + token.Length;
        }
        return false;
    }

    static int FindObjectEnd(string json, int start, int end)
    {
        int depth = 0;
        bool quoted = false;
        bool escaped = false;
        for (int index = start; index < end; index++)
        {
            char value = json[index];
            if (quoted)
            {
                if (escaped) escaped = false;
                else if (value == '\\') escaped = true;
                else if (value == '"') quoted = false;
                continue;
            }
            if (value == '"') quoted = true;
            else if (value == '{') depth++;
            else if (value == '}' && --depth == 0) return index;
        }
        return -1;
    }
}
