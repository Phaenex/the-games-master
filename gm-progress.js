// gm-progress.js — shared, persisted cumulative "cheats caught" total.
// The Thesis/cheat ending (per the story bible) needs 8+ cheats caught across
// scenes — Parlor, Court, the Labyrinth — so this has to survive page reloads
// and new sessions instead of resetting to 0 every time a scene loads.
// Any scene calls add(1) each time its own cheat-catch fires; get() reads the
// running total back so a fresh scene load can pick up where the player left off.
window.GMProgress = {
  get() {
    try { return parseInt(localStorage.getItem('gm_cheats_caught') || '0', 10) || 0; }
    catch (e) { return 0; }
  },
  add(n) {
    n = (typeof n === 'number' && n > 0) ? n : 1;
    const next = this.get() + n;
    try { localStorage.setItem('gm_cheats_caught', String(next)); } catch (e) {}
    return next;
  }
};
