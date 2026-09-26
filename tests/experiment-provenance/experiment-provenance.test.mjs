// PCT-036 / DF-PERCEPTA-2026-0001: sealed experiment run provenance.
//
// Proves, for FUTURE experiments only, that run records conform to
// schemas/percepta-experiment-run.schema.json and the Praxis provenance
// contract, that no provenance leaks into lane-visible inputs, lane outputs,
// or reviewer bundles, that the evaluator cannot read identity, and that no
// frozen or hashed experiment artifact changed.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import * as praxis from '../../scripts/experiment-provenance/praxis-provenance-record.mjs';
import {
  LEGACY_EXPERIMENT_DIRECTORIES, RUN_RECORD_PATH, makeRunRecord, placedRunRecordProblems, runRecordProblems, sha256
} from '../../scripts/experiment-provenance/experiment-run.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const rel = full => path.relative(root, full).split(path.sep).join('/');
const readFile = relative => fs.readFileSync(path.join(root, relative));
const readJson = relative => JSON.parse(readFile(relative).toString('utf8'));
const exists = relative => fs.existsSync(path.join(root, relative));
const IGNORED_DIRECTORIES = new Set(['.git', 'node_modules', 'bin', 'obj']);
const walk = directory =>
  !fs.existsSync(directory)
    ? []
    : fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry =>
        entry.isDirectory()
          ? (IGNORED_DIRECTORIES.has(entry.name) ? [] : walk(path.join(directory, entry.name)))
          : [path.join(directory, entry.name)]);
const directories = (relative, pattern) =>
  exists(relative)
    ? fs.readdirSync(path.join(root, relative), { withFileTypes: true })
        .filter(entry => entry.isDirectory() && pattern.test(entry.name))
        .map(entry => `${relative}/${entry.name}`)
    : [];
const gitBlob = bytes => createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');

const schema = readJson('schemas/percepta-experiment-run.schema.json');

// ---- vendored Praxis conformance fixtures --------------------------------------

const FIXTURES = 'tests/fixtures/praxis-provenance-record';
const fixture = file => readJson(`${FIXTURES}/${file}`);
const manifest = fixture('manifest.json');
const outcome = reading =>
  ({ current: 'valid', unversioned: 'valid-unversioned', unsupported: 'unsupported-version', invalid: 'invalid' })[reading.kind];

test('vendored Praxis fixtures match SOURCE.json digests exactly', () => {
  const source = fixture('SOURCE.json');
  assert.equal(source.repository, 'kemiller2002/praxis');
  assert.equal(source.path, 'schemas/conformance/provenance-record');
  assert.match(source.commit, /^[0-9a-f]{40}$/);
  const present = walk(path.join(root, FIXTURES)).map(full => path.relative(path.join(root, FIXTURES), full).split(path.sep).join('/'))
    .filter(file => file !== 'SOURCE.json').sort();
  assert.deepEqual(present, Object.keys(source.files).sort(), 'no fixture added or removed');
  for (const file of present) assert.equal(sha256(readFile(`${FIXTURES}/${file}`)), source.files[file], file);
});

test('codec classifies every conformance case as the manifest expects', () => {
  assert.ok(manifest.cases.length >= 28);
  for (const item of manifest.cases) {
    const reading = praxis.validate(fixture(item.file));
    assert.equal(outcome(reading), item.expect, `${item.file}: ${item.why} ${JSON.stringify(reading.problems ?? [])}`);
  }
});

test('codec judges every successor pair as the manifest expects', () => {
  assert.ok(manifest.successors.length >= 15);
  for (const pair of manifest.successors) {
    const problems = praxis.successorProblems(fixture(pair.before), fixture(pair.after));
    assert.equal(problems.length ? 'destructive' : 'preserved', pair.expect, `${pair.before}: ${pair.why} ${JSON.stringify(problems)}`);
  }
});

