// Quick model inspector: for each .glb/.gltf, list mesh names and an approximate local bounding box
// (union of every POSITION accessor min/max; ignores node TRS, so it's a rough local size — good
// enough to judge scale and contents). No external deps.
import { readFile, readdir } from 'fs/promises';
import { join, extname } from 'path';

async function walk(dir) {
  const out = [];
  for (const e of await readdir(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    if (e.isDirectory()) out.push(...await walk(p));
    else if (['.glb', '.gltf'].includes(extname(e.name))) out.push(p);
  }
  return out;
}

function bboxFromJson(json) {
  let min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
  for (const mesh of json.meshes || []) {
    for (const prim of mesh.primitives || []) {
      const pa = prim.attributes && prim.attributes.POSITION;
      if (pa == null) continue;
      const acc = json.accessors[pa];
      if (!acc || !acc.min || !acc.max) continue;
      for (let i = 0; i < 3; i++) { min[i] = Math.min(min[i], acc.min[i]); max[i] = Math.max(max[i], acc.max[i]); }
    }
  }
  const size = min[0] === Infinity ? null : [max[0]-min[0], max[1]-min[1], max[2]-min[2]].map(n => +n.toFixed(2));
  return { size, minY: min[1] === Infinity ? null : +min[1].toFixed(2) };
}

async function inspect(path) {
  const buf = await readFile(path);
  let json;
  if (extname(path) === '.glb') {
    if (buf.readUInt32LE(0) !== 0x46546c67) return { path, error: 'bad glb magic' };
    const jsonLen = buf.readUInt32LE(12);
    json = JSON.parse(buf.toString('utf8', 20, 20 + jsonLen));
  } else {
    json = JSON.parse(buf.toString('utf8'));
  }
  const meshNames = (json.meshes || []).map(m => m.name || '(unnamed)');
  const { size, minY } = bboxFromJson(json);
  return { path, meshes: meshNames.length, meshNames: meshNames.slice(0, 14), sizeApprox: size, minY };
}

const requested = process.argv.slice(2);
const files = requested.length
  ? requested
  : [...await walk('assets/models/sourced'), 'assets/models/exterior/scene.gltf'];
for (const f of files.sort()) {
  try { console.log(JSON.stringify(await inspect(f))); }
  catch (e) { console.log(JSON.stringify({ path: f, error: String(e.message || e) })); }
}
