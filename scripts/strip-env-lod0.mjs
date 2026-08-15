#!/usr/bin/env node
// Strip Victorian Mansion Env modular GLBs to a single LOD0 mesh (no hulls / LOD1–3 / textures).
// Full Modular/*.glb are 31–45MB and stall the browser; web/*_LOD0.glb are ~0.2–0.4MB.
import { readFile, writeFile, mkdir } from 'fs/promises';
import { basename, join, dirname } from 'path';

async function stripToLod0(src, dest) {
  const buf = await readFile(src);
  const jsonLen = buf.readUInt32LE(12);
  const json = JSON.parse(buf.toString('utf8', 20, 20 + jsonLen));
  const binStart = 20 + jsonLen;
  const bin = buf.subarray(binStart + 8, binStart + 8 + buf.readUInt32LE(binStart));

  let keepMeshIdx = null;
  let via = 'LOD0';
  for (const n of json.nodes || []) {
    if ((n.name || '').includes('LOD0') && n.mesh != null) { keepMeshIdx = n.mesh; break; }
  }
  if (keepMeshIdx == null) {
    // No LOD-named node: the pick below is a name guess with no LOD awareness at all, so the
    // output can be full detail. Reported as `via` so a caller can tell the two paths apart.
    via = 'name-guess';
    keepMeshIdx = (json.meshes || []).findIndex((m) => {
      const n = m.name || '';
      return !/convex/i.test(n) && !/\.\d{3}$/.test(n);
    });
  }
  if (keepMeshIdx < 0) throw new Error('no mesh in ' + src);

  const mesh = JSON.parse(JSON.stringify(json.meshes[keepMeshIdx]));
  const usedAcc = new Set();
  const usedMat = new Set();
  for (const p of mesh.primitives || []) {
    for (const a of Object.values(p.attributes || {})) usedAcc.add(a);
    if (p.indices != null) usedAcc.add(p.indices);
    if (p.material != null) usedMat.add(p.material);
  }
  const usedBV = new Set();
  for (const ai of usedAcc) {
    const acc = json.accessors[ai];
    if (acc?.bufferView != null) usedBV.add(acc.bufferView);
  }
  const bvMap = new Map();
  const newBVs = [];
  const parts = [];
  let offset = 0;
  for (const old of [...usedBV].sort((a, b) => a - b)) {
    const bv = { ...json.bufferViews[old] };
    const start = bv.byteOffset || 0;
    const len = bv.byteLength;
    const slice = bin.subarray(start, start + len);
    const pad = (4 - (len % 4)) % 4;
    bv.buffer = 0;
    bv.byteOffset = offset;
    parts.push(slice);
    if (pad) parts.push(Buffer.alloc(pad));
    bvMap.set(old, newBVs.length);
    newBVs.push(bv);
    offset += len + pad;
  }
  const accMap = new Map();
  const newAcc = [];
  for (const old of [...usedAcc].sort((a, b) => a - b)) {
    const a = { ...json.accessors[old] };
    if (a.bufferView != null) a.bufferView = bvMap.get(a.bufferView);
    accMap.set(old, newAcc.length);
    newAcc.push(a);
  }
  for (const p of mesh.primitives || []) {
    for (const [k, v] of Object.entries(p.attributes || {})) p.attributes[k] = accMap.get(v);
    if (p.indices != null) p.indices = accMap.get(p.indices);
  }
  const matMap = new Map();
  const newMats = [];
  for (const old of [...usedMat].sort((a, b) => a - b)) {
    matMap.set(old, newMats.length);
    const mat = JSON.parse(JSON.stringify(json.materials[old]));
    if (mat.pbrMetallicRoughness) {
      delete mat.pbrMetallicRoughness.baseColorTexture;
      delete mat.pbrMetallicRoughness.metallicRoughnessTexture;
    }
    delete mat.normalTexture;
    delete mat.occlusionTexture;
    delete mat.emissiveTexture;
    newMats.push(mat);
  }
  for (const p of mesh.primitives || []) {
    if (p.material != null) p.material = matMap.get(p.material);
  }

  const out = {
    asset: json.asset || { version: '2.0' },
    scenes: [{ nodes: [0] }],
    scene: 0,
    nodes: [{ name: (json.nodes || []).find((n) => n.mesh === keepMeshIdx)?.name || mesh.name || 'LOD0', mesh: 0 }],
    meshes: [mesh],
    accessors: newAcc,
    bufferViews: newBVs,
    buffers: [{ byteLength: offset }],
    materials: newMats,
  };
  const jsonStr = JSON.stringify(out);
  const jsonPad = (4 - (jsonStr.length % 4)) % 4;
  const jsonBuf = Buffer.concat([Buffer.from(jsonStr, 'utf8'), Buffer.alloc(jsonPad, 0x20)]);
  const binBuf = Buffer.concat(parts);
  const binPad = (4 - (binBuf.length % 4)) % 4;
  const binPadded = binPad ? Buffer.concat([binBuf, Buffer.alloc(binPad)]) : binBuf;
  const total = 12 + 8 + jsonBuf.length + 8 + binPadded.length;
  const outBuf = Buffer.alloc(total);
  outBuf.write('glTF', 0);
  outBuf.writeUInt32LE(2, 4);
  outBuf.writeUInt32LE(total, 8);
  outBuf.writeUInt32LE(jsonBuf.length, 12);
  outBuf.writeUInt32LE(0x4E4F534A, 16);
  jsonBuf.copy(outBuf, 20);
  const bo = 20 + jsonBuf.length;
  outBuf.writeUInt32LE(binPadded.length, bo);
  outBuf.writeUInt32LE(0x004E4942, bo + 4);
  binPadded.copy(outBuf, bo + 8);
  await mkdir(dirname(dest), { recursive: true });
  await writeFile(dest, outBuf);
  return { src: basename(src), outMB: +(outBuf.length / 1e6).toFixed(2), mesh: mesh.name, via };
}

const base = 'assets/models/unity/victorian-mansion-environment/BefourStudios/VictorianMansionEnvironment/Art/Meshes/Modular';
const outDir = 'assets/models/unity/victorian-mansion-environment/web';
const files = process.argv.slice(2).length
  ? process.argv.slice(2)
  : ['SM_FenceSmall.glb', 'SM_Fence.glb', 'SM_Wall_Door.glb', 'SM_Wall_Standard.glb'];

let guessed = 0;
for (const f of files) {
  const src = f.includes('/') ? f : join(base, f);
  const dest = join(outDir, basename(src).replace('.glb', '') + '_LOD0.glb');
  const r = await stripToLod0(src, dest);
  console.log(JSON.stringify(r));
  if (r.via !== 'LOD0') {
    guessed++;
    console.error(`WARN ${r.src}: no *LOD0* node — kept "${r.mesh}" by name guess, so ${dest} may be full detail`);
  }
}
if (guessed) {
  console.error(`strip-env-lod0: ${guessed} file(s) had no LOD0 node; check those outputs before serving them`);
  process.exitCode = 1;
}