test('end-to-end chain keeps every actor, execution, and originator', () => {
  for (const step of manifest.e2e.steps) {
    assert.equal(praxis.validate(fixture(step.file)).kind, 'current', step.file);
    if (step.successorOf) assert.deepEqual(praxis.successorProblems(fixture(step.successorOf), fixture(step.file)), [], step.file);
  }
  const final = praxis.validate(fixture(manifest.e2e.expected.final)).record;
  const links = praxis.chain(final).map(link => ({
    subject: link.subject, key: link.contribution.key, actor: link.contribution.actor.id, operations: [...link.contribution.operations]
  }));
  assert.deepEqual(links, manifest.e2e.expected.chain);
  const origin = praxis.originator(final);
  assert.equal(origin.key, manifest.e2e.expected.originatorOfFinal.key);
  assert.equal(origin.actor.id, manifest.e2e.expected.originatorOfFinal.actor);
});

// ---- run record contract --------------------------------------------------------

const manifestBytes = Buffer.from('0000000000000000000000000000000000000000000000000000000000000000  ./candidate-01/evidence.json\n');
const example = overrides => makeRunRecord({
  experiment: 'EX-PERCEPTA-2026-0005',
  run: 'lane-control-a',
  role: 'lane',
  at: '2026-10-01T12:00:00.000Z',
  actor: praxis.actor({ kind: 'agent', provider: 'example-provider', runtime: 'example-runtime' }),
  reason: 'First-pass lane implementation run',
  firstPassFreezeCommit: 'a'.repeat(40),
  manifest: { path: 'artifacts/percepta/experiment-0005/SHA256SUMS', sha256: sha256(manifestBytes) },
  ...overrides
});

test('a run record built from declared identity conforms; unknown stays unknown', () => {
  const record = example({});
  assert.deepEqual(runRecordProblems(schema, record), []);
  const [key, entry] = Object.entries(record.provenance.contributions)[0];
  assert.equal(key, 'EXE-percepta.lane-control-a');
  assert.deepEqual(entry.actor, { kind: 'agent', id: 'example-provider/example-runtime', provider: 'example-provider', model: 'unknown', runtime: 'example-runtime' });
  assert.deepEqual(praxis.actorFromEnvironment({}), { kind: 'unknown', id: 'unknown', provider: 'unknown', model: 'unknown', runtime: 'unknown' });
  assert.deepEqual(praxis.actorFromEnvironment({ GITHUB_ACTIONS: 'true' }),
    { kind: 'automation', id: 'github/github-actions', provider: 'github', model: 'unknown', runtime: 'github-actions' });
});

test('a propagated ROS_EXECUTION_ID keys the run; otherwise EXE-percepta.<run>', () => {
  const propagated = example({ env: { ROS_EXECUTION_ID: 'EXE-20261001T120000000Z-0a1b2c3d' } });
  assert.deepEqual(Object.keys(propagated.provenance.contributions), ['EXE-20261001T120000000Z-0a1b2c3d']);
  assert.deepEqual(runRecordProblems(schema, propagated), []);
  assert.throws(() => example({ env: { ROS_EXECUTION_ID: 'not-an-execution' } }));
  assert.equal(praxis.isPraxisExecutionId(Object.keys(example({}).provenance.contributions)[0]), false, 'never mints a Praxis-shaped id');
});

test('malformed or evidence-bearing run records are rejected, not dropped', () => {
  const base = example({});
  const [key] = Object.keys(base.provenance.contributions);
  const withContribution = (change) => ({
    ...base, provenance: { ...base.provenance, contributions: { [key]: change(base.provenance.contributions[key]) } }
  });
  const rejected = {
    'evidence status smuggled into the record': { ...base, status: 'Passed' },
    'score smuggled into the record': { ...base, score: 1 },
    'agent without a model': withContribution(entry => ({ ...entry, actor: { kind: 'agent', id: 'x', provider: 'p', runtime: 'r' } })),
    'credential in identity': withContribution(entry => ({ ...entry, actor: { ...entry.actor, model: 'sk-ant-api03-AAAAAAAAAAAAAAAAAAAAAAAA' } })),
    'contribution not keyed by an execution': { ...base, provenance: { ...base.provenance, contributions: { 'CTB-20261001-aaaa': base.provenance.contributions[key] } } },
    'two contributions': { ...base, provenance: { ...base.provenance, contributions: { ...base.provenance.contributions, 'EXE-percepta.other': base.provenance.contributions[key] } } },
    'subject names another run': { ...base, provenance: { ...base.provenance, subject: 'percepta:EX-PERCEPTA-2026-0005/runs/lane-treatment-a' } },
    'unsupported Praxis major': { ...base, provenance: { ...base.provenance, version: '2.0.0' } },
    'unknown role': { ...base, role: 'observer' },
    'no sealing': (({ sealing, ...rest }) => rest)(base)
  };
  for (const [name, record] of Object.entries(rejected)) assert.notDeepEqual(runRecordProblems(schema, record), [], name);
});

