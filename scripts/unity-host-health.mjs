import { cpus, loadavg } from 'node:os';

export const MAX_LOAD_PER_CORE = 3;

export function evaluateHostCapacity({
  oneMinuteLoad,
  coreCount,
  maxLoadPerCore = MAX_LOAD_PER_CORE,
}) {
  if (!Number.isFinite(oneMinuteLoad) || !Number.isInteger(coreCount) || coreCount < 1) {
    return { healthy: true, ratio: null, reason: 'host load unavailable' };
  }
  const ratio = oneMinuteLoad / coreCount;
  return {
    healthy: ratio <= maxLoadPerCore,
    ratio,
    reason: `${oneMinuteLoad.toFixed(1)} load / ${coreCount} cores = ${ratio.toFixed(1)} per core`,
  };
}

export function currentHostCapacity() {
  return evaluateHostCapacity({ oneMinuteLoad: loadavg()[0], coreCount: cpus().length });
}

export function assertHostCapacity({ allowOverride = true } = {}) {
  const state = currentHostCapacity();
  if (state.healthy) return state;
  if (allowOverride && process.env.GM_UNITY_IGNORE_HOST_LOAD === '1') {
    console.warn(`  ⚠ forcing Unity on an overloaded host (${state.reason})`);
    return state;
  }
  throw new Error(
    `host is too overloaded for a trustworthy Unity launch (${state.reason}; limit ${MAX_LOAD_PER_CORE.toFixed(1)}). ` +
    'Wait for other browser/editor jobs to settle, then retry. Set GM_UNITY_IGNORE_HOST_LOAD=1 only for a deliberate diagnostic run.',
  );
}
