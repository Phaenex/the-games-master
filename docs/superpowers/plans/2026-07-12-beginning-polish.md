# Beginning Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the four gaps found in the beginning-polish audit: a real Options screen, the invitation letter's three-stage doubt reveal (backed by a new minimal shared persistence module), a scripted post-wake chandelier beat in the Entry Hall, and the small settings/motion plumbing those need.

**Architecture:** Two new tiny shared JS modules (`gm-doubt.js`, `gm-settings.js`), loaded via `<script src="./gm-doubt.js">` the same way `support.js` already is — no build step, no bundler, matching the project's existing pattern exactly. Everything else is additive edits to the three existing `.dc.html` files.

**Tech Stack:** Vanilla JS, the existing `dc-runtime` component pattern, `localStorage` for persistence (the only precedent already in the codebase is `gm_bob`), Playwright via the existing headless Test Harness runner for verification.

---

### Task 1: Shared modules — `gm-doubt.js` and `gm-settings.js`

**Files:**
- Create: `gm-doubt.js`
- Create: `gm-settings.js`

- [ ] **Step 1: Write `gm-doubt.js`**

```js
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
```

- [ ] **Step 2: Write `gm-settings.js`**

```js
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
```

- [ ] **Step 3: Verify both files load standalone**

Run: `node -e "global.localStorage={_d:{},getItem(k){return this._d[k]||null},setItem(k,v){this._d[k]=v}}; require('./gm-doubt.js'); require('./gm-settings.js'); console.log(GMDoubt.get(), GMSettings.get());"`

Expected output: `0 { volMaster: 100, volMusic: 100, volSfx: 100, reduceMotion: false, textSize: 'default' }`

- [ ] **Step 4: Commit**

```bash
git add gm-doubt.js gm-settings.js
git commit -m "Add gm-doubt.js and gm-settings.js shared persistence modules"
```

---

### Task 2: Prologue — letter's back face reads doubt stage

**Files:**
- Modify: `The Games Master - Prologue.dc.html` (the `<helmet>` script includes, and the letter BACK face block around line 143-149)

**Context:** The letter's back face currently always shows static "the back is bare..." text (line 147). It needs to show one of three texts depending on `GMDoubt.get()`, computed fresh each time the letter is raised (not cached in component state, so a change made in another scene is picked up next time the player opens the letter).

- [ ] **Step 1: Add the script include**

In the `<head>` section (after the existing `<script src="./support.js"></script>` line), add:

```html
<script src="./gm-doubt.js"></script>
```

- [ ] **Step 2: Replace the static back-face text with a computed property**

Find this block (current line 146-148):

```html
            <div style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center;padding:34px;">
              <div style="font-family:'Cormorant Garamond',serif;font-style:italic;font-size:20px;line-height:1.55;color:rgba(36,26,13,0.13);letter-spacing:0.03em;transform:rotate(-2deg);text-align:center;">the back is bare —<br>though the paper holds<br>a faint impression,<br>as if something waits<br>to be written</div>
            </div>
```

Replace with:

```html
            <div style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center;padding:34px;">
              <div style="{{ letterBackStyle }}">{{ letterBackText }}</div>
            </div>
```

- [ ] **Step 3: Add `letterBackText` / `letterBackStyle` to the render-values method**

Find where other render values are computed (near `letterTransform`, `flipTransform`, search for the method returning the object with `bobLabel`, `letterHintText`, etc. — this is the `renderVals()`-equivalent method in this component). Add:

```js
      letterBackText: (()=>{ const s = (window.GMDoubt ? GMDoubt.get() : 0);
        if (s>=2) return 'a friend who never left. The hand is Aldric’s own. He wrote the warning. He is the friend.';
        if (s>=1) return 'He cannot lose… watch his hands. — a friend who got out.';
        return 'the back is bare —<br>though the paper holds<br>a faint impression,<br>as if something waits<br>to be written';
      })(),
      letterBackStyle: (()=>{ const s = (window.GMDoubt ? GMDoubt.get() : 0);
        return 'font-family:\'Cormorant Garamond\',serif;font-style:italic;font-size:20px;line-height:1.55;color:'+(s>0?'rgba(36,26,13,0.82)':'rgba(36,26,13,0.13)')+';letter-spacing:0.03em;transform:rotate(-2deg);text-align:center;';
      })(),
```

