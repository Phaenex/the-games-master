#!/usr/bin/env node
// Render-defect scanner for captured frames.
//
// Why this exists: on 2026-08-03 a magenta object sat in the shipping scene through every green
// gate run, hidden in screenshots behind an opaque UI panel, while the gate piers rendered as black
// voids and seventeen frames all scored "ok" on percentile luminance. Luminance proves brightness
// and nothing else. This scans the actual pixels for the defect classes that pass a brightness meter.
//
// Dependency-free PNG decode (node:zlib only) so it can run in gate 1 before any Unity work.

import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { inflateSync } from 'node:zlib';
import path from 'node:path';

const MAGENTA_PIXELS = 400;   // a few stray pixels are AA fringing; a defect is a region
const BLOWN_MEDIAN = 235;
const FLAT_SPREAD = 4;

// Colour cast. Added 2026-08-15 after all eight prologue tour frames passed this scanner while the
// drive's ground rendered as glowing orange -- measured red:blue 2.95 with 1% cool pixels, on a
// scene set at night. Brightness percentiles cannot see hue, the magenta rule only catches a
// suppressed GREEN channel, and so "the entire world is the wrong colour" had no rule at all.
//
// The cause was not the lamps. 24 practicals at 2000K totalling 2475 lumens against a 1-lux moon
// leaves nothing cool for warm light to be warm AGAINST. A night frame should read cool with warm
// accents; these read as firelight.
//
// The band is deliberately generous. A candlelit interior really is warm and a moonlit field really
// is blue, so this is not a look police -- it is a floor that catches a scene lit by only one colour
// of light. 2.0 would have failed the porch (2.23), which is a real and defensible warm shot.
const CAST_MAX_RED_BLUE = 2.5;
const CAST_MIN_RED_BLUE = 0.35;
// Pixels dimmer than this carry no usable hue. Without the floor a night frame's black sky averages
// the cast back to neutral and the rule sees nothing.
const CAST_FLOOR_SUM = 60;

function decodePng(file) {
  const buf = readFileSync(file);
  if (buf.readUInt32BE(0) !== 0x89504e47) throw new Error(`not a PNG: ${file}`);
  let pos = 8, width = 0, height = 0, depth = 0, colorType = 0, interlace = 0;
  const idat = [];
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos);
    const type = buf.toString('ascii', pos + 4, pos + 8);
    const data = buf.subarray(pos + 8, pos + 8 + len);
    if (type === 'IHDR') {
      width = data.readUInt32BE(0); height = data.readUInt32BE(4);
      depth = data[8]; colorType = data[9]; interlace = data[12];
    } else if (type === 'IDAT') idat.push(data);
    else if (type === 'IEND') break;
    pos += 12 + len;
  }
  if (depth !== 8 || interlace !== 0 || (colorType !== 2 && colorType !== 6))
    throw new Error(`unsupported PNG (depth=${depth} color=${colorType} interlace=${interlace})`);
  const channels = colorType === 6 ? 4 : 3;
  const raw = inflateSync(Buffer.concat(idat));
  const stride = width * channels;
  const out = Buffer.alloc(height * stride);
  let src = 0;
  for (let y = 0; y < height; y++) {
    const filter = raw[src++];
    const line = raw.subarray(src, src + stride); src += stride;
    const prev = y > 0 ? out.subarray((y - 1) * stride, y * stride) : null;
    const cur = out.subarray(y * stride, (y + 1) * stride);
    for (let x = 0; x < stride; x++) {
      const a = x >= channels ? cur[x - channels] : 0;
      const b = prev ? prev[x] : 0;
      const c = x >= channels && prev ? prev[x - channels] : 0;
      let v = line[x];
      if (filter === 1) v += a;
      else if (filter === 2) v += b;
      else if (filter === 3) v += (a + b) >> 1;
      else if (filter === 4) {
        const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
        v += (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
      }
      cur[x] = v & 0xff;
    }
  }
  return { width, height, channels, pixels: out };
}