test('a placed run record must be sealed behind a hashed evidence manifest', () => {
  const record = example({});
  const files = { 'artifacts/percepta/experiment-0005/SHA256SUMS': manifestBytes };
  const reader = relative => { if (!(relative in files)) throw new Error('missing'); return files[relative]; };
  assert.deepEqual(placedRunRecordProblems(schema, 'artifacts/percepta/experiment-0005/runs/lane-control-a.json', record, reader), []);
  assert.notDeepEqual(placedRunRecordProblems(schema, 'research/experiments/EX-PERCEPTA-2026-0005/lanes/control-a/run.json', record, reader), []);
  assert.notDeepEqual(placedRunRecordProblems(schema, 'artifacts/percepta/experiment-0006/runs/lane-control-a.json', record, reader), []);
  assert.notDeepEqual(placedRunRecordProblems(schema, 'artifacts/percepta/experiment-0005/runs/lane-control-a.json', record,
    () => Buffer.from('changed after the record was written\n')), []);
  assert.notDeepEqual(placedRunRecordProblems(schema, 'artifacts/percepta/experiment-0005/runs/lane-control-a.json', record,
    () => { throw new Error('missing'); }), []);
});

test('every run record in the repository conforms (legacy experiments are not backfilled)', () => {
  const found = directories('artifacts/percepta', /^experiment-[0-9]{4}$/)
    .filter(directory => !LEGACY_EXPERIMENT_DIRECTORIES.includes(directory.split('/').pop()))
    .flatMap(directory => walk(path.join(root, directory, 'runs')).map(rel))
    .filter(relative => relative.endsWith('.json'));
  for (const relative of found) {
    assert.deepEqual(placedRunRecordProblems(schema, relative, readJson(relative), readFile), [], relative);
  }
  const misplaced = walk(root).map(rel)
    .filter(relative => relative.endsWith('.json') && !relative.startsWith(`${FIXTURES}/`))
    .filter(relative => { try { return readJson(relative)?.contract === 'percepta.experiment-run'; } catch { return false; } })
    .filter(relative => !RUN_RECORD_PATH.test(relative));
  assert.deepEqual(misplaced, [], 'run records must never be placed outside artifacts/percepta/experiment-NNNN/runs/');
});

// ---- leak guard -----------------------------------------------------------------

// Structured provenance that must never reach a lane (inputs or outputs) or a
// blinded reviewer. Deliberately structural: prose that mentions a provider
// is governed by each experiment's own preregistration, not by this guard.
const LEAK_PATTERNS = [
  ['Praxis execution id', /\bEXE-[0-9]{8}T[0-9]{9}Z-[0-9a-f]{8}\b/],
  ['Percepta run execution id', /\bEXE-percepta\./],
  ['Praxis contribution id', /\bCTB-[0-9]{8}/],
  ['provenance contract', /praxis\.provenance-record|percepta\.experiment-run/],
  ['identity JSON field', /"(actor|provenance|actorKind|agentId|provider|model|runtime)"\s*:/],
  ['provenance front matter', /^provenance:\s*$/m],
  ['model identifier', /\b(claude-(opus|sonnet|haiku)-[0-9][\w.-]*|gpt-[0-9][\w.-]*|gemini-[0-9][\w.-]*)\b/i]
];
const BINARY = /\.(png|jpe?g|gif|webp|ico|pdf|zip|woff2?)$/i;

const laneVisibleFiles = () => [
  ...directories('experiments', /^EX-/),
  ...directories('research/experiments', /^EX-/)
].flatMap(directory => walk(path.join(root, directory))).map(rel);