Note: stage 0's text keeps its original `<br>` line breaks; stages 1-2 are single flowing lines, which is why only stage 0 needs the literal `<br>` tags preserved in the string.

- [ ] **Step 4: Add a dev-bridge hook to force a stage, for testing**

In `devSnapshot()`, add `doubt: (window.GMDoubt ? GMDoubt.get() : 0)` to the returned object. In `devGoTo(name)`, add a branch: `else if (name.indexOf('doubt:')===0) { const n=parseInt(name.split(':')[1],10); if(window.GMDoubt) GMDoubt.set(n); this.forceUpdate(); }` — this lets the Test Harness / a manual check call `window.__GM.goTo('doubt:1')` to force a stage without playing through the real trigger conditions.

- [ ] **Step 5: Manual verification**

Serve the folder locally, open Prologue, open devtools console:
```js
window.__GM.goTo('doubt:0'); // then raise+flip the letter, confirm original faint text
window.__GM.goTo('doubt:1'); // raise+flip again, confirm "watch his hands" text, legible (not faint)
window.__GM.goTo('doubt:2'); // raise+flip again, confirm "he is the friend" text
```

- [ ] **Step 6: Commit**

```bash
git add "The Games Master - Prologue.dc.html"
git commit -m "Prologue: letter's back face reads GMDoubt stage"
```

---

### Task 3: Entry Hall — invitation examine POI + doubt-stage-1 trigger

**Files:**
- Modify: `The Games Master - Entry Hall.dc.html`

**Context:** Entry Hall's POI system (`buildPOIs()`, `tryExamine()`, `speak()`) is uniformly text-based — no 3D card-flip visual exists here, unlike Prologue. The invitation POI should match this existing pattern (an `es` string), not import Prologue's visual component. `es` needs to be computed per-examine rather than a static string, since it depends on the current doubt stage.

- [ ] **Step 1: Add the script include**

In the `<head>` section, add `<script src="./gm-doubt.js"></script>` alongside the existing `support.js` include.

- [ ] **Step 2: Add the invitation POI**

In `buildPOIs()`, add one new entry to the `this.pois` array (position it near the entry doors, where the player first stands after waking — reuse coordinates close to the existing armor/coat-stand POIs at z≈13-15):

```js
      { x:-3, z:14, r:3.4, type:'examine', verb:'Check your coat pocket',
        em:'I felt for the invitation, still folded where I’d put it.', esFn:()=>{
          const s = (window.GMDoubt ? GMDoubt.get() : 0);
          if (s>=2) return 'The back, this time, was not bare. a friend who never left. The hand is Aldric’s own. He wrote the warning. He is the friend.';
          if (s>=1) return 'Something had changed on the back since I’d last looked. He cannot lose… watch his hands. — a friend who got out.';
          return 'The back was still bare. Whatever waited to be written there hadn’t been, not yet.';
        } },
```

- [ ] **Step 3: Wire `tryExamine()` to call `esFn` when present**

Find `tryExamine()`:

```js
  tryExamine() {
    const p=this._active; if(!p||this.mode!=='hall'||!this.walkEnabled) return;
    if (p.em) this.speak(p.em, p.es, 6800);
  }
```

Replace with:

```js
  tryExamine() {
    const p=this._active; if(!p||this.mode!=='hall'||!this.walkEnabled) return;
    const text = p.esFn ? p.esFn() : p.es;
    if (p.em) { this.speak(p.em, text, 6800); this.trackDoubtProgress(p); }
  }
```

- [ ] **Step 4: Add the doubt-stage-1 trigger**

Add a new method, tracking distinct examine-type POIs read (portraits + ledger only, not the invitation itself, the piano, or the globe):

```js
  trackDoubtProgress(p) {
    if (!window.GMDoubt) return;
    p._read = true;
    const isTracked = (poi)=> poi.type==='examine' && poi.verb==='Read the nameplate';
    const ledgerRead = this.pois.some(poi => poi.verb==='Read the open ledger' && poi._read);
    const portraitsRead = this.pois.filter(poi => isTracked(poi) && poi._read).length;
    if (ledgerRead && portraitsRead >= 5) GMDoubt.set(1);
  }
```

- [ ] **Step 5: Manual verification**

Serve locally, open Entry Hall, examine the ledger and 5 of the 9 portraits, then walk to the invitation POI near the entry doors and examine it — confirm it now shows the stage-1 text instead of "still bare." Confirm examining the invitation before reaching that threshold still shows the stage-0 text.

