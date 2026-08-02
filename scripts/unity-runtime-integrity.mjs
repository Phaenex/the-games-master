// Standalone-log integrity policy. These are not generic Unity warnings: each one means visible
// content can be missing or replaced even though the process remained alive and returned 0.
export const RENDER_INTEGRITY_PATTERNS = [
  /couldn't be instanced/i,
  /contains no valid mesh renderer/i,
  /shader is not supported on this gpu/i,
  /has no shader assigned/i,
  /fallback shader 'hidden\/internalerrorshader'/i,
  /BoxCollider does not support negative scale or size/i,
];

export function findRuntimeIntegrityDefects(log) {
  if (typeof log !== 'string') throw new TypeError('runtime log must be a string');
  return log.split(/\r?\n/)
    .filter((line) => RENDER_INTEGRITY_PATTERNS.some((pattern) => pattern.test(line)));
}
