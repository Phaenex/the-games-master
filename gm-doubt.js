// gm-doubt.js — minimal cross-scene "doubt" progress used by the invitation letter.
// Stage 0: blank (default). Stage 1: "watch his hands". Stage 2: "he is the friend".
window.GMDoubt = {
  get() {
    try { return parseInt(localStorage.getItem('gm_doubt') || '0', 10) || 0; }
    catch (e) { return 0; }
  },
  set(stage) {
    try { localStorage.setItem('gm_doubt', String(Math.max(this.get(), stage))); }
    catch (e) {}
  }
};
