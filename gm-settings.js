// gm-settings.js — shared, persisted player preferences (audio levels, motion, text size).
window.GMSettings = {
  DEFAULTS: { volMaster: 100, volMusic: 100, volSfx: 100, reduceMotion: false, textSize: 'default' },
  get() {
    try {
      const stored = JSON.parse(localStorage.getItem('gm_settings') || '{}');
      return Object.assign({}, this.DEFAULTS, stored);
    } catch (e) { return Object.assign({}, this.DEFAULTS); }
  },
  set(patch) {
    const next = Object.assign({}, this.get(), patch);
    try { localStorage.setItem('gm_settings', JSON.stringify(next)); } catch (e) {}
    return next;
  }
};
