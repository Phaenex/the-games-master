// Procedural Shut-the-Box boards (needs window.THREE + window.GMShutBox).
// Two boards facing each other on a table; tiles hinge shut about their near edge.
(function (root) {
  function buildBoard(THREE, opts) {
    opts = opts || {};
    const logic = root.GMShutBox;
    if (!logic) throw new Error('GMShutBox logic missing — load gm-shutbox-logic.js first');
    const g = new THREE.Group();
    const wood = new THREE.MeshStandardMaterial({ color: opts.wood || '#2a1e14', roughness: 1 });
    const woodDark = new THREE.MeshStandardMaterial({ color: opts.woodDark || '#16100a', roughness: 1 });
    const bone = new THREE.MeshStandardMaterial({ color: opts.bone || '#cfc6b0', roughness: 0.85 });

    // tray
    const tray = new THREE.Mesh(new THREE.BoxGeometry(2.4, 0.08, 1.15), wood);
    tray.position.y = 0.04;
    g.add(tray);
    const lip = new THREE.Mesh(new THREE.BoxGeometry(2.5, 0.12, 1.25), woodDark);
    lip.position.y = 0.02;
    g.add(lip);

    const tiles = {};
    const tileW = 0.22, tileH = 0.08, tileD = 0.85, gap = 0.04;
    const startX = -((9 * tileW + 8 * gap) / 2) + tileW / 2;

    logic.TILES.forEach((n, i) => {
      const tg = new THREE.Group();
      const x = startX + i * (tileW + gap);
      // hinge at +z edge of tray (near the owning player)
      tg.position.set(x, 0.1, tileD / 2 - 0.05);
      const body = new THREE.Mesh(new THREE.BoxGeometry(tileW, tileH, tileD), bone);
      body.position.set(0, 0, -tileD / 2);
      tg.add(body);
      // number — high-contrast stamp on TOP + near face (top-only vanished in side reading shots)
      const c = document.createElement('canvas'); c.width = 128; c.height = 128;
      const ctx = c.getContext('2d');
      ctx.fillStyle = '#f0e8d4'; ctx.fillRect(0, 0, 128, 128);
      ctx.strokeStyle = '#2a1a0c'; ctx.lineWidth = 6; ctx.strokeRect(8, 8, 112, 112);
      ctx.fillStyle = '#1a1008'; ctx.font = 'bold 72px Georgia, serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      ctx.fillText(String(n), 64, 70);
      const tex = new THREE.CanvasTexture(c);
      if (THREE.sRGBEncoding != null) tex.encoding = THREE.sRGBEncoding;
      const numMat = new THREE.MeshBasicMaterial({ map: tex, fog: false });
      const numTop = new THREE.Mesh(new THREE.PlaneGeometry(tileW * 0.85, tileW * 0.85), numMat);
      numTop.rotation.x = -Math.PI / 2;
      numTop.position.set(0, tileH / 2 + 0.008, -tileD / 2);
      tg.add(numTop);
      const numFace = new THREE.Mesh(new THREE.PlaneGeometry(tileW * 0.75, tileH * 0.9), numMat.clone());
      numFace.position.set(0, 0, 0.01);
      tg.add(numFace);

      tg.userData.tile = n;
      tg.userData.open = true;
      tg.userData.setOpen = function (open) {
        tg.userData.open = !!open;
        // shut = folded flat away from player (rotate about hinge toward -z tray)
        tg.rotation.x = open ? 0 : -Math.PI / 2 * 0.95;
      };
      g.add(tg);
      tiles[n] = tg;
    });

    g.userData.tiles = tiles;
    g.userData.syncFromBox = function (box) {
      logic.TILES.forEach((n) => { if (tiles[n]) tiles[n].userData.setOpen(!!box.open[n]); });
    };
    g.userData.syncFromBox(logic.freshBox());
    return g;
  }

  // Pair of boards for head-to-head: player near +z, Aldric near -z, table at origin.
  function buildMatch(THREE, opts) {
    const rootG = new THREE.Group();
    const player = buildBoard(THREE, opts);
    player.position.set(0, 0, 0.55);
    const aldric = buildBoard(THREE, opts);
    aldric.rotation.y = Math.PI;
    aldric.position.set(0, 0, -0.55);
    rootG.add(player); rootG.add(aldric);
    rootG.userData.playerBoard = player;
    rootG.userData.aldricBoard = aldric;
    return rootG;
  }

  const api = { buildBoard, buildMatch };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  root.GMShutBoxBoard = api;
})(typeof window !== 'undefined' ? window : globalThis);
