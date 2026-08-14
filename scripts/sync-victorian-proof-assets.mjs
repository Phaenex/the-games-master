#!/usr/bin/env node

// Installs the small, proven subset of the locally-owned MetalMan Victorian Interiors package
// needed by the opening. The complete extracted package is 1.1 GB; copying all of it into Unity
// would lengthen every import and hide which assets the room actually depends on. This manifest is
// intentionally explicit and supports a read-only --check mode so the visual build stays repeatable.
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
} from 'node:fs';
import { homedir } from 'node:os';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const repoRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const unityRoot = process.env.GM_UNITY_PROJECT || path.join(repoRoot, 'unity-project');
const sourceRoot = path.join(
  repoRoot,
  'assets',
  'models',
  'unity-import',
  'metalman-victorian-interior',
  'Victorian Interiors',
  'Victorian Interiors',
);
const destinationRoot = path.join(
  unityRoot,
  'Assets',
  'ThirdParty',
  'MetalManVictorianInteriors',
);

const portraitSlugs = [
  'edwin-marr',
  'caspian-dufresne',
  'halvard-pike',
  'solveig-hale',
  'theo-gall',
  'barnaby-quill',
  'imogen-thale',
  'constance',
  'percival',
];

const models = [
  'BookShelf_1.fbx',
  'Carpet_1.fbx',
  'Chair_1.fbx',
  'Chair_2.fbx',
  'Couch_2.fbx',
  'Door_1.fbx',
  'Lamp_1_LOD0.fbx',
  'Lamp_2.fbx',
  'Mantel.fbx',
  'Mirror_1.fbx',
  'Table_2.fbx',
  'Table_3.fbx',
  'Stair.fbx',
  ...Array.from({ length: 8 }, (_, index) => `Picture_${index + 1}.fbx`),
];

const textureFamilies = {
  BookShelf_1: ['Bookshelf_Albedo.psd', 'Bookshelf_Normal.psd'],
  Carpet_1: ['Carpets_Albedo.psd', 'Carpets_Normal.psd'],
  Chair_1: ['Chairs_Albedo.psd', 'Chairs_Normal.psd'],
  Chair_2: ['Chairs_Albedo.psd', 'Chairs_Normal.psd'],
  Couch_2: ['Couch_2_Brown_Albedo.psd', 'Couch_2_Normal.psd'],
  Door_1: ['Doors_Albedo.psd', 'Doors_Normal.psd'],
  Lamp_1_LOD0: ['Lamp_Albedo.psd', 'Lamp_Normal.psd', 'Lamp_Emission.psd'],
  Mantel: ['Mantel_Albedo.psd', 'Mantel_Normal.psd'],
  Mirror_1: ['Mirrors_Albedo.psd', 'Mirrors_Normal.psd'],
  Table_2: ['Tables_2_Albedo.psd', 'Tables_2_Normal.psd'],
  Picture_1: ['Frames_Albedo.psd', 'Frames_Normal.psd'],
  Stair: ['Stair_Albedo.png', 'Stair_Normal.png'],
  WallSurface: ['Wall_1_Albedo.psd', 'Wall_1_Normal.png'],
  FloorSurface: ['Floor_Albedo.png', 'Floor_Normal.png'],
  CeilingSurface: ['Roof_Albedo.png', 'Roof_Normal.png'],
};

const textures = [...new Set(Object.values(textureFamilies).flat())].sort();
const files = [
  ...models.flatMap((name) => [name, `${name}.meta`]),
  ...textures.flatMap((name) => [path.join('Materials', name), path.join('Materials', `${name}.meta`)]),
];

const authoredFiles = [
  {
    source: path.join(repoRoot, 'assets', 'models', 'hall', 'fbx', 'Armor_Metal.fbx'),
    destination: path.join(unityRoot, 'Assets', 'GamesMaster', 'Interior', 'Armor_Metal.fbx'),
    label: 'GamesMaster/Interior/Armor_Metal.fbx',
  },
  ...portraitSlugs.map((slug) => ({
    source: path.join(repoRoot, 'assets', 'images', 'portraits', `${slug}.png`),
    destination: path.join(unityRoot, 'Assets', 'GamesMaster', 'Portraits', `${slug}.png`),
    label: `GamesMaster/Portraits/${slug}.png`,
  })),
];

const check = process.argv.includes('--check');
const drift = [];
let copied = 0;
let bytes = 0;

for (const relative of files) {
  const source = path.join(sourceRoot, relative);
  const destination = path.join(destinationRoot, relative);
  if (!existsSync(source)) throw new Error(`Victorian proof source is missing: ${source}`);
  const sourceBytes = readFileSync(source);
  bytes += sourceBytes.length;
  const matches = existsSync(destination) && readFileSync(destination).equals(sourceBytes);
  if (matches) continue;
  drift.push(relative);
  if (!check) {
    mkdirSync(path.dirname(destination), { recursive: true });
    copyFileSync(source, destination);
    copied++;
  }
}

for (const asset of authoredFiles) {
  if (!existsSync(asset.source)) throw new Error(`Authored proof source is missing: ${asset.source}`);
  const sourceBytes = readFileSync(asset.source);
  bytes += sourceBytes.length;
  const matches = existsSync(asset.destination) && readFileSync(asset.destination).equals(sourceBytes);
  if (matches) continue;
  drift.push(asset.label);
  if (!check) {
    mkdirSync(path.dirname(asset.destination), { recursive: true });
    copyFileSync(asset.source, asset.destination);
    copied++;
  }
}

if (check && drift.length) {
  for (const relative of drift) console.error(`  ✗ ${relative}`);
  console.error(`✗ Victorian proof asset drift: ${drift.length}/${files.length} file(s)`);
  process.exit(1);
}

const mib = (bytes / 1024 / 1024).toFixed(1);
const governed = files.length + authoredFiles.length;
console.log(check
  ? `✓ Victorian proof assets synchronized: ${governed} files, ${mib} MiB governed`
  : `✓ Victorian proof assets installed: ${copied} copied, ${governed} governed, ${mib} MiB`);
