// Shared normalisation helpers for the grid harness.

/** Accept 0x148, "0x148", "328", 328 -> 328. Returns null for absent. */
export function parseOffset(value) {
  if (value === null || value === undefined) return null;
  if (typeof value === 'number') return Number.isInteger(value) ? value : null;
  if (typeof value === 'bigint') return Number(value);
  if (typeof value !== 'string') return null;
  const text = value.trim();
  if (text === '') return null;
  const parsed = /^0[xX]/.test(text) ? Number.parseInt(text, 16) : Number.parseInt(text, 10);
  return Number.isNaN(parsed) ? null : parsed;
}

export function hex(value) {
  return value === null || value === undefined ? 'null' : `0x${Number(value).toString(16)}`;
}

/**
 * §12.6: scry returns vectors as array-LIKES ({0:…,1:…}); Array.isArray() is
 * false for them. Any driver bridging through a similar layer will do the same,
 * so normalise defensively rather than trusting the shape.
 */
export function toVector(value, arity) {
  if (value === null || value === undefined) return null;
  let parts;
  if (Array.isArray(value)) parts = value;
  else if (typeof value === 'object') {
    if ('x' in value) parts = arity === 2 ? [value.x, value.y] : [value.x, value.y, value.z, value.w];
    else parts = Array.from({ length: arity }, (_, i) => value[i] ?? value[String(i)]);
  } else return null;
  if (parts.length < arity) return null;
  const nums = parts.slice(0, arity).map(Number);
  return nums.some(Number.isNaN) ? null : nums;
}

/** Pointers are compared as normalised lowercase hex strings, never as Numbers. */
export function normPtr(value) {
  if (value === null || value === undefined) return null;
  if (typeof value === 'bigint') return `0x${value.toString(16)}`;
  if (typeof value === 'number') {
    if (!Number.isSafeInteger(value)) return null; // 64-bit pointer lost to double
    return `0x${value.toString(16)}`;
  }
  if (typeof value !== 'string') return null;
  const text = value.trim().toLowerCase();
  if (text === '' || text === '0' || text === '0x0' || text === 'null') return null;
  try {
    return `0x${BigInt(/^0x/.test(text) ? text : `0x${text}`).toString(16)}`;
  } catch {
    return null;
  }
}

export function vectorsEqual(a, b, eps) {
  if (a === null || b === null) return false;
  if (a.length !== b.length) return false;
  return a.every((v, i) => Math.abs(v - b[i]) <= eps);
}

export function fmtVec(v) {
  return v === null ? 'null' : `[${v.map((n) => (Number.isInteger(n) ? n : n.toFixed(3))).join(', ')}]`;
}

export function codepoints(text) {
  return [...text].map((c) => c.codePointAt(0));
}

/** Rendering helper for text diffs: keep non-ASCII visible as U+XXXX. */
export function escapeText(text) {
  if (text === null || text === undefined) return 'null';
  return [...text]
    .map((c) => {
      const cp = c.codePointAt(0);
      if (cp < 0x20 || cp === 0x7f) return `\\u${cp.toString(16).padStart(4, '0')}`;
      return c;
    })
    .join('');
}
