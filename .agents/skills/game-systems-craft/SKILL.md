---
name: game-systems-craft
description: Guidelines and interfaces for authoring parlor minigames, cheat-catching mechanics, interactive props, and persistent shard progression
---

# Game Systems Craft Skill

Use this skill when implementing or modifying game rules, parlor mechanics (*Flames*, *Shut the Box*, *Court*), inspectable environment items, or narrative shard progression.

## 1. Inspectable Props (`GmInteractable`)
Attach `GmInteractable` to objects that provide lore, clues, or item acquisition:
```csharp
var interact = go.AddComponent<GmInteractable>();
interact.Configure("item-id", "Action Verb", range: 2.8f, focusAngle: 12f);
interact.BindContent("First examine text (uninspected)", "Second examine text (subsequent uses)");
```

## 2. Card Game & Cheat Detection Architecture
- Opponent cheating routines must trigger visible and auditable "tells" (card sleeve glance, finger tap, false call).
- When a tell fires, open the reaction window for the player to press the "Catch Cheat" action (`Space` / `A button`).
- Successful catches increment `GmRunStore.CheatsCaughtTotal` and advance corruption tier.

## 3. Shard & Persistence Storage (`GmRunStore`)
- Atomic JSON persistence in `Application.persistentDataPath/the_games_master_save.json`.
- Track `MirrorShards[0..2]`, `CheatsCaughtTotal`, and story milestones.
- Shards gate Ending 3 (True Whistleblower Escape).