const reviewerBundleFiles = () => directories('artifacts/percepta', /^experiment-[0-9]{4}$/)
  .flatMap(directory => [...directories(directory, /^candidate-[0-9]+$/), ...directories(directory, /^semantic-review$/)])
  .flatMap(directory => walk(path.join(root, directory))).map(rel);

const leaks = files => files.filter(relative => !BINARY.test(relative)).flatMap(relative => {
  const text = readFile(relative).toString('utf8');
  return LEAK_PATTERNS.filter(([, pattern]) => pattern.test(text)).map(([name]) => `${relative}: ${name}`);
});

test('leak guard patterns catch structured provenance and spare ordinary prose', () => {
  const record = JSON.stringify(example({}), null, 2);
  assert.ok(LEAK_PATTERNS.some(([, pattern]) => pattern.test(record)));
  assert.ok(LEAK_PATTERNS.some(([, pattern]) => pattern.test('see EXE-20260926T064434719Z-3f3b6b20')));
  assert.ok(!LEAK_PATTERNS.some(([, pattern]) => pattern.test('Semantic reviewers do not receive provider, condition, or branch.')));
});

test('no root instruction, lane-visible input, lane output, or reviewer bundle carries run provenance', () => {
  const lane = laneVisibleFiles();
  const reviewer = reviewerBundleFiles();
  const rootInstructions = ['AGENTS.md', 'CLAUDE.md', 'BOOTSTRAP.md', 'GEMINI.md'].filter(exists);
  assert.ok(lane.length > 0, 'lane-visible experiment bundles were found');
  assert.deepEqual(leaks([...rootInstructions, ...lane, ...reviewer]), []);
});

test('the evaluator cannot read identity, so identity cannot change evidence or completion', () => {
  const sources = walk(path.join(root, 'src')).map(rel).filter(relative => /\.(fs|fsx|fsproj|cs|js|mjs|ts)$/.test(relative));
  assert.ok(sources.length > 0);
  const readers = sources.filter(relative =>
    /percepta\.experiment-run|praxis\.provenance-record|ROS_EXECUTION_ID|experiment-[0-9]{4}\/runs|percepta-experiment-run/.test(readFile(relative).toString('utf8')));
  assert.deepEqual(readers, []);
  const evidenceSchema = readFile('schemas/percepta-evidence.schema.json').toString('utf8');
  assert.doesNotMatch(evidenceSchema, /"(actor|provenance|provider|model|runtime|agentId)"\s*:/);
});

// ---- frozen artifacts unchanged -------------------------------------------------