- [ ] **Step 6: Commit**

```bash
git add "The Games Master - Entry Hall.dc.html"
git commit -m "Entry Hall: add invitation examine POI, wire doubt-stage-1 trigger"
```

---

### Task 4: Entry Hall — post-wake chandelier beat

**Files:**
- Modify: `The Games Master - Entry Hall.dc.html`

**Context:** `beginWake()`'s timeline currently ends at `T(11400, ...)`, where `walkEnabled` flips true. Insert one more beat before that, pushing the final timer back to `T(15000, ...)`, and give the existing chandelier (`this.chand`, a `THREE.Group`) a brief one-time sway timed to the new beat.

- [ ] **Step 1: Adjust the timeline**

Find (current lines 141-142):

```js
    T(7800, ()=> { this.setState({ fade:0 }); this.rising = true; this._riseStart = Date.now(); });
    T(11400,()=> { this.waking=false; this.rising=false; this.standY=1.7; this.wakeRoll=0; this.wakePitch=0; this.walkEnabled=true; this.setState({ beatOn:false, hintOn:true, headachePulse:0 }); if (this._pulseTick) { clearInterval(this._pulseTick); this._pulseTick=null; } });
```

Replace with:

```js
    T(7800, ()=> { this.setState({ fade:0 }); this.rising = true; this._riseStart = Date.now(); });
    T(11400,()=> {
      this.waking=false; this.rising=false; this.standY=1.7; this.wakeRoll=0; this.wakePitch=0;
      this.setState({ beatMain:'The chandelier hung dead still overhead.', beatSub:'Then — not a draft, nothing in this house moved on its own — it swayed, once, the way a thing does when someone has just let go of it.' });
      this.swayChandelier();
    });
    T(15000,()=> { this.walkEnabled=true; this.setState({ beatOn:false, hintOn:true, headachePulse:0 }); if (this._pulseTick) { clearInterval(this._pulseTick); this._pulseTick=null; } });
```

- [ ] **Step 2: Add the sway animation**

```js
  swayChandelier() {
    if (!this.chand) return;
    const start = Date.now(), dur = 2600;
    const tick = () => {
      const t = (Date.now()-start)/dur;
      if (t >= 1) { this.chand.rotation.z = 0; return; }
      this.chand.rotation.z = Math.sin(t*Math.PI*3) * 0.05 * (1-t);
      requestAnimationFrame(tick);
    };
    tick();
  }
```

- [ ] **Step 3: Manual verification**

Serve locally, trigger `window.__GM.goTo('wake')`, let the full sequence play (or fast-forward via the dev bridge), confirm the new chandelier beat appears after "How long had I been under?" and before free-roam opens, and that the chandelier visibly sways once during it.

- [ ] **Step 4: Commit**

```bash
git add "The Games Master - Entry Hall.dc.html"
git commit -m "Entry Hall: add scripted chandelier-sway beat after waking, before free-roam"
```

---

### Task 5: Parlor — doubt-stage-2 trigger + one-time letter insert

**Files:**
- Modify: `The Parlor - Playable Prototype.dc.html`

**Context:** The `cheatsCaught>=4` check already exists at the `roundsP===2` (player wins the match) branch. Add the doubt trigger there, plus a one-time full-screen insert showing the letter's stage-3 text, gated so it only ever shows once per session (not on every subsequent game).

- [ ] **Step 1: Add the script include**

In the `<head>` section, add `<script src="./gm-doubt.js"></script>`.

- [ ] **Step 2: Fire the trigger and flag the insert**

Find (existing code, in the round-resolution method):

```js
      if (g.roundsP===2 || g.roundsG===2) {
        g.phase = 'gameDone';
        if (g.roundsP===2) { g.gmState = g.cheatsCaught>=4 ? 'VULNERABLE':'IRRITATED'; g.line = g.cheatsCaught>=4 ? 'I was very good, once. You were better.' : 'You won. Do not mistake it for the end.'; g.status = g.cheatsCaught>=4 ? 'You beat him — and you saw how.' : 'You beat him. But did you see how?'; }
        else { g.gmState='HOSTILE'; g.line='The house keeps you a while longer.'; g.status='He wins the night. Try again.'; }
        this.setState(g); return;
      }
```

Replace with:

