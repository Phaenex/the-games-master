// Shared Unity-GLB placement helper for The Games Master scenes (Three r128).
(function (root) {
  function standIfFlat(obj, THREE) {
    let b = new THREE.Box3().setFromObject(obj);
    let sz = new THREE.Vector3();
    b.getSize(sz);
    if (sz.y < 0.5 && Math.max(sz.x, sz.z) > sz.y) {
      obj.rotation.x = -Math.PI / 2;
      obj.updateMatrixWorld(true);
      b = new THREE.Box3().setFromObject(obj);
      b.getSize(sz);
    }
    return sz;
  }

  function placeAt(obj, THREE, x, y, z, ry, targetH) {
    const sz = standIfFlat(obj, THREE);
    const h = targetH || Math.max(sz.y, 0.01);
    obj.scale.setScalar(h / Math.max(sz.y, 0.001));
    obj.rotation.y += ry || 0;
    obj.updateMatrixWorld(true);
    const b2 = new THREE.Box3().setFromObject(obj);
    obj.position.set(
      x - (b2.min.x + b2.max.x) / 2,
      (y != null ? y : 0) - b2.min.y,
      z - (b2.min.z + b2.max.z) / 2
    );
    return obj;
  }

  function loadGLB(THREE, url, onOk, onFail) {
    if (!THREE.GLTFLoader) {
      if (onFail) onFail();
      return;
    }
    try {
      const loader = new THREE.GLTFLoader();
      loader.load(url, (gltf) => onOk(gltf.scene), undefined, () => {
        if (onFail) onFail();
      });
    } catch (e) {
      if (onFail) onFail();
    }
  }

  /** Apply Modular Victorian albedo maps (Three FBX convert drops them). */
  const MOD_TEX = 'assets/models/unity/modular-victorian-interior-mansion/_tex/';
  let _modWood = null, _modPlaster = null;
  function modularTextures(THREE) {
    if (_modWood) return { wood: _modWood, plaster: _modPlaster };
    const L = new THREE.TextureLoader();
    const woodUrl = MOD_TEX + 'wood.png';
    const plasterUrl = MOD_TEX + 'plaster.png';
    _modWood = L.load(woodUrl);
    _modPlaster = L.load(plasterUrl);
    [_modWood, _modPlaster].forEach((t) => {
      if (!t) return;
      if ('encoding' in t) t.encoding = THREE.sRGBEncoding;
      t.wrapS = t.wrapT = THREE.RepeatWrapping;
      t.needsUpdate = true;
    });
    return { wood: _modWood, plaster: _modPlaster };
  }
  function dressModular(root, THREE) {
    const { wood, plaster } = modularTextures(THREE);
    root.traverse((o) => {
      if (!o.isMesh || !o.material) return;
      const on = String(o.name || '').toLowerCase();
      if (on.includes('convex') || on.includes('lod1') || on.includes('lod2') || on.includes('lod3')) {
        o.visible = false;
        return;
      }
      // Picture frames / canvas art / ghosts — never force #1e1610 (that made black voids)
      if (/picture|frame|ghost|canvas|paint|portrait|art/.test(on)) return;
      const ms = Array.isArray(o.material) ? o.material : [o.material];
      ms.forEach((m) => {
        if (!m) return;
        const n = String(m.name || o.name || '').toLowerCase();
        if (/picture|frame|ghost|canvas|paint|portrait|art/.test(n)) return;
        if (n.includes('wood') || n.includes('panel') || n.includes('wainscot') || n.includes('oak')) {
          if (wood) { m.map = wood; m.color.setHex(0x8a6e52); }
          else m.color.setHex(0x4a3424);
          if ('roughness' in m) m.roughness = 0.85;
        } else if (n.includes('plaster') || n.includes('wallpaper') || n.includes('wall_') || n.includes('damask')) {
          if (plaster) { m.map = plaster; m.color.setHex(0xa89880); }
          else m.color.setHex(0x5a4a3a);
          if ('roughness' in m) m.roughness = 0.95;
        } else if (n.includes('wall') || on.includes('wall')) {
          if (wood) { m.map = wood; m.color.setHex(0x7a6048); }
          else m.color.setHex(0x4a3424);
          if ('roughness' in m) m.roughness = 0.9;
        } else if (/chair|table|couch|sofa|lamp|book|shelf|plant|clock|cupboard|door/.test(n + on)) {
          // furniture: keep maps, just kill pale #ccc default from FBX convert
          if (m.color && m.color.r > 0.7 && m.color.g > 0.7 && m.color.b > 0.7 && !m.map) {
            m.color.setHex(0x3a2c20);
          }
        } else {
          m.color.setHex(0x2a2018);
          if ('roughness' in m) m.roughness = 0.92;
        }
        m.needsUpdate = true;
      });
    });
    return root;
  }

  /** spots: [x,z,ry?] or [x,y,z,ry?] when length>=4 and y looks like height */
  function placeMany(THREE, scene, url, spots, targetH, onFail) {
    loadGLB(THREE, url, (proto) => {
      dressModular(proto, THREE);
      spots.forEach((s) => {
        let x, y, z, ry;
        if (s.length >= 4) {
          x = s[0]; y = s[1]; z = s[2]; ry = s[3];
        } else {
          x = s[0]; y = 0; z = s[1]; ry = s[2] || 0;
        }
        const c = proto.clone(true);
        placeAt(c, THREE, x, y, z, ry, targetH);
        scene.add(c);
      });
    }, onFail);
  }

  root.GMUnityProps = { standIfFlat, placeAt, loadGLB, placeMany, dressModular };
})(typeof window !== 'undefined' ? window : globalThis);
