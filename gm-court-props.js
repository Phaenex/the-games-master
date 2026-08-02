// Court room procedural props — drop into The Games Master - Court.dc.html later.
// Gavel deliberately procedural (matches manor low-poly; Poly Pizza download was blocked).
// Requires window.THREE. Returns groups you add to a scene.
(function (root) {
  function mat(THREE, hex, rough) {
    return new THREE.MeshStandardMaterial({ color: hex, roughness: rough == null ? 0.85 : rough, metalness: 0 });
  }
  function metal(THREE, hex, metalness, rough) {
    return new THREE.MeshStandardMaterial({ color: hex, roughness: rough == null ? 0.4 : rough, metalness: metalness == null ? 0.75 : metalness });
  }

  // Gold → dull bronze tarnish driven by `gavel.userData.setTarnish(0..1)` each frame.
  function buildGavel(THREE, opts) {
    opts = opts || {};
    const g = new THREE.Group();
    const wood = mat(THREE, opts.handleColor || '#2a1e12', 0.95);
    const gold = metal(THREE, '#d4b45a', 0.8, 0.35);
    const dull = new THREE.Color('#5a4a2a');
    const bright = gold.color.clone();

    const handle = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.04, 0.85, 8), wood);
    handle.rotation.z = Math.PI / 2;
    handle.position.set(0.1, 0, 0);
    g.add(handle);

    const head = new THREE.Mesh(new THREE.CylinderGeometry(0.11, 0.11, 0.32, 10), gold);
    head.rotation.z = Math.PI / 2;
    head.position.set(-0.28, 0, 0);
    g.add(head);

    const band = new THREE.Mesh(new THREE.TorusGeometry(0.12, 0.02, 6, 12), metal(THREE, '#b89840', 0.7, 0.45));
    band.rotation.y = Math.PI / 2;
    band.position.set(-0.28, 0, 0);
    g.add(band);

    // sound block
    const block = new THREE.Mesh(new THREE.CylinderGeometry(0.22, 0.24, 0.1, 16), mat(THREE, '#1a140c', 1));
    block.position.set(0.55, -0.22, 0);
    g.add(block);

    g.userData.headMat = gold;
    g.userData.setTarnish = function (t) {
      const k = Math.max(0, Math.min(1, t));
      gold.color.copy(bright).lerp(dull, k);
      gold.metalness = 0.8 - k * 0.55;
      gold.roughness = 0.35 + k * 0.5;
    };
    g.userData.setTarnish(0);
    return g;
  }

  // Three wax seals for the argument HUD (can also be DOM — this is a 3D fallback for bench dressing).
  function buildWaxSeal(THREE, cracked) {
    const g = new THREE.Group();
    const wax = mat(THREE, cracked ? '#4a1810' : '#6e1c14', 0.9);
    const disc = new THREE.Mesh(new THREE.CylinderGeometry(0.14, 0.15, 0.04, 16), wax);
    g.add(disc);
    if (cracked) {
      const crack = new THREE.Mesh(new THREE.BoxGeometry(0.02, 0.05, 0.28), mat(THREE, '#1a0806', 1));
      crack.rotation.y = 0.3;
      g.add(crack);
    } else {
      const stamp = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, 0.02, 8), metal(THREE, '#c4a050', 0.6, 0.5));
      stamp.position.y = 0.03;
      g.add(stamp);
    }
    return g;
  }

  // Dust sheet for Shut the Box portrait frames — plane + slightly translucent linen.
  function buildDustSheet(THREE, w, h) {
    const c = document.createElement('canvas');
    c.width = 128; c.height = 128;
    const x = c.getContext('2d');
    x.fillStyle = '#c8c0a8'; x.fillRect(0, 0, 128, 128);
    x.fillStyle = 'rgba(0,0,0,0.06)';
    for (let i = 0; i < 40; i++) x.fillRect(Math.random() * 128, Math.random() * 128, 8 + Math.random() * 20, 2);
    const tex = new THREE.CanvasTexture(c);
    if (THREE.sRGBEncoding != null) tex.encoding = THREE.sRGBEncoding;
    const m = new THREE.MeshStandardMaterial({ map: tex, color: '#b8b09a', roughness: 1, transparent: true, opacity: 0.92, side: THREE.DoubleSide });
    const mesh = new THREE.Mesh(new THREE.PlaneGeometry(w || 1.5, h || 2.0), m);
    return mesh;
  }

  const api = { buildGavel, buildWaxSeal, buildDustSheet };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  root.GMCourtProps = api;
})(typeof window !== 'undefined' ? window : globalThis);