```js
      if (g.roundsP===2 || g.roundsG===2) {
        g.phase = 'gameDone';
        if (g.roundsP===2) {
          g.gmState = g.cheatsCaught>=4 ? 'VULNERABLE':'IRRITATED'; g.line = g.cheatsCaught>=4 ? 'I was very good, once. You were better.' : 'You won. Do not mistake it for the end.'; g.status = g.cheatsCaught>=4 ? 'You beat him — and you saw how.' : 'You beat him. But did you see how?';
          if (g.cheatsCaught>=4 && window.GMDoubt) {
            const wasBelow = GMDoubt.get() < 2;
            GMDoubt.set(2);
            if (wasBelow) g.showLetterInsert = true;
          }
        }
        else { g.gmState='HOSTILE'; g.line='The house keeps you a while longer.'; g.status='He wins the night. Try again.'; }
        this.setState(g); return;
      }
```

- [ ] **Step 3: Add the insert overlay UI**

Near the other `sc-if` overlay blocks (find the pattern used for the existing win/loss test overlay), add:

```html
  <sc-if value="{{ showLetterInsert }}" hint-placeholder-val="{{ false }}">
    <div onClick="{{ onDismissLetterInsert }}" style="position:fixed;inset:0;z-index:60;background:rgba(0,0,0,0.86);display:flex;align-items:center;justify-content:center;cursor:pointer;padding:8vh 8vw;">
      <div style="max-width:520px;text-align:center;font-family:'Cormorant Garamond',serif;font-style:italic;font-size:22px;line-height:1.6;color:#EBE0D1;">a friend who never left. The hand is Aldric&rsquo;s own. He wrote the warning. He is the friend.
        <div style="margin-top:26px;font-family:'JetBrains Mono',monospace;font-size:10px;letter-spacing:0.2em;color:#5c5344;text-transform:uppercase;">click to continue</div>
      </div>
    </div>
  </sc-if>
```

- [ ] **Step 4: Add the dismiss handler**

Add to the render-values method's returned handlers object: `onDismissLetterInsert:()=>this.setState({showLetterInsert:false})`. Add `showLetterInsert:false` to the initial `state = {...}` object.

- [ ] **Step 5: Manual verification**

Serve locally, use the dev bridge to force `cheatsCaught` to 5 and trigger a player win (`window.__GM.goTo('winGame')` already sets `cheatsCaught=5`), confirm the insert appears once; confirm winning a second time in the same session does not show it again (since `GMDoubt.get()` is already `2`).

- [ ] **Step 6: Commit**

```bash
git add "The Parlor - Playable Prototype.dc.html"
git commit -m "Parlor: fire doubt-stage-2 and show one-time letter insert on cheatsCaught>=4 win"
```

---

### Task 6: Prologue — Options screen

**Files:**
- Modify: `The Games Master - Prologue.dc.html`

**Context:** Replace the inline "Head-bob" menu button with a proper "Options" button leading to a new `isOptions` screen. Migrate `bobOn`/`gm_bob` into `GMSettings.reduceMotion`.

- [ ] **Step 1: Add the script include**

Add `<script src="./gm-settings.js"></script>` to `<head>`.

- [ ] **Step 2: Update the main menu block**

Find (current lines 47-52):

```html
      <div style="display:flex;flex-direction:column;gap:2px;margin-top:52px;">
        <button onClick="{{ onNewGame }}" style="font-family:'Cormorant Garamond',serif;font-size:26px;color:#FFFFF2;background:none;border:none;cursor:pointer;padding:9px 30px;letter-spacing:0.04em;transition:color .2s;" style-hover="color:#F2C759;">New Game</button>
        <div style="font-family:'Cormorant Garamond',serif;font-size:23px;color:#5c5344;padding:9px 30px;letter-spacing:0.04em;cursor:not-allowed;">Continue</div>
        <button onClick="{{ onToggleBob }}" style="font-family:'Cormorant Garamond',serif;font-size:23px;color:#8a7a5e;background:none;border:none;cursor:pointer;padding:9px 30px;letter-spacing:0.04em;transition:color .2s;" style-hover="color:#E6B86B;">Head-bob · {{ bobLabel }}</button>
        <div style="font-family:'Cormorant Garamond',serif;font-size:23px;color:#5c5344;padding:9px 30px;letter-spacing:0.04em;cursor:not-allowed;">Leave the house</div>
      </div>
```

Replace with:

