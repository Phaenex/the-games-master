using System;
using UnityEngine;

public sealed class GmHiddenRoomController : MonoBehaviour
{
    public const string InvitationClueId = "hidden-room-original-invitation";
    public const string MirrorAssembledClueId = "mirror-fully-assembled";
    public const int ShardThreeIndex = 2; // Shard #3
    public const int JournalCount = 8;    // the eight previous victims; 9 would be Percival

    // The Hidden Room is its own loaded scene, so leaving and coming back builds a fresh controller
    // while the run itself survives in GmRunStore. Instance flags reset with the component, which
    // let a returning player re-farm the invitation's sanity reward and re-fire the mirror
    // completion event; every piece of progress is therefore read back from the run, not remembered
    // here.
    public bool InvitationRead => GmRunStore.HasCatch(InvitationClueId);
    public bool ShardThreeCollected => GmRunStore.HasShard(ShardThreeIndex);
    public bool MirrorAssembled => GmRunStore.AllShardsCollected;

    public int JournalsInspected
    {
        get
        {
            int inspected = 0;
            for (int guest = 1; guest <= JournalCount; guest++)
                if (GmRunStore.HasCatch(JournalClueId(guest))) inspected++;
            return inspected;
        }
    }

    public event Action OnStateChanged;
    public event Action OnMirrorCompleted;

    public bool ReadOriginalInvitation()
    {
        // RecordCatch decides whether this is the first read of the run: it returns false for a clue
        // the run already holds, which is exactly the re-entry case the reward must not repeat.
        if (!GmRunStore.RecordCatch(InvitationClueId)) return false;
        GmRunStore.ApplySanityDelta(GmFeelConfig.Active.hiddenRoomInvitationSanityGain);
        Debug.Log("[GmHiddenRoom] Read Aldric Voss's original invitation letter: cycle truth revealed!");
        OnStateChanged?.Invoke();
        return true;
    }

    public bool CollectShardThree()
    {
        if (!GmRunStore.CollectShard(ShardThreeIndex)) return false;
        Debug.Log("[GmHiddenRoom] Mirror Shard #3 collected from standing frame!");

        // The completion event rides on the catch being new, so the true-escape unlock announces
        // itself once per run rather than once per visit.
        if (MirrorAssembled && GmRunStore.RecordCatch(MirrorAssembledClueId))
        {
            OnMirrorCompleted?.Invoke();
            Debug.Log("[GmHiddenRoom] ALL THREE MIRROR SHARDS ASSEMBLED: True escape pathway unlocked!");
        }

        OnStateChanged?.Invoke();
        return true;
    }

    public bool InspectJournal(int guestIndex)
    {
        if (guestIndex < 1 || guestIndex > JournalCount) return false;
        if (!GmRunStore.RecordCatch(JournalClueId(guestIndex))) return false;
        OnStateChanged?.Invoke();
        return true;
    }

    static string JournalClueId(int guestIndex) => $"hidden-room-journal-{guestIndex}";

}