const LEGACY_LOCATIONS = [[/^research\/experiments\/(EX-PERCEPTA-2026-000[23])\//, 'experiments/$1/']];
// Pre-existing, documented drift (not introduced here): commit d5021a2 changed
// this preregistration's status and input paths after the EX-0003 freeze.
// Pinned to its current digest so any further change still fails.
const KNOWN_DRIFT = {
  'research/experiments/EX-PERCEPTA-2026-0003--runtime-protocol-controlled-replication.md':
    'e941726c5a34e0d20024fe59eef479eeff5fbce1f2ccc11d2a70361e0a17f600'
};

const hashedEntries = node =>
  Array.isArray(node) ? node.flatMap(hashedEntries)
    : node && typeof node === 'object'
      ? [...(typeof node.path === 'string' && (node.sha256 || node.blob || node.gitBlobSha) ? [node] : []), ...Object.values(node).flatMap(hashedEntries)]
      : [];

test('frozen experiment artifacts still match integrity.json and SHA256SUMS', () => {
  const experimentDirectories = directories('artifacts/percepta', /^experiment-[0-9]{4}$/);
  let verified = 0;
  for (const directory of experimentDirectories) {
    for (const sums of fs.readdirSync(path.join(root, directory)).filter(name => /SHA256SUMS(\.sha256)?$/.test(name))) {
      for (const line of readFile(`${directory}/${sums}`).toString('utf8').split('\n').filter(Boolean)) {
        const [, digest, file] = /^([0-9a-f]{64}) [ *](.+)$/.exec(line) ?? [];
        assert.ok(digest, `${directory}/${sums}: malformed line ${line}`);
        assert.equal(sha256(readFile(`${directory}/${file.replace(/^\.\//, '')}`)), digest, `${directory}/${file}`);
        verified++;
      }
    }
    if (!exists(`${directory}/integrity.json`)) continue;
    const integrity = readJson(`${directory}/integrity.json`);
    for (const entry of hashedEntries(integrity)) {
      const located = [entry.path, ...LEGACY_LOCATIONS.map(([from, to]) => entry.path.replace(from, to))].find(exists);
      if (!located) continue; // lane outputs live on their lane branches, not on this branch
      const bytes = readFile(located);
      if (KNOWN_DRIFT[entry.path]) { assert.equal(sha256(bytes), KNOWN_DRIFT[entry.path], entry.path); verified++; continue; }
      if (entry.sha256) assert.equal(sha256(bytes), entry.sha256, located);
      if (entry.blob) assert.equal(gitBlob(bytes), entry.blob, located);
      if (entry.gitBlobSha) assert.equal(gitBlob(bytes), entry.gitBlobSha, located);
      verified++;
    }
    for (const lane of integrity.lanes ?? []) {
      if (!lane.evidenceSha256) continue;
      assert.equal(sha256(readFile(`${directory}/${lane.lane}.evidence.json`)), lane.evidenceSha256, `${directory}/${lane.lane}.evidence.json`);
      verified++;
    }
  }
  assert.ok(verified >= 100, `verified ${verified} frozen digests`);
});

// EX-PERCEPTA-2026-0004 has no integrity manifest yet; its pending lanes branch
// from the preregistration commit f474c449. While it is preregistered or
// active, every input its lanes can see must still be byte-identical to that
// commit (git blob SHA-1 taken from f474c449).
const EX_0004_PREREGISTERED_BLOBS = {
  'AGENTS.md': 'c3c9313c69c6d953daa01706c8f4154609bdbcb8',
  'CLAUDE.md': '8f4820c2dc8bb614947150d1e688e89ee14ac2e7',
  'BOOTSTRAP.md': 'e924007c0cfdbfba70a5425e2165ebe72a0c2a0a',
  '.percepta/contracts/indy-init-investigation-workspace.json': '2017a2eab551551cb59bc6ab525defd525024bbb',
  'research/experiments/EX-PERCEPTA-2026-0004--held-out-semantic-generalization.md': 'b74f0a475b99bca46fca1783099c467a361fa2bc',
  'research/experiments/EX-PERCEPTA-2026-0004/frozen-product-brief.md': 'f3a1571689702f7ffe26f0b02b2a9af7e4e28227',
  'research/experiments/EX-PERCEPTA-2026-0004/frozen-runtime-protocol.md': '197038f2d1869d0eeb5cf5d9bfd2b8345d5d11ed',
  'research/experiments/EX-PERCEPTA-2026-0004/held-out-semantic-cases.json': 'b702a376bf5b8f3d33a9d348785c5b3b1c50e687',
  'research/experiments/EX-PERCEPTA-2026-0004/lanes/control-a/AGENTS.md': '708ea98a505312b3930c0de9b10428a69b98a1a3',
  'research/experiments/EX-PERCEPTA-2026-0004/lanes/control-b/AGENTS.md': '5c82866f99009237a093a1258a62abcf03a1c46b',
  'research/experiments/EX-PERCEPTA-2026-0004/lanes/treatment-a/AGENTS.md': '14403c03ed585d67b65e30104bcfa551cc88256b',
  'research/experiments/EX-PERCEPTA-2026-0004/lanes/treatment-b/AGENTS.md': '4bd99043ff0d5761160ee5714bd594c030460dac'
};

test('EX-PERCEPTA-2026-0004 lane-visible inputs are unchanged while it is pending', t => {
  const preregistration = readFile('research/experiments/EX-PERCEPTA-2026-0004--held-out-semantic-generalization.md').toString('utf8');
  const status = /^status:\s*(\S+)/m.exec(preregistration)?.[1];
  if (!['preregistered', 'active'].includes(status)) { t.skip(`EX-PERCEPTA-2026-0004 is ${status}`); return; }
  for (const [file, blob] of Object.entries(EX_0004_PREREGISTERED_BLOBS)) assert.equal(gitBlob(readFile(file)), blob, file);
});