```html
      <div style="display:flex;flex-direction:column;gap:2px;margin-top:52px;">
        <button onClick="{{ onNewGame }}" style="font-family:'Cormorant Garamond',serif;font-size:26px;color:#FFFFF2;background:none;border:none;cursor:pointer;padding:9px 30px;letter-spacing:0.04em;transition:color .2s;" style-hover="color:#F2C759;">New Game</button>
        <div style="font-family:'Cormorant Garamond',serif;font-size:23px;color:#5c5344;padding:9px 30px;letter-spacing:0.04em;cursor:not-allowed;">Continue</div>
        <button onClick="{{ onOpenOptions }}" style="font-family:'Cormorant Garamond',serif;font-size:23px;color:#8a7a5e;background:none;border:none;cursor:pointer;padding:9px 30px;letter-spacing:0.04em;transition:color .2s;" style-hover="color:#E6B86B;">Options</button>
        <div style="font-family:'Cormorant Garamond',serif;font-size:23px;color:#5c5344;padding:9px 30px;letter-spacing:0.04em;cursor:not-allowed;">Leave the house</div>
      </div>
```

- [ ] **Step 3: Add the Options screen block**

Add a new `sc-if` block right after the `isMenu` block:

```html
  <sc-if value="{{ isOptions }}" hint-placeholder-val="{{ false }}">
    <div style="position:fixed;inset:0;z-index:31;display:flex;flex-direction:column;align-items:center;background:rgba(0,0,0,0.88);padding:8vh 8vw;overflow-y:auto;">
      <h2 style="font-family:'Cormorant Garamond',serif;font-weight:500;font-size:38px;color:#FFFFF2;margin:0 0 8px;">Options</h2>
      <div style="width:min(520px,88vw);margin-top:26px;">
        <div style="font-family:'JetBrains Mono',monospace;font-size:11px;letter-spacing:0.2em;color:#A69980;text-transform:uppercase;margin-bottom:10px;">Audio</div>
        <sc-for list="{{ audioSliders }}" as="a" hint-placeholder-count="3">
          <div style="display:flex;align-items:center;gap:14px;margin-bottom:12px;">
            <span style="font-family:'Cormorant Garamond',serif;font-size:16px;color:#EBE0D1;width:90px;">{{ a.label }}</span>
            <input type="range" min="0" max="100" value="{{ a.value }}" onInput="{{ a.onInput }}" style="flex:1;" />
            <span style="font-family:'JetBrains Mono',monospace;font-size:11px;color:#A69980;width:32px;text-align:right;">{{ a.value }}</span>
          </div>
        </sc-for>
        <div style="font-family:'JetBrains Mono',monospace;font-size:11px;letter-spacing:0.2em;color:#A69980;text-transform:uppercase;margin:22px 0 10px;">Accessibility</div>
        <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:12px;">
          <span style="font-family:'Cormorant Garamond',serif;font-size:16px;color:#EBE0D1;">Reduce motion</span>
          <button onClick="{{ onToggleReduceMotion }}" style="font-family:'JetBrains Mono',monospace;font-size:11px;letter-spacing:0.12em;text-transform:uppercase;background:none;border:1px solid rgba(166,153,128,0.4);color:#EBE0D1;cursor:pointer;padding:6px 14px;">{{ reduceMotionLabel }}</button>
        </div>
        <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:12px;">
          <span style="font-family:'Cormorant Garamond',serif;font-size:16px;color:#EBE0D1;">Text size</span>
          <button onClick="{{ onCycleTextSize }}" style="font-family:'JetBrains Mono',monospace;font-size:11px;letter-spacing:0.12em;text-transform:uppercase;background:none;border:1px solid rgba(166,153,128,0.4);color:#EBE0D1;cursor:pointer;padding:6px 14px;">{{ textSizeLabel }}</button>
        </div>
        <div style="font-family:'JetBrains Mono',monospace;font-size:11px;letter-spacing:0.2em;color:#A69980;text-transform:uppercase;margin:22px 0 10px;">Controls</div>
        <div style="font-family:'JetBrains Mono',monospace;font-size:12px;line-height:2;color:#A69980;">Click · look · move mouse<br>Arrows turn · WASD walk<br>Space jump · C crouch · F light · E interact</div>
      </div>
      <button onClick="{{ onCloseOptions }}" style="margin-top:36px;font-family:'JetBrains Mono',monospace;font-size:12px;letter-spacing:0.16em;text-transform:uppercase;color:#FFFFF2;background:rgba(242,199,89,0.12);border:1px solid #F2C759;cursor:pointer;padding:12px 26px;">Back</button>
    </div>
  </sc-if>
```

