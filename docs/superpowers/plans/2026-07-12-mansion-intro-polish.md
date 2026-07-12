# Mansion & Intro Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 9 named portrait backstories tied to the Entry Hall's existing ledger, a missing narrative beat showing the mansion's interior during the Prologue's door-to-blackout transition, and a pulsing vignette during Entry Hall's wake-up sequence.

**Architecture:** All three changes are additive within the existing `dc-runtime` component pattern (a single `Component extends DCLogic` class per `.dc.html` file, state-driven inline styles, no build step). Portraits reuse the existing `buildPOIs()`/`examine` pattern verbatim. The door glimpse reuses the existing `speak()` narrative-beat mechanism already used for the walk-up beats. The wake vignette follows the existing `fadeStyle`/`goldStyle` pattern (a state value driving a fixed-position overlay's opacity).

**Tech Stack:** Vanilla JS, Three.js r128, the project's own `dc-runtime` (`support.js`) — no npm packages needed for this feature. Verification uses the existing `npm test` (Playwright-driven Test Harness) plus manual browser checks for the two narrative/visual additions that aren't covered by automated assertions.

**Spec:** `docs/superpowers/specs/2026-07-12-mansion-intro-polish-design.md`

---

### Task 1: Add 9 portrait POIs and update the ledger (TDD — bump the test first)

**Files:**
- Modify: `The Games Master - Test Harness.dc.html` (POI count assertion)
- Modify: `The Games Master - Entry Hall.dc.html` (`buildPOIs()`, ledger POI's `es` text)

- [ ] **Step 1: Update the Test Harness assertion to expect 20 POIs (will fail until Task 1 Step 3)**

In `The Games Master - Test Harness.dc.html`, find this line inside the `'Entry Hall'` spec's `tests` array:

```js
        { name:'scene builds with 11 points of interest', fn:async(w,GM,C,H)=>{ if(!await H.waitFor(()=>GM.getState().sceneReady && GM.getState().pois===11,5000)) throw new Error('pois='+GM.getState().pois+' ready='+GM.getState().sceneReady); return true; } },
```

Replace it with:

```js
        { name:'scene builds with 20 points of interest', fn:async(w,GM,C,H)=>{ if(!await H.waitFor(()=>GM.getState().sceneReady && GM.getState().pois===20,5000)) throw new Error('pois='+GM.getState().pois+' ready='+GM.getState().sceneReady); return true; } },
```

- [ ] **Step 2: Run the test suite, confirm it now fails on this assertion**

Run: `npm test`
Expected output includes:
```
Entry Hall
✗ scene builds with 20 points of interest  pois=11 ready=true
```
(The Prologue and Parlor groups should still be all-green — only this one Entry Hall assertion should be red, since the portraits don't exist yet.)

- [ ] **Step 3: Add the 9 portrait POI entries to `buildPOIs()`**

In `The Games Master - Entry Hall.dc.html`, find the `buildPOIs()` method. The existing faceless-portrait entry looks like this (do not change it):

```js
      { x:6.6, z:-2, r:4.0, type:'murmur',
        m:'One portrait on the wall had no face yet.', s:'Only primed canvas, waiting. Room, perhaps, for whoever left the table last.' },
```

Immediately after that entry (and before the `{ x:-4.4, z:-23.8, ...` velvet rope entry), insert these 9 new entries:

```js
      { x:-7.68, z:10, r:3.2, type:'examine', verb:'Read the nameplate',
        em:'I read the small plate beneath the frame.', es:'EDWIN MARR. The paint has him mid-turn, same as all the rest, but his eyes haven’t quite caught up to his shoulders — he’s still watching the table over his own back. A tell, I’d learn later, cheats give right before they play the impossible card: they look anywhere but at their hands. Edwin must have noticed too. Too late for it to save him.' },
      { x:-7.68, z:3, r:3.2, type:'examine', verb:'Read the nameplate',
        em:'I read the small plate beneath the frame.', es:'CASPIAN DUFRESNE, self-declared the finest card hand in three counties, according to the plate — though the plate is the only place that claim survives. He came, I’d guess, the way men like that always come to a game they can’t lose: certain, cheerful, already composing the story he’d tell after. There’s no second portrait of him. There didn’t need to be.' },
      { x:-7.68, z:-4, r:3.2, type:'examine', verb:'Read the nameplate',
        em:'I read the small plate beneath the frame.', es:'HALVARD PIKE. An actuary, or so I’d guess from the ledger’s second column, where someone had noted his trade in a hand too neat to be his own. He’d have come with odds worked out to the decimal, certain a fair game has no cheat that arithmetic can’t survive. What he hadn’t worked out, I’d learn: the game was never fair, and losing was never really the point. Winning was going to cost him something the numbers didn’t have a column for.' },
      { x:-7.68, z:-13, r:3.2, type:'examine', verb:'Read the nameplate',
        em:'I read the small plate beneath the frame.', es:'SOLVEIG HALE. A debt of her own on the ledger’s third column, in the same neat foreign hand — a sister’s name beside it, and a hospital I didn’t recognize. I understood her before I read a word further. I did not need a clue from her portrait. I only needed to know I wasn’t the first person this house had made a bargain with, and wouldn’t be the last.' },
      { x:-7.68, z:-21, r:3.2, type:'examine', verb:'Read the nameplate',
        em:'I read the small plate beneath the frame.', es:'THEO GALL. No trade beside his name, no debt, nothing the ledger thought worth recording — which told me plenty on its own. A man who comes to a game like this owing nothing usually comes to take something instead. I don’t know what Theo tried. I know only that his portrait, like all the rest, hangs facing away from the table, and that his shoulders, even in paint, look like a man still walking backward out of a room.' },
      { x:7.68, z:12, r:3.2, type:'examine', verb:'Read the nameplate',
        em:'I read the small plate beneath the frame.', es:'BARNABY QUILL. Arrived, by the look of him, three drinks into a party he hadn’t been invited to and was delighted to have found anyway — the only face on this wall with anything like a smile still on it. I almost envied him that, until I noticed what the smile was aimed at: not the viewer. The stairs. Whatever he found up there, going up laughing, I don’t think he came down the same way. There are more doors in this house than the one I walked through.' },
      { x:7.68, z:5, r:3.2, type:'examine', verb:'Read the nameplate',
        em:'I read the small plate beneath the frame.', es:'IMOGEN THALE. No trade, no debt column, no note at all beside her name — whoever kept this ledger either didn’t know her reasons or thought better of writing them down. I found I didn’t want to guess. Some of the losing, I think, isn’t mine to read.' },
      { x:7.68, z:-11, r:3.2, type:'examine', verb:'Read the nameplate',
        em:'I read the small plate beneath the frame.', es:'CONSTANCE AUBREY-LOCKE. Old money, by the double-barreled name and the fur still painted at her throat — the kind of guest, I’d guess, who came the way other people go hunting: for the thrill of a thing that could, in theory, kill you, with none of the actual risk expected to land. In the portrait her chin is lifted a fraction too high. Aldric lifts his the same fraction, I’d notice later, in the half-second before he plays a card he has no right to be holding.' },
      { x:7.68, z:-19, r:3.2, type:'examine', verb:'Read the nameplate',
        em:'I read the small plate beneath the frame.', es:'The plate’s been rubbed half through by hands other than mine — only PERCIVAL survives it. Whoever he was, someone came back to this one more than the rest. I couldn’t blame them. There’s something almost comic in his portrait, chin low, shoulders slack, a man who’d clearly lost everything worth losing before he ever sat down at this table — for whom the stakes must have felt, absurdly, like a relief. I hoped, looking at him, that losing wasn’t the same as the ledger made it look. I was less and less sure, the longer I looked.' },
```

These positions match the existing `addPortrait` calls exactly: left wall (`x=-7.68`) at `z=[10,3,-4,-13,-21]`, right wall (`x=7.68`) at `z=[12,5,-11,-19]` (right wall's `z=-2` stays the existing faceless portrait, untouched). `r:3.2` keeps each portrait's examine radius from overlapping its neighbors (closest gap between any two portrait z-positions is 7 units; `2 × 3.2 = 6.4 < 7`).

- [ ] **Step 4: Update the ledger's `es` text to name these guests**

Find this line (the existing ledger POI):

```js
        em:'I turned the ledger toward the light.', es:'Names, down the page. Guests before me — every one crossed neatly through. At the foot, a blank line left open. My width, exactly.' },
```

Replace it with:

```js
        em:'I turned the ledger toward the light.', es:'Names, down the page, in a hand too neat to be any one guest’s own. Marr. Dufresne. Pike. Hale. Gall. Quill. Thale. Aubrey-Locke. A ninth, the ink gone soft where someone kept touching it, worn past reading. Every one crossed neatly through. At the foot, a blank line left open. My width, exactly.' },
```

- [ ] **Step 5: Run the test suite, confirm it now passes**

Run: `npm test`
Expected: `30 passed, 0 failed` (same total — this renames and re-thresholds one existing test, it doesn't add a new one; the Entry Hall group stays `8/8`, now with the correct 20-POI count).

- [ ] **Step 6: Manually verify in a browser**

```bash
npx serve .
```
Open `The Games Master - Entry Hall.dc.html`, open the browser console, run:
```js
window.__GM.goTo('start')
```
Walk to each of the 9 new portrait positions (left wall around x=-7.68, right wall around x=7.68, at the z-values above), press `E` at each, and confirm the text displays correctly with no console errors. Then walk to the ledger (around x=19.5, z=-13) and press `E` to confirm its updated text.

- [ ] **Step 7: Commit**

```bash
git add "The Games Master - Entry Hall.dc.html" "The Games Master - Test Harness.dc.html"
git commit -m "Add 9 named portrait backstories tied to the ledger"
```

---

### Task 2: Door → glimpse → dark beat in the Prologue

**Files:**
- Modify: `The Games Master - Prologue.dc.html`

- [ ] **Step 1: Find and replace the arrival-hold block**

Find this line (inside the `mode==='arrival' && this.arriving` block, in the `!this.knockStarted` branch):

```js
        if (this.arrDoor>=1){ this.arrHold=(this.arrHold||0)+dt; if(this.arrHold>0.9){ this.knockStarted=true; this.knockT=0; this.knockBaseY=cam.position.y; this.knockBaseZ=cam.position.z; } }
```

Replace it with:

```js
        if (this.arrDoor>=1){
          if (!this._glimpseSaid) { this._glimpseSaid=true; this.setState({ beatMain:'Beyond the widening gap, gold light and a floor laid out in black and white.', beatSub:'A staircase climbing into the dark above it, and something enormous hanging dead center of it all, swaying very slightly, though no one had touched it.', beatOn:true, hintOn:false }); }
          this.arrHold=(this.arrHold||0)+dt; if(this.arrHold>2.0){ this.knockStarted=true; this.knockT=0; this.knockBaseY=cam.position.y; this.knockBaseZ=cam.position.z; }
        }
```

This fires once (guarded by `this._glimpseSaid`) the moment the doors finish opening. **Correction from the original plan draft:** this originally called `this.speak(main, sub, holdMs)`, incorrectly assuming Prologue shared Entry Hall's `speak()` helper — it doesn't; `speak()` is only defined in the Entry Hall file. Prologue's own `checkBeats()` sets beat text via a direct `setState({beatMain, beatSub, beatOn:true, hintOn:false})` with no auto-clear timer, so the fix here follows that existing convention instead. This was caught by a code-quality review (the call would have thrown `TypeError: this.speak is not a function` on every arrival) and confirmed fixed via a live browser trace: the glimpse now fires at the correct moment with the correct text, stays visible for the full hold window, and the sequence completes with zero console errors. The hold window before the grab triggers is extended from 0.9s to 2.0s so there's time to read the beat before the blackout.

- [ ] **Step 2: Reset `_glimpseSaid` wherever the arrival state resets, so replaying via the dev menu re-fires the beat**

Search the file for this exact line (it appears twice — once in `devGoTo`'s `'walk'||'door'||'knockout'` branch, once elsewhere in the reset flow):

```js
      this.mode='walk'; this.walkEnabled=true; this.knockStarted=false; this.arriving=false; this.arrDoor=0; this.arrHold=0;
```

Replace **both occurrences** with:

```js
      this.mode='walk'; this.walkEnabled=true; this.knockStarted=false; this.arriving=false; this.arrDoor=0; this.arrHold=0; this._glimpseSaid=false;
```

- [ ] **Step 3: Manually verify in a browser**

```bash
npx serve .
```
Open `The Games Master - Prologue.dc.html`, open the console, run:
```js
window.__GM.goTo('door')
```
Watch the arrival sequence play out. Confirm the glimpse text appears right as the doors finish opening and stays legible for about 2 seconds before the grab/blackout happens. Confirm no console errors.

- [ ] **Step 4: Run the full test suite to confirm no regression**

Run: `npm test`
Expected: `30 passed, 0 failed` (unchanged from Task 1 — this change doesn't alter any state the harness asserts on, since `phase` still reaches `'door'` the same way).

- [ ] **Step 5: Commit**

```bash
git add "The Games Master - Prologue.dc.html"
git commit -m "Add interior glimpse beat between the door opening and the blackout"
```

---

### Task 3: Wake-up headache vignette in the Entry Hall

**Files:**
- Modify: `The Games Master - Entry Hall.dc.html`

- [ ] **Step 1: Add the vignette div to the template**

Find this block in the template:

```html
  <!-- warm gold flood as you pass through the doors -->
  <div style="{{ goldStyle }}"></div>
  <!-- fade -->
  <div style="{{ fadeStyle }}"></div>
</div>
```

Replace it with:

```html
  <!-- warm gold flood as you pass through the doors -->
  <div style="{{ goldStyle }}"></div>
  <!-- headache vignette while waking -->
  <div style="{{ vignetteStyle }}"></div>
  <!-- fade -->
  <div style="{{ fadeStyle }}"></div>
</div>
```

- [ ] **Step 2: Add `headachePulse` to initial state**

Find:

```js
  state = { fade:1, beatOn:false, beatMain:'', beatSub:'', hintOn:false, promptOn:false, promptText:'', isArrival:false, gold:0, devOpen:false };
```

Replace with:

```js
  state = { fade:1, beatOn:false, beatMain:'', beatSub:'', hintOn:false, promptOn:false, promptText:'', isArrival:false, gold:0, devOpen:false, headachePulse:0 };
```

- [ ] **Step 3: Drive the pulse from `beginWake()`**

Find the full `beginWake()` method:

```js
  beginWake() {
    (this._wt||[]).forEach(clearTimeout); this._wt = [];
    this.waking = true; this.rising = false; this.walkEnabled = false; this.mode='hall';
    this.standY = 0.34; this.wakeRoll = 1.15; this.wakePitch = 0.52; this.yaw = -0.22; this.pitch = 0;
    if (this.cam) this.cam.position.set(1.4, 0.34, 15);
    this.setState({ fade:1, isArrival:false, hintOn:false, promptOn:false, beatOn:true,
      beatMain:'A door boomed shut behind me.', beatSub:'Then a bolt, thrown home — and after that, nothing worth keeping.' });
    const T=(ms,fn)=>this._wt.push(setTimeout(fn,ms));
    T(2500, ()=> this.setState({ fade:0.6, beatMain:'Cold. The marble had my cheek.', beatSub:'My skull kept a slow, enormous pulse. Somewhere far off, a fire cracked and settled.' }));
    T(4700, ()=> this.setState({ fade:0.82 }));
    T(5500, ()=> this.setState({ fade:0.2, beatMain:'How long had I been under?', beatSub:'Long enough to be arranged. Set down facing the room — the way you pose a thing you mean to use.' }));
    T(7800, ()=> { this.setState({ fade:0 }); this.rising = true; });
    T(11400,()=> { this.waking=false; this.rising=false; this.standY=1.7; this.wakeRoll=0; this.wakePitch=0; this.walkEnabled=true; this.setState({ beatOn:false, hintOn:true }); });
  }
```

Replace it with:

```js
  beginWake() {
    (this._wt||[]).forEach(clearTimeout); this._wt = [];
    if (this._pulseTick) clearInterval(this._pulseTick);
    this.waking = true; this.rising = false; this.walkEnabled = false; this.mode='hall';
    this.standY = 0.34; this.wakeRoll = 1.15; this.wakePitch = 0.52; this.yaw = -0.22; this.pitch = 0;
    if (this.cam) this.cam.position.set(1.4, 0.34, 15);
    this.setState({ fade:1, isArrival:false, hintOn:false, promptOn:false, beatOn:true,
      beatMain:'A door boomed shut behind me.', beatSub:'Then a bolt, thrown home — and after that, nothing worth keeping.' });
    const T=(ms,fn)=>this._wt.push(setTimeout(fn,ms));
    T(2500, ()=> this.setState({ fade:0.6, beatMain:'Cold. The marble had my cheek.', beatSub:'My skull kept a slow, enormous pulse. Somewhere far off, a fire cracked and settled.' }));
    T(4700, ()=> this.setState({ fade:0.82 }));
    T(5500, ()=> this.setState({ fade:0.2, beatMain:'How long had I been under?', beatSub:'Long enough to be arranged. Set down facing the room — the way you pose a thing you mean to use.' }));
    T(7800, ()=> { this.setState({ fade:0 }); this.rising = true; this._riseStart = Date.now(); });
    T(11400,()=> { this.waking=false; this.rising=false; this.standY=1.7; this.wakeRoll=0; this.wakePitch=0; this.walkEnabled=true; this.setState({ beatOn:false, hintOn:true, headachePulse:0 }); if (this._pulseTick) { clearInterval(this._pulseTick); this._pulseTick=null; } });
    this._pulseTick = setInterval(()=>{
      const decay = this.rising ? Math.max(0, 1 - (Date.now()-(this._riseStart||Date.now()))/3600) : 1;
      const t = Date.now()/1000;
      this.setState({ headachePulse: decay * (0.5+0.5*Math.sin(t*2*Math.PI/1.1)) });
    }, 80);
  }
```

- [ ] **Step 4: Clean up the interval on unmount**

Find:

```js
  componentWillUnmount() {
    if (this._raf) cancelAnimationFrame(this._raf);
    window.removeEventListener('keydown', this._kd); window.removeEventListener('keyup', this._ku);
    window.removeEventListener('resize', this._resize);
    if (this._t0) clearTimeout(this._t0);
    (this._wt||[]).forEach(clearTimeout);
    if (this._sayTimer) clearTimeout(this._sayTimer);
    if (this._devTick) clearInterval(this._devTick);
    window.removeEventListener('error', this._onErr);
  }
```

Replace with:

```js
  componentWillUnmount() {
    if (this._raf) cancelAnimationFrame(this._raf);
    window.removeEventListener('keydown', this._kd); window.removeEventListener('keyup', this._ku);
    window.removeEventListener('resize', this._resize);
    if (this._t0) clearTimeout(this._t0);
    (this._wt||[]).forEach(clearTimeout);
    if (this._sayTimer) clearTimeout(this._sayTimer);
    if (this._devTick) clearInterval(this._devTick);
    if (this._pulseTick) clearInterval(this._pulseTick);
    window.removeEventListener('error', this._onErr);
  }
```

- [ ] **Step 4b: Also clear `_pulseTick` in `devGoTo` (added after code review — not in the original plan draft)**

A code-quality review caught a real leak: `devGoTo(name)` (the dev-menu/console jump mechanism) resets `waking`/`rising`/`_wt` when jumping to any other phase, but didn't originally touch `_pulseTick` — so triggering the dev menu (or `window.__GM.goTo(...)`) while a wake sequence was still mid-flight left the interval running forever, pulsing the vignette at full amplitude indefinitely (since `decay` reads `1` once `rising` is false) and calling `setState` every 80ms with no end. Confirmed reproducible: calling `beginWake()` then immediately `devGoTo('start')` left `_pulseTick` truthy before the fix, `null` after.

Find the start of `devGoTo`:

```js
  devGoTo(name) {
    (this._wt||[]).forEach(clearTimeout); this._wt=[];
    this.waking=false; this.rising=false; this.standY=1.7; this.wakeRoll=0; this.wakePitch=0;
    const tp=(z)=>{ if(this.cam) this.cam.position.set(0,1.7,z); };
```

Replace with:

```js
  devGoTo(name) {
    (this._wt||[]).forEach(clearTimeout); this._wt=[];
    this.waking=false; this.rising=false; this.standY=1.7; this.wakeRoll=0; this.wakePitch=0;
    if (this._pulseTick) { clearInterval(this._pulseTick); this._pulseTick=null; }
    const tp=(z)=>{ if(this.cam) this.cam.position.set(0,1.7,z); };
```

This runs for every `devGoTo` destination (including `'wake'`, where `beginWake()` immediately clears-and-recreates its own interval right after — redundant but harmless, matching the idempotent-guard style already used elsewhere in this file).

- [ ] **Step 5: Add `vignetteStyle` to `renderVals()`**

Find:

```js
      fadeStyle: 'position:fixed;inset:0;z-index:45;background:#000;transition:opacity 0.9s ease;opacity:'+st.fade+';pointer-events:'+(st.fade>0.02?'auto':'none')+';',
      goldStyle: 'position:fixed;inset:0;z-index:44;pointer-events:none;background:radial-gradient(60% 55% at 50% 52%, rgba(255,226,150,0.96), rgba(240,176,82,0.5) 45%, rgba(120,70,20,0) 78%);opacity:'+(st.gold||0)+';transition:opacity .5s ease;',
      parlorHref: 'The Parlor - Playable Prototype.dc.html'
    };
  }
}
```

Replace with:

```js
      fadeStyle: 'position:fixed;inset:0;z-index:45;background:#000;transition:opacity 0.9s ease;opacity:'+st.fade+';pointer-events:'+(st.fade>0.02?'auto':'none')+';',
      goldStyle: 'position:fixed;inset:0;z-index:44;pointer-events:none;background:radial-gradient(60% 55% at 50% 52%, rgba(255,226,150,0.96), rgba(240,176,82,0.5) 45%, rgba(120,70,20,0) 78%);opacity:'+(st.gold||0)+';transition:opacity .5s ease;',
      vignetteStyle: 'position:fixed;inset:0;z-index:43;pointer-events:none;box-shadow:inset 0 0 '+(140+(st.headachePulse||0)*90)+'px '+(40+(st.headachePulse||0)*30)+'px rgba(0,0,0,'+(0.35+(st.headachePulse||0)*0.35)+');',
      parlorHref: 'The Parlor - Playable Prototype.dc.html'
    };
  }
}
```

- [ ] **Step 6: Manually verify in a browser**

```bash
npx serve .
```
Open `The Games Master - Entry Hall.dc.html` fresh (it auto-triggers `beginWake()` on load). Confirm you can see the screen edges darken and pulse rhythmically during the ~11 second wake sequence, fading out smoothly as the camera rises to standing, and that the existing "How long had I been under?" text is still fully readable (not obscured by the vignette). Confirm no console errors.

- [ ] **Step 7: Run the full test suite to confirm no regression**

Run: `npm test`
Expected: `30 passed, 0 failed` (unchanged — `headachePulse` isn't part of any assertion the harness makes).

- [ ] **Step 8: Commit**

```bash
git add "The Games Master - Entry Hall.dc.html"
git commit -m "Add pulsing vignette during the wake-up sequence"
```

---

### Task 4: Push and final full-suite verification

**Files:** none (verification only)

- [ ] **Step 1: Run the full test suite one final time**

Run: `npm test`
Expected: `30 passed, 0 failed`

- [ ] **Step 2: Push**

```bash
git push origin main
```

- [ ] **Step 3: Confirm the push landed**

```bash
git ls-remote https://github.com/Phaenex/the-games-master.git main
```
Expected: the printed commit hash matches `git log -1 --format=%H` run locally.
