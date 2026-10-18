// Validates the src/i18n JSON catalogs: key parity against en.json, no CJK
// characters (stray model-translation artifacts), and {{token}} parity.
// Usage: node scripts/check-i18n.mjs
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const dir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'src', 'i18n');
const reference = 'en.json';

const CJK = /[\u2E80-\u9FFF\uF900-\uFAFF\u3040-\u30FF\uAC00-\uD7AF\uFF00-\uFFEF\u{20000}-\u{2FA1F}]/u;
const TOKENS = /\{\{[a-zA-Z0-9_.]+\}\}/g;

const readJson = (file) => JSON.parse(fs.readFileSync(path.join(dir, file), 'utf8'));

const collectTokens = (value, out) => {
  if (typeof value === 'string') {
    for (const match of value.matchAll(TOKENS)) out.push(match[0]);
  } else if (Array.isArray(value)) {
    for (const item of value) collectTokens(item, out);
  } else if (value && typeof value === 'object') {
    for (const nested of Object.values(value)) collectTokens(nested, out);
  }
  return out;
};

const walk = (referenceNode, node, prefix, problems) => {
  if (Object.keys(referenceNode).some((key) => !(key in node)))
    problems.push(`${prefix || '<root>'} misses keys: ${Object.keys(referenceNode).filter((key) => !(key in node)).join(', ')}`);

  for (const extra of Object.keys(node).filter((key) => !(key in referenceNode)))
    problems.push(`${prefix}${extra} is an extra key absent from ${reference}`);

  for (const [key, referenceValue] of Object.entries(referenceNode)) {
    if (!(key in node)) continue;
    const value = node[key];
    const pathPrefix = `${prefix}${key}`;

    if (typeof referenceValue === 'string') {
      if (typeof value !== 'string') {
        problems.push(`${pathPrefix} must be a string`);
        continue;
      }

      const offender = value.match(CJK);
      if (offender) {
        const at = value.indexOf(offender[0]);
        problems.push(`${pathPrefix} contains a CJK character U+${offender[0].codePointAt(0).toString(16).toUpperCase()} at ${at}: ...${value.slice(Math.max(0, at - 20), at + 20)}...`);
      }

      const referenceTokens = [...referenceValue.matchAll(TOKENS)].map((match) => match[0]).sort();
      const tokens = [...value.matchAll(TOKENS)].map((match) => match[0]).sort();
      if (referenceTokens.join('|') !== tokens.join('|'))
        problems.push(`${pathPrefix} placeholder mismatch: expected [${referenceTokens.join(', ')}], got [${tokens.join(', ')}]`);
    } else if (Array.isArray(referenceValue)) {
      if (!Array.isArray(value) || value.length !== referenceValue.length) {
        problems.push(`${pathPrefix} must be an array of ${referenceValue.length} items`);
        continue;
      }

      referenceValue.forEach((item, index) => {
        if (typeof item === 'string' && typeof value[index] === 'string') {
          const offender = value[index].match(CJK);
          if (offender)
            problems.push(`${pathPrefix}[${index}] contains a CJK character U+${offender[0].codePointAt(0).toString(16).toUpperCase()}`);
        }
      });
    } else if (referenceValue && typeof referenceValue === 'object') {
      if (!value || typeof value !== 'object') {
        problems.push(`${pathPrefix} must be an object`);
        continue;
      }

      walk(referenceValue, value, `${pathPrefix}.`, problems);
    }
  }
};

const referenceJson = readJson(reference);
const files = fs.readdirSync(dir).filter((file) => file.endsWith('.json') && file !== reference).sort();
const problems = [];

for (const file of files) {
  let json;
  try {
    json = readJson(file);
  } catch (error) {
    problems.push(`${file}: invalid JSON - ${error.message}`);
    continue;
  }

  walk(referenceJson, json, '', problems);
}

if (problems.length > 0) {
  console.error(`i18n check FAILED (${problems.length} problem(s)):`);
  for (const problem of problems) console.error(` - ${problem}`);
  process.exit(1);
}

// The reference itself must also stay free of CJK characters
const referenceOffender = JSON.stringify(referenceJson).match(CJK);
if (referenceOffender) {
  console.error(`i18n check FAILED: ${reference} contains a CJK character U+${referenceOffender[0].codePointAt(0).toString(16).toUpperCase()}`);
  process.exit(1);
}

console.log(`i18n check OK: ${files.length} locale file(s) match ${reference} (${collectTokens(referenceJson, []).length} placeholder occurrences in the reference).`);