- [ ] **Step 4: Add the handlers and render values**

```js
  onOpenOptions() { this.setState({ isMenu:false, isOptions:true }); }
  onCloseOptions() { this.setState({ isMenu:true, isOptions:false }); }
```

In the render-values method, add:

```js
      isOptions: st.isOptions,
      audioSliders: ['volMaster','volMusic','volSfx'].map((k,i)=>({
        label:['Master','Music','SFX'][i], value:(GMSettings.get()[k]),
        onInput:(e)=>{ GMSettings.set({[k]:parseInt(e.target.value,10)}); this.forceUpdate(); }
      })),
      reduceMotionLabel: GMSettings.get().reduceMotion ? 'On' : 'Off',
      onToggleReduceMotion: ()=>{ const cur=GMSettings.get().reduceMotion; GMSettings.set({reduceMotion:!cur}); this.forceUpdate(); },
      textSizeLabel: {default:'Default',large:'Large',small:'Small'}[GMSettings.get().textSize] || 'Default',
      onCycleTextSize: ()=>{ const order=['default','large','small']; const cur=GMSettings.get().textSize; const next=order[(order.indexOf(cur)+1)%order.length]; GMSettings.set({textSize:next}); this.forceUpdate(); },
```

- [ ] **Step 5: Migrate the head-bob logic to read `GMSettings.reduceMotion` instead of `state.bobOn`**

Find (current lines 479-482):

```js
      const bobOn = this.state.bobOn !== false;
      ...
      const bob = bobOn ? Math.sin(this._bob*2)*0.055*amt : 0;
      const sway = bobOn ? Math.sin(this._bob)*0.02*amt : 0;
```

Replace with:

```js
      const bobOn = !(window.GMSettings && GMSettings.get().reduceMotion);
      ...
      const bob = bobOn ? Math.sin(this._bob*2)*0.055*amt : 0;
      const sway = bobOn ? Math.sin(this._bob)*0.02*amt : 0;
```

