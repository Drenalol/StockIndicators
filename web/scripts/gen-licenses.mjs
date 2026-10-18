import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// Regenerate src/lib/openSourceLicenses.ts from the actual installed license
// texts: node node scripts/gen-licenses.mjs (from the frontend/ directory).
const FRONT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const NM = path.join(FRONT, 'node_modules');
const lock = JSON.parse(fs.readFileSync(path.join(FRONT, 'package-lock.json'), 'utf8'));
const pkgJson = JSON.parse(fs.readFileSync(path.join(FRONT, 'package.json'), 'utf8'));

// Build toolchain roots: their output is generated artifacts, no code ships in the bundle.
const BUILD_ONLY = new Set(['tailwindcss', '@tailwindcss/vite', '@tailwindcss/node', '@tailwindcss/oxide']);

const depGraph = new Map();
for (const [key, p] of Object.entries(lock.packages)) {
  if (!key.startsWith('node_modules/')) continue;
  const name = key.slice('node_modules/'.length);
  if (name.includes('node_modules/')) continue;
  depGraph.set(name, Object.keys(p.dependencies || {}));
}

function closure(roots) {
  const seen = new Set();
  const stack = [...roots];
  while (stack.length) {
    const n = stack.pop();
    if (seen.has(n)) continue;
    seen.add(n);
    for (const d of depGraph.get(n) || []) stack.push(d);
  }
  return seen;
}

const roots = Object.keys(pkgJson.dependencies).filter((n) => !BUILD_ONLY.has(n));
const shipped = closure(roots);
shipped.add('tw-animate-css'); // its CSS ships verbatim in the stylesheet

const LICENSE_FILE_CANDIDATES = ['LICENSE', 'LICENSE.md', 'LICENSE.txt', 'licence', 'COPYING'];

function readLicenseText(name) {
  const dir = path.join(NM, name);
  for (const f of LICENSE_FILE_CANDIDATES) {
    const p = path.join(dir, f);
    if (fs.existsSync(p)) return fs.readFileSync(p, 'utf8').replace(/\r\n/g, '\n').trim();
  }
  return null;
}

function repoUrl(pj) {
  let r = pj.repository;
  if (r && typeof r === 'object') r = r.url;
  if (!r) return pj.homepage || null;
  r = String(r).replace(/^git\+/, '').replace(/\.git$/, '').replace(/^git:\/\//, 'https://');
  if (r.startsWith('git@github.com:')) r = 'https://github.com/' + r.slice('git@github.com:'.length);
  return r || null;
}

const VENDORED = [
  {
    name: 'shadcn/ui',
    version: null,
    license: 'MIT',
    url: 'https://ui.shadcn.com',
    text: 'MIT License\n\nCopyright (c) 2023 shadcn\n\nPermission is hereby granted, free of charge, to any person obtaining a copy\nof this software and associated documentation files (the "Software"), to deal\nin the Software without restriction, including without limitation the rights\nto use, copy, modify, merge, publish, distribute, sublicense, and/or sell\ncopies of the Software, and to permit persons to whom the Software is\nfurnished to do so, subject to the following conditions:\n\nThe above copyright notice and this permission notice shall be included in all\ncopies or substantial portions of the Software.\n\nTHE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR\nIMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,\nFITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE\nAUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER\nLIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,\nOUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE\nSOFTWARE.',
  },
];

const entries = [];
const missingText = [];
for (const name of shipped) {
  const pjPath = path.join(NM, name, 'package.json');
  if (!fs.existsSync(pjPath)) { missingText.push(name + ' (no package.json)'); continue; }
  const pj = JSON.parse(fs.readFileSync(pjPath, 'utf8'));
  const license = String(pj.license || (lock.packages['node_modules/' + name] || {}).license || '?');
  const text = readLicenseText(name);
  if (!text) missingText.push(name + ' (' + license + ')');
  entries.push({ name, version: pj.version, license, url: repoUrl(pj), text });
}
entries.push(...VENDORED);

const groups = new Map();
for (const e of entries) {
  const blocks = groups.get(e.license) || [];
  groups.set(e.license, blocks);
  const norm = (e.text || '').replace(/\s+/g, ' ').trim();
  let block = blocks.find((b) => b.norm === norm);
  if (!block) { block = { norm, text: e.text, packages: [] }; blocks.push(block); }
  block.packages.push({ name: e.name, version: e.version, url: e.url });
}

const TEXT_URLS = {
  'Apache-2.0': 'https://www.apache.org/licenses/LICENSE-2.0.txt',
  'MPL-2.0': 'https://www.mozilla.org/MPL/2.0/',
  'BSD-3-Clause': 'https://opensource.org/licenses/BSD-3-Clause',
  '0BSD': 'https://opensource.org/licenses/0BSD',
  'ISC': 'https://opensource.org/licenses/ISC',
  'MIT': 'https://opensource.org/licenses/MIT',
};
const EMBED_LIMIT = 4000;

const out = [];
out.push('export interface LicensePackage {');
out.push('  name: string;');
out.push('  version: string | null;');
out.push('  url: string | null;');
out.push('}');
out.push('');
out.push('export interface LicenseBlock {');
out.push('  /** SPDX identifier as declared by the packages themselves. */');
out.push('  license: string;');
out.push('  /** Full license text, embedded when short enough (MIT/ISC/0BSD family). */');
out.push('  text: string | null;');
out.push('  /** Canonical online license text (long licenses like Apache-2.0, or missing file). */');
out.push('  textUrl: string | null;');
out.push('  packages: LicensePackage[];');
out.push('}');
out.push('');
out.push('/**');
out.push(' * Third-party open-source components actually shipped to the user (browser/app');
out.push(' * bundle): generated from the frontend runtime dependency tree plus vendored');
out.push(' * sources. Regenerate with the helper script when dependencies change.');
out.push(' */');
out.push('export const LICENSE_GROUPS: LicenseBlock[] = [');
for (const [lic, blocks] of [...groups.entries()].sort()) {
  for (const b of blocks.sort((x, y) => x.packages[0].name.localeCompare(y.packages[0].name))) {
    const embed = b.text && b.text.length <= EMBED_LIMIT ? b.text : null;
    out.push('  {');
    out.push(`    license: ${JSON.stringify(lic)},`);
    out.push(`    text: ${embed === null ? 'null' : JSON.stringify(embed)},`);
    out.push(`    textUrl: ${JSON.stringify(TEXT_URLS[lic] ?? b.packages[0].url ?? null)},`);
    out.push('    packages: [');
    for (const p of b.packages.sort((x, y) => x.name.localeCompare(y.name))) {
      out.push(`      { name: ${JSON.stringify(p.name)}, version: ${p.version === null ? 'null' : JSON.stringify(p.version)}, url: ${JSON.stringify(p.url)} },`);
    }
    out.push('    ],');
    out.push('  },');
  }
}
out.push('];');
out.push('');
const OUT = path.join(FRONT, 'src', 'lib', 'openSourceLicenses.ts');
fs.writeFileSync(OUT, out.join('\n') + '\n');
console.error('groups:', [...groups.entries()].map(([k, v]) => `${k}=${v.flatMap((b) => b.packages).length}`).join(' '));
console.error('total packages:', entries.length);
console.error('no embedded text file:', missingText.join(', ') || 'none');
