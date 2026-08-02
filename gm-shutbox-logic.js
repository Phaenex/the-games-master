// Shut the Box — pure rules (no Three.js). Ready for the Phase 2 scene + harness.
// Head-to-head variant from docs/superpowers/specs/2026-07-12-per-game-cheats-design.md:
//   two boxes × tiles 1–9, alternate rolls, voluntary stop, Hold catch.
// Tiles map to ledger order: 1=Marr … 9=Percival (never stated in dialogue).
(function (root) {
  const TILES = [1, 2, 3, 4, 5, 6, 7, 8, 9];
  const LEDGER = ['Marr', 'Dufresne', 'Pike', 'Hale', 'Gall', 'Quill', 'Thale', 'Aubrey-Locke', 'Percival'];

  function freshBox() {
    const open = {};
    TILES.forEach((n) => { open[n] = true; });
    return { open, locked: false, sum: 45 };
  }

  function openSum(box) {
    let s = 0;
    TILES.forEach((n) => { if (box.open[n]) s += n; });
    return s;
  }

  // All non-empty subsets of currently-open tiles that sum to `total`.
  function legalMoves(box, total) {
    const open = TILES.filter((n) => box.open[n]);
    const out = [];
    const n = open.length;
    for (let mask = 1; mask < (1 << n); mask++) {
      let sum = 0;
      const pick = [];
      for (let i = 0; i < n; i++) {
        if (mask & (1 << i)) { sum += open[i]; pick.push(open[i]); }
      }
      if (sum === total) out.push(pick.slice().sort((a, b) => a - b));
    }
    // de-dupe identical picks
    const key = (p) => p.join(',');
    const seen = {};
    return out.filter((p) => { const k = key(p); if (seen[k]) return false; seen[k] = 1; return true; });
  }

  function applyMove(box, tiles) {
    if (box.locked) return { ok: false, reason: 'locked' };
    const set = tiles.slice();
    for (const t of set) {
      if (!box.open[t]) return { ok: false, reason: 'tile-shut', tile: t };
    }
    set.forEach((t) => { box.open[t] = false; });
    box.sum = openSum(box);
    return { ok: true, sum: box.sum };
  }

  function lockBox(box) {
    box.locked = true;
    box.sum = openSum(box);
    return box.sum;
  }

  // Aldric AI (legal only) — prefer leaving opponent with hard leftovers:
  // among legal moves, pick the one that leaves the lowest open sum (greedy).
  // Scope matches gmFollow() complexity: greedy scan, no deep search.
  function pickLegalMove(box, total) {
    const moves = legalMoves(box, total);
    if (!moves.length) return null;
    let best = moves[0], bestSum = Infinity;
    for (const m of moves) {
      const trial = { open: Object.assign({}, box.open), locked: false };
      applyMove(trial, m);
      const s = openSum(trial);
      if (s < bestSum) { bestSum = s; best = m; }
    }
    return best;
  }

  // Hold checks — what the player claims vs board truth.
  // kind: 'palm' | 'falseCall' | 'tamper'
  // claim: { tiles?, roll?, tile? } depending on kind
  function evaluateHold(kind, claim, truth) {
    // truth shape:
    //   palm:     { didPalm:bool }
    //   falseCall:{ announcedTiles:[], legalForRoll:[[n,...],...] }
    //   tamper:   { tile:n, wasShut:bool, isOpenNow:bool }
    if (kind === 'palm') {
      const hit = !!truth.didPalm;
      return { correct: hit, penalty: !hit };
    }
    if (kind === 'falseCall') {
      const ann = (claim.tiles || []).slice().sort((a, b) => a - b).join(',');
      const legal = (truth.legalForRoll || []).some((m) => m.slice().sort((a, b) => a - b).join(',') === ann);
      // false call = he shut tiles that weren't a legal move for the roll
      const hit = !legal && (claim.tiles || []).length > 0;
      return { correct: hit, penalty: !hit };
    }
    if (kind === 'tamper') {
      const tile = claim.tile != null ? claim.tile : truth.tile;
      const hit = !!truth.wasShut && !!truth.isOpenNow && tile === truth.tile;
      return { correct: hit, penalty: !hit, opensHiddenDoor: hit && tile === 9 };
    }
    return { correct: false, penalty: true };
  }

  const api = {
    TILES, LEDGER, freshBox, openSum, legalMoves, applyMove, lockBox, pickLegalMove, evaluateHold,
    guestForTile: (n) => LEDGER[n - 1] || null,
  };

  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  root.GMShutBox = api;
})(typeof window !== 'undefined' ? window : globalThis);