Remove the now-unused `bobOn` from `state = {...}`, the `gm_bob` localStorage read in `componentDidMount`, and `bobLabel`/`onToggleBob` from the render-values method (fully superseded by the Options screen's Reduce Motion toggle).

- [ ] **Step 6: Add `devPhaseList` entry for manual testing**

In `devPhaseList()`, add `'options'` to the array, and a matching branch in `devGoTo()`: `else if (name==='options') { this.setState(Object.assign({}, base, { isOptions:true })); }`.

- [ ] **Step 7: Manual verification**

Serve locally, open the menu, click Options, move each slider (confirm the number updates), toggle Reduce Motion (confirm the label flips and, back in a walk, head-bob actually stops), cycle Text Size, click Back, reload the page, reopen Options — confirm every value persisted.

- [ ] **Step 8: Commit**

```bash
git add "The Games Master - Prologue.dc.html"
git commit -m "Prologue: add real Options screen (audio, accessibility, controls reference), migrate head-bob into Reduce Motion"
```

---

### Task 7: Entry Hall — respect Reduce Motion and Text Size

**Files:**
- Modify: `The Games Master - Entry Hall.dc.html`

**Context:** The wake sequence's `headachePulse` oscillation (in the `_pulseTick` interval set up in `beginWake()`) needs to go static when Reduce Motion is on. Beat text (`beatMain`/`beatSub`) needs a size multiplier from `GMSettings.textSize`.

- [ ] **Step 1: Add the script include**

Add `<script src="./gm-settings.js"></script>` to `<head>`.

- [ ] **Step 2: Gate the pulse oscillation**

Find the `_pulseTick` setup in `beginWake()`:

```js
    this._pulseTick = setInterval(()=>{
      const decay = this.rising ? Math.max(0, 1 - (Date.now()-(this._riseStart||Date.now()))/3600) : 1;
      const t = Date.now()/1000;
      this.setState({ headachePulse: decay * (0.5+0.5*Math.sin(t*2*Math.PI/1.1)) });
    }, 80);
```

Replace with:

```js
    const reduceMotion = window.GMSettings && GMSettings.get().reduceMotion;
    if (reduceMotion) { this.setState({ headachePulse: 0.4 }); }
    else {
      this._pulseTick = setInterval(()=>{
        const decay = this.rising ? Math.max(0, 1 - (Date.now()-(this._riseStart||Date.now()))/3600) : 1;
        const t = Date.now()/1000;
        this.setState({ headachePulse: decay * (0.5+0.5*Math.sin(t*2*Math.PI/1.1)) });
      }, 80);
    }
```

- [ ] **Step 3: Apply the text-size multiplier to beat text**

Find where `beatMain`/`beatSub` styles are computed in the render-values method (the existing font-size clamp values for the beat overlay). Add a helper and multiply:

```js
      textScale: {default:1, large:1.22, small:0.86}[(window.GMSettings ? GMSettings.get().textSize : 'default')] || 1,
```

Then, in the beat text style strings, change any hardcoded `font-size:clamp(22px,3.4vw,40px)`-style value to incorporate `textScale` — e.g. `'font-size:calc(clamp(22px,3.4vw,40px) * '+textScale+');'` for the main beat line and the equivalent for the sub line. Apply the same pattern to any other player-facing narrative text sized this way in the file.

- [ ] **Step 4: Manual verification**

Set Reduce Motion on via Prologue's Options screen, load Entry Hall, trigger `wake`, confirm the vignette holds steady instead of pulsing. Cycle Text Size to Large, confirm beat text visibly grows; cycle to Small, confirm it shrinks.

- [ ] **Step 5: Commit**

```bash
git add "The Games Master - Entry Hall.dc.html"
git commit -m "Entry Hall: respect Reduce Motion (static headache vignette) and Text Size (beat text scale)"
```

---

### Task 8: Prologue — apply Text Size to its own beat/subtitle text

**Files:**
- Modify: `The Games Master - Prologue.dc.html`

- [ ] **Step 1: Add the same `textScale` computed value** (same pattern as Task 7 Step 3) to Prologue's render-values method, and apply it to the walk beat's `beatMain`/`beatSub` font-size styles (the block under `<!-- ===== WALK HUD ===== -->`) and the cold-open text style.

- [ ] **Step 2: Manual verification**

Set Text Size to Large in Options, start a new game, confirm the cold-open lines and walk beats render larger; set to Small, confirm they shrink.

- [ ] **Step 3: Commit**

```bash
git add "The Games Master - Prologue.dc.html"
git commit -m "Prologue: apply Text Size setting to cold-open and walk beat text"
```

---

### Task 9: Test Harness updates and full suite verification

**Files:**
- Modify: `The Games Master - Test Harness.dc.html` (POI count assertion)
- Modify: `The Games Master - Entry Hall.dc.html` (if the POI count in a dev-facing assertion needs bumping)

**Context:** Entry Hall gained one new POI (the invitation) in Task 3. The harness's hardcoded POI-count assertion needs to move up by one, matching the same kind of update made during the mansion-intro-polish pass.

- [ ] **Step 1: Find and update the POI-count assertion**

Locate the Test Harness assertion checking Entry Hall's `pois` count (search for `pois===` in the harness file) and increment the expected number by 1.

- [ ] **Step 2: Run the full test suite**

Run: `npm test`
Expected: all existing assertions pass, including the updated POI count.

- [ ] **Step 3: Commit**

```bash
git add "The Games Master - Test Harness.dc.html"
git commit -m "Test Harness: update Entry Hall POI count for the new invitation POI"
```

---

### Task 10: Final whole-implementation review

Not a code task — dispatch a code-quality reviewer subagent (or do this directly) across all files touched by Tasks 1-9 together, checking specifically:
- `GMDoubt`/`GMSettings` are included via `<script>` tag in every file that references them (Prologue, Entry Hall, Parlor) — a missing include is a silent `window.GMDoubt is undefined` failure, not a loud one, since every call site already guards with `window.GMDoubt ? ... : ...`.
- The one-time letter insert in Parlor truly only fires once per session, verified by actually forcing two consecutive wins via the dev bridge, not just by reading the code.
- Reduce Motion and Text Size actually take effect live (re-render triggered) rather than only on next page load — since these are read via `GMSettings.get()` inside render-values methods rather than stored in component state, confirm the component actually re-renders after `onInput`/toggle clicks (`forceUpdate()` is called in each handler above — verify this is actually wired, not just written in the plan).
- Full `npm test` passes one final time after all tasks are integrated.