/// Magenta = red and blue present while green is suppressed. That is what a missing shader, a null
/// material and an unsupported render path all resolve to, and no luminance percentile catches it.
function scan(file) {
  const { width, height, channels, pixels } = decodePng(file);
  let magenta = 0;
  let redSum = 0, blueSum = 0, litPixels = 0;
  const lum = new Uint8Array(width * height);
  for (let i = 0, p = 0; i < pixels.length; i += channels, p++) {
    const r = pixels[i], g = pixels[i + 1], b = pixels[i + 2];
    if (r > 40 && b > 40 && g < r * 0.55 && g < b * 0.55) magenta++;
    // Only pixels with something in them. Near-black pixels carry no hue worth averaging and, in a
    // night scene, there are enough of them to drag any whole-frame average back toward neutral --
    // which would let a frame with a blazing orange foreground and a black sky read as balanced.
    if (r + g + b > CAST_FLOOR_SUM) { redSum += r; blueSum += b; litPixels++; }
    lum[p] = (r * 77 + g * 150 + b * 29) >> 8;
  }
  const sorted = Uint8Array.from(lum).sort();
  const at = (q) => sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * q))];
  const p5 = at(0.05), median = at(0.5), p90 = at(0.9), max = sorted[sorted.length - 1];
  const cast = litPixels > 0 ? redSum / Math.max(blueSum, 1) : 1;
  const defects = [];
  if (magenta >= MAGENTA_PIXELS)
    defects.push(`MAGENTA ${magenta}px — missing shader / null material / unsupported render path`);
  if (p90 < 3) defects.push(`NEAR-BLACK p90=${p90}`);
  if (median > BLOWN_MEDIAN) defects.push(`BLOWN median=${median}`);
  if (p90 - p5 < FLAT_SPREAD && max < 250) defects.push(`FLAT spread=${p90 - p5}`);
  if (litPixels > width * height * 0.02) {
    if (cast > CAST_MAX_RED_BLUE)
      defects.push(`ORANGE CAST red:blue=${cast.toFixed(2)} (max ${CAST_MAX_RED_BLUE}) — ` +
        'the lit part of this frame has almost no cool light in it');
    if (cast < CAST_MIN_RED_BLUE)
      defects.push(`BLUE CAST red:blue=${cast.toFixed(2)} (min ${CAST_MIN_RED_BLUE}) — ` +
        'the lit part of this frame has almost no warm light in it');
  }
  return { magenta, p5, median, p90, max, cast, defects };
}

function collect(target) {
  if (!existsSync(target)) return [];
  if (statSync(target).isFile()) return target.endsWith('.png') ? [target] : [];
  return readdirSync(target, { withFileTypes: true }).flatMap((entry) =>
    collect(path.join(target, entry.name)));
}

const targets = process.argv.slice(2);
if (targets.length === 0) {
  console.error('usage: scan-frame-defects.mjs <file-or-directory>...');
  process.exit(2);
}

// A target that produced no evidence fails, and the test is the FRAME COUNT, not whether the path
// exists. Checking existence alone left a hole wide enough to drive the whole gate through: a
// directory that exists and holds zero PNGs contributed nothing, appeared in no list, and the run
// still printed a clean tally — proven by `scan-frame-defects.mjs <one-real-frame> scripts/`, which
// reported "1 frame(s) clean" while scripts/ was silently ignored. A Unity output path that gets
// renamed, or an earlier gate that fails to populate its directory, lands exactly there.
//
// Absence of evidence is the failure condition. Each target must speak for itself.
const tally = targets.map((target) => ({
  target,
  exists: existsSync(target),
  frames: collect(target).sort(),
}));

const barren = tally.filter((entry) => entry.frames.length === 0);
for (const entry of barren) {
  console.log(entry.exists
    ? `  ✗ empty target: ${entry.target} — the path is there but captured no frames`
    : `  ✗ missing target: ${entry.target} — nothing was captured there`);
}

const frames = tally.flatMap((entry) => entry.frames).sort();
if (frames.length === 0) {
  console.error('✗ no PNG frames found — nothing was verified');
  process.exit(1);
}

let bad = 0;
let unreadable = 0;
for (const frame of frames) {
  let result;
  // A frame that will not decode was not scanned. Counting it as clean is the exact blindness this
  // scanner exists to close, so it fails the run instead of dropping out of the tally.
  try { result = scan(frame); }
  catch (error) { unreadable++; console.log(`  ✗ ${path.basename(frame)}: ${error.message} — NOT SCANNED`); continue; }
  if (result.defects.length === 0) continue;
  bad++;
  console.log(`  ✗ ${path.basename(frame)}`);
  for (const defect of result.defects) console.log(`      ${defect}`);
}

const scanned = frames.length - unreadable;
const problems = [];
if (bad) problems.push(`${bad}/${scanned} scanned frame(s) carry a render defect`);
if (unreadable) problems.push(`${unreadable} frame(s) could not be decoded`);
if (barren.length) problems.push(`${barren.length} target(s) produced no frames`);
console.log(problems.length === 0
  ? `✓ frame defects: ${scanned} frame(s) clean`
  : `✗ frame defects: ${problems.join('; ')}`);
process.exit(problems.length === 0 ? 0 : 1);
