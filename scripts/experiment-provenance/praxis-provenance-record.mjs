// Local codec for the Praxis provenance interchange record
// (`praxis.provenance-record`, major version 1).
//
// Contract source: kemiller2002/praxis @ b32677d (interchange record introduced in 58cf46a)
//   schemas/provenance-record.schema.json, schemas/artifact-provenance.schema.json,
//   schemas/provenance-actor.schema.json, docs/agent-provenance.md,
//   RQ-ROS-2026-A010, RQ-ROS-2026-A013..A015, DF-ROS-2026-A037.
//
// This file is a small, dependency-free reimplementation of that contract. It
// has no package or project reference to Praxis. It never rewrites a record:
// readers return the raw JSON value untouched next to the interpreted view, so
// the JSON a caller stores or forwards is always the JSON it received.
//
// Identity recorded here is self-reported provenance. It is not
// authentication, authorization, evidence, or evidence weight.

export const CONTRACT_NAME = 'praxis.provenance-record';
export const SUPPORTED_MAJOR = 1;
export const CURRENT_VERSION = '1.0.0';
export const MAX_SOURCE_DEPTH = 16;
export const UNKNOWN = 'unknown';

const VERSION_PATTERN = /^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$/;
const KIND_PATTERN = /^(agent|human|automation|unknown|x-[a-z0-9][a-z0-9-]*)$/;
const OPERATION_PATTERN = /^(created|modified|reviewed|approved|superseded|migrated|x-[a-z0-9][a-z0-9-]*)$/;
const EXECUTION_KEY = /^EXE-[A-Za-z0-9._-]+$/;
const CONTRIBUTION_KEY = /^CTB-[A-Za-z0-9._-]+$/;
const TIMESTAMP = /^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,9})?Z$/;
const REFERENCE = /^\S+$/;
const SYSTEM = /^[a-z][a-z0-9-]*$/;
const RUN = /^[A-Za-z0-9_-][A-Za-z0-9._-]*$/;
const PRAXIS_EXECUTION = /^EXE-[0-9]{8}T[0-9]{9}Z-[0-9a-f]{8}$/;

// Recognizes unambiguous credential shapes (a guard against accidents, not a
// secret scanner). Mirrors Praxis `Credentials.looksLikeCredential`.
const CREDENTIAL_PATTERNS = [
  /\bsk-(?:ant-|proj-)?[A-Za-z0-9_-]{16,}/,
  /\bgh[pousr]_[A-Za-z0-9]{20,}/,
  /\bgithub_pat_[A-Za-z0-9_]{20,}/,
  /\bxox[abposr]-[A-Za-z0-9-]{10,}/,
  /\bAKIA[0-9A-Z]{16}\b/,
  /\bAIza[0-9A-Za-z_-]{30,}/,
  /-----BEGIN [A-Z ]*PRIVATE KEY-----/,
  /\bbearer\s+[A-Za-z0-9._~+/=-]{16,}/i,
  /\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}/,
  /\b(?:api[_-]?key|access[_-]?token|secret|password|passwd)\s*[=:]\s*\S{8,}/i
];

export const looksLikeCredential = value =>
  typeof value === 'string' && CREDENTIAL_PATTERNS.some(pattern => pattern.test(value));

const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const isString = value => typeof value === 'string';
const problem = (field, message) => Object.freeze({ field, message });
const at = (prefix, field) => (prefix === '' ? field : `${prefix}.${field}`);

export const isExecutionKey = key => EXECUTION_KEY.test(key);
export const isValidKey = key => EXECUTION_KEY.test(key) || CONTRIBUTION_KEY.test(key);
export const isPraxisExecutionId = key => PRAXIS_EXECUTION.test(key);

export const isTimestamp = value =>
  isString(value) && TIMESTAMP.test(value) && !Number.isNaN(instant(value));

// Milliseconds since the epoch; sub-millisecond digits are truncated so any
// 1..9 digit fraction parses. Invalid timestamps sort last, so they can never
// masquerade as the originating contribution.
export const instant = value => {
  if (!isString(value) || !TIMESTAMP.test(value)) return Number.POSITIVE_INFINITY;
  const parsed = Date.parse(value.replace(/\.([0-9]{1,3})[0-9]*Z$/, '.$1Z'));
  return Number.isNaN(parsed) ? Number.POSITIVE_INFINITY : parsed;
};

export const parseVersion = value => {
  const match = isString(value) ? VERSION_PATTERN.exec(value) : null;
  return match ? Object.freeze({ major: Number(match[1]), minor: Number(match[2]), patch: Number(match[3]) }) : null;
};

// `EXE-<system>.<run>`: how a system with no propagated ROS_EXECUTION_ID keys
// its own run (RQ-ROS-2026-A014). Never mints a Praxis-shaped EXE id.
export const foreignExecutionKey = (system, run) => {
  if (!SYSTEM.test(system ?? '')) throw new Error(`system '${system}' must match ^[a-z][a-z0-9-]*$`);
  if (!RUN.test(run ?? '')) throw new Error(`run '${run}' must match ^[A-Za-z0-9_-][A-Za-z0-9._-]*$`);
  return `EXE-${system}.${run}`;
};

// The execution a contribution is keyed by: the propagated Praxis execution
// when ROS_EXECUTION_ID is set, otherwise this system's own namespaced run.
export const executionKeyFrom = (env, system, run) => {
  const propagated = env?.ROS_EXECUTION_ID;
  if (isString(propagated) && propagated.trim() !== '') {
    if (!isExecutionKey(propagated.trim())) throw new Error('ROS_EXECUTION_ID is not an execution id (EXE-...)');
    return propagated.trim();
  }
  return foreignExecutionKey(system, run);
};

// ---- reading ---------------------------------------------------------------

const stringList = (field, value) =>
  value === undefined
    ? { ok: [] }
    : Array.isArray(value) && value.every(isString)
      ? { ok: value }
      : { problems: [problem(field, 'must be an array of strings')] };

const readContribution = (key, value) => {
  const prefix = `contributions.${key}`;
  if (!isObject(value)) return { problems: [problem(prefix, 'contribution must be an object')] };
  const operations = stringList(`${prefix}.operations`, value.operations);
  const evidence = stringList(`${prefix}.evidence`, value.evidence);
  const actor = value.actor;
  const actorProblems = !isObject(actor)
    ? [problem(`${prefix}.actor`, actor === undefined ? 'actor is required' : 'actor must be an object')]
    : !isString(actor.kind)
      ? [problem(`${prefix}.actor`, 'actor.kind is required')]
      : !KIND_PATTERN.test(actor.kind)
        ? [problem(`${prefix}.actor`, `unknown actor kind '${actor.kind}'`)]
        : [];
  const operationProblems = (operations.ok ?? []).flatMap(operation =>
    OPERATION_PATTERN.test(operation) ? [] : [problem(`${prefix}.operations`, `unknown operation '${operation}'`)]);
  const duplicateProblems = operations.ok && new Set(operations.ok).size !== operations.ok.length
    ? [problem(`${prefix}.operations`, 'operations must be unique')]
    : [];
  const problems = [
    ...(operations.problems ?? []), ...(evidence.problems ?? []), ...actorProblems, ...operationProblems, ...duplicateProblems
  ];
  return problems.length
    ? { problems }
    : {
        ok: Object.freeze({
          key,
          operations: Object.freeze([...operations.ok]),
          at: isString(value.at) ? value.at : '',
          last: isString(value.last) ? value.last : undefined,
          actor,
          reason: isString(value.reason) ? value.reason : undefined,
          evidence: Object.freeze([...evidence.ok])
        })
      };
};

const byTimeThenKey = (left, right) => {
  const byTime = instant(left.at) - instant(right.at);
  if (byTime !== 0 && !Number.isNaN(byTime)) return byTime < 0 ? -1 : 1;
  return left.key < right.key ? -1 : left.key > right.key ? 1 : 0;
};

const readBody = (raw, version, depth) => {
  const contributions = !isObject(raw.contributions)
    ? {
        problems: [problem('contributions', raw.contributions === undefined
          ? 'contributions is required'
          : 'contributions must be an object keyed by execution (EXE-...) or contribution (CTB-...) ID')]
      }
    : (() => {
        const results = Object.entries(raw.contributions).map(([key, value]) => readContribution(key, value));
        const problems = results.flatMap(result => result.problems ?? []);
        return problems.length ? { problems } : { ok: results.map(result => result.ok).sort(byTimeThenKey) };
      })();
  const derivedFrom = stringList('derivedFrom', raw.derivedFrom);
  const subject = raw.subject === undefined
    ? { ok: undefined }
    : isString(raw.subject) ? { ok: raw.subject } : { problems: [problem('subject', 'subject must be a string')] };
  const sources = raw.sources === undefined
    ? { ok: [] }
    : !isObject(raw.sources)
      ? { problems: [problem('sources', 'sources must be an object keyed by lineage reference')] }
      : depth >= MAX_SOURCE_DEPTH && Object.keys(raw.sources).length > 0
        ? { problems: [problem('sources', `lineage snapshots nest deeper than ${MAX_SOURCE_DEPTH} levels`)] }
        : (() => {
            const results = Object.entries(raw.sources).map(([reference, value]) => {
              const reading = readAt(value, depth + 1);
              return reading.kind === 'invalid'
                ? { problems: reading.problems.map(item => problem(at(`sources.${reference}`, item.field), item.message)) }
                : { ok: Object.freeze({ reference, reading }) };
            });
            const problems = results.flatMap(result => result.problems ?? []);
            return problems.length ? { problems } : { ok: results.map(result => result.ok) };
          })();
  const problems = [contributions, derivedFrom, subject, sources].flatMap(part => part.problems ?? []);
  return problems.length
    ? { problems }
    : {
        ok: Object.freeze({
          version,
          subject: subject.ok,
          contributions: Object.freeze(contributions.ok),
          derivedFrom: Object.freeze([...derivedFrom.ok]),
          sources: Object.freeze(sources.ok)
        })
      };
};

function readAt(raw, depth) {
  if (!isObject(raw)) return { kind: 'invalid', problems: [problem('', 'a provenance record must be a JSON object')] };
  if (raw.contract === undefined && raw.version === undefined) {
    const body = readBody(raw, parseVersion(CURRENT_VERSION), depth);
    return body.problems ? { kind: 'invalid', problems: body.problems } : { kind: 'unversioned', record: body.ok, raw };
  }
  if (raw.contract !== CONTRACT_NAME) return { kind: 'invalid', problems: [problem('contract', `contract must be '${CONTRACT_NAME}'`)] };
  const version = parseVersion(raw.version);
  if (!version) return { kind: 'invalid', problems: [problem('version', 'version must be a semantic version (MAJOR.MINOR.PATCH)')] };
  if (version.major !== SUPPORTED_MAJOR) return { kind: 'unsupported', version: raw.version, raw };
  const body = readBody(raw, version, depth);
  return body.problems ? { kind: 'invalid', problems: body.problems } : { kind: 'current', record: body.ok, raw };
}

// Structure only. Returns { kind: 'current' | 'unversioned' | 'unsupported' |
// 'invalid', record?, raw?, problems? }. `raw` is the caller's value, unchanged.
export const read = raw => readAt(raw, 0);

// ---- rules -----------------------------------------------------------------

const actorProblems = (prefix, actor) => [
  ...(!isString(actor.id) || actor.id.trim() === ''
    ? [problem(`${prefix}.id`, "actor id must not be empty; use 'unknown' when it is not known")]
    : []),
  ...['provider', 'model', 'runtime'].flatMap(field =>
    actor[field] !== undefined && !isString(actor[field]) ? [problem(`${prefix}.${field}`, `${field} must be a string`)] : []),
  ...(actor.kind === 'agent'
    ? ['provider', 'model', 'runtime'].flatMap(field =>
        !isString(actor[field])
          ? [problem(`${prefix}.${field}`, `agent actor must record ${field} (use 'unknown' when it is not known)`)]
          : actor[field].trim() === '' ? [problem(`${prefix}.${field}`, `agent actor ${field} must not be empty`)] : [])
    : []),
  ...['id', 'provider', 'model', 'runtime'].flatMap(field =>
    looksLikeCredential(actor[field])
      ? [problem(`${prefix}.${field}`, 'value looks like a credential; identity must never carry secrets')]
      : [])
];

const contributionProblems = (prefix, contribution) => {
  const field = name => `${prefix}.contributions.${contribution.key}.${name}`.replace(/^\./, '');
  return [
    ...(!isValidKey(contribution.key)
      ? [problem(field('key'), `contribution key '${contribution.key}' must be an execution ID (EXE-...) or a contribution ID (CTB-...)`)]
      : []),
    ...(contribution.operations.length === 0 ? [problem(field('operations'), 'contribution must record at least one operation')] : []),
    ...(!isTimestamp(contribution.at) ? [problem(field('at'), `'${contribution.at}' is not an ISO-8601 UTC timestamp`)] : []),
    ...(contribution.last !== undefined && !isTimestamp(contribution.last)
      ? [problem(field('last'), `'${contribution.last}' is not an ISO-8601 UTC timestamp`)]
      : contribution.last !== undefined && instant(contribution.last) < instant(contribution.at)
        ? [problem(field('last'), 'last must not precede at')]
        : []),
    ...actorProblems(field('actor'), contribution.actor),
    ...(contribution.actor.kind === 'agent' && !isExecutionKey(contribution.key)
      ? [problem(field('key'), 'an agent contribution must be keyed by the execution (EXE-...) that produced it')]
      : []),
    ...(contribution.evidence.some(item => item.trim() === '') ? [problem(field('evidence'), 'evidence references must not be empty')] : []),
    ...(looksLikeCredential(contribution.reason)
      ? [problem(field('reason'), 'reason looks like it contains a credential; provenance must never carry secrets')]
      : []),
    ...(contribution.evidence.some(looksLikeCredential)
      ? [problem(field('evidence'), 'an evidence reference looks like a credential; provenance must never carry secrets')]
      : [])
  ];
};

export const originator = record => record.contributions.find(item => item.operations.includes('created'));

const recordProblems = (prefix, depth, record) => {
  const creations = record.contributions.filter(item => item.operations.includes('created'));
  const creationProblems = creations.length > 1
    ? [problem(at(prefix, 'contributions'), `more than one contribution claims 'created': ${creations.map(item => item.key).join(', ')}`)]
    : creations.length === 1
      ? record.contributions
          .filter(item => instant(item.at) < instant(creations[0].at))
          .map(item => problem(at(prefix, `contributions.${item.key}.at`), `contribution precedes the recorded creation (${creations[0].key})`))
      : [];
  const references = [...(record.subject === undefined ? [] : [['subject', record.subject]]), ...record.derivedFrom.map(value => ['derivedFrom', value])];
  const referenceProblems = references.flatMap(([name, value]) => [
    ...(!REFERENCE.test(value) ? [problem(at(prefix, name), `reference '${value}' must be a non-empty token without whitespace`)] : []),
    ...(looksLikeCredential(value) ? [problem(at(prefix, name), 'value looks like a credential; provenance must never carry secrets')] : [])
  ]);
  const lineageProblems = [
    ...(record.subject !== undefined && record.derivedFrom.includes(record.subject)
      ? [problem(at(prefix, 'derivedFrom'), `'${record.subject}' cannot be derived from itself`)]
      : []),
    ...(new Set(record.derivedFrom).size !== record.derivedFrom.length ? [problem(at(prefix, 'derivedFrom'), 'lineage references must be unique')] : []),
    ...record.sources
      .filter(source => !record.derivedFrom.includes(source.reference))
      .map(source => problem(at(prefix, `sources.${source.reference}`), 'a lineage snapshot must name a reference listed in derivedFrom')),
    ...record.sources
      .filter(source => source.reading.kind !== 'unsupported' && source.reading.record.subject !== undefined && source.reading.record.subject !== source.reference)
      .map(source => problem(at(prefix, `sources.${source.reference}.subject`),
        `the snapshot describes '${source.reading.record.subject}', not '${source.reference}'; a lineage snapshot must be the named source's own provenance`))
  ];
  const sourceProblems = record.sources.flatMap(source =>
    source.reading.kind === 'unsupported'
      ? []
      : depth >= MAX_SOURCE_DEPTH
        ? [problem(at(prefix, `sources.${source.reference}`), `lineage snapshots nest deeper than ${MAX_SOURCE_DEPTH} levels`)]
        : recordProblems(at(prefix, `sources.${source.reference}`), depth + 1, source.reading.record));
  return [
    ...record.contributions.flatMap(item => contributionProblems(prefix, item)),
    ...creationProblems,
    ...referenceProblems,
    ...lineageProblems,
    ...sourceProblems
  ];
};

// Reads and applies every structural rule. `unsupported` is not an error: the
// record must be carried verbatim. Returns the reading, with `problems` for
// an invalid record (malformed provenance is reported, never dropped).
export const validate = raw => {
  const reading = read(raw);
  if (reading.kind !== 'current' && reading.kind !== 'unversioned') return reading;
  const problems = recordProblems('', 0, reading.record);
  return problems.length ? { kind: 'invalid', problems } : reading;
};

// ---- lineage chain ---------------------------------------------------------

// Every contribution of a record and its lineage snapshots, the record's own
// first; each link keeps the subject it belongs to, so a source's
// contributors are never presented as the derivative's.
export const chain = (record, depth = 0) => [
  ...record.contributions.map(contribution => Object.freeze({ subject: record.subject, depth, contribution })),
  ...(depth >= MAX_SOURCE_DEPTH
    ? []
    : record.sources.flatMap(source =>
        source.reading.kind === 'unsupported'
          ? []
          : chain({ ...source.reading.record, subject: source.reading.record.subject ?? source.reference }, depth + 1)))
];

// ---- successor (non-destructive hop) check ---------------------------------

export const deepEqual = (left, right) => {
  if (left === right) return true;
  if (Array.isArray(left) || Array.isArray(right)) {
    return Array.isArray(left) && Array.isArray(right) && left.length === right.length && left.every((item, index) => deepEqual(item, right[index]));
  }
  if (isObject(left) && isObject(right)) {
    const leftKeys = Object.keys(left);
    const rightKeys = Object.keys(right);
    return leftKeys.length === rightKeys.length && leftKeys.every(key => Object.hasOwn(right, key) && deepEqual(left[key], right[key]));
  }
  return false;
};

const EXTENDABLE = new Set(['operations', 'evidence', 'last', 'reason']);
const ENVELOPE = new Set(['contributions', 'derivedFrom', 'sources', 'version', 'contract']);

const preservationProblems = (before, after) => [
  ...Object.entries(before)
    .filter(([key, value]) => !ENVELOPE.has(key) && (!Object.hasOwn(after, key) || !deepEqual(value, after[key])))
    .map(([key]) => problem(key, 'field was removed or changed; fields a consumer does not model must be preserved')),
  ...(isObject(before.contributions) && isObject(after.contributions)
    ? Object.entries(before.contributions).flatMap(([key, entry]) => {
        const successor = after.contributions[key];
        return isObject(entry) && isObject(successor)
          ? Object.entries(entry)
              .filter(([name, value]) => !EXTENDABLE.has(name) && (!Object.hasOwn(successor, name) || !deepEqual(value, successor[name])))
              .map(([name]) => problem(`contributions.${key}.${name}`, "field was removed or changed; another contributor's entry must be preserved verbatim"))
          : [];
      })
    : []),
  ...(isObject(before.sources) && isObject(after.sources)
    ? Object.entries(before.sources)
        .filter(([key, value]) => Object.hasOwn(after.sources, key) && !deepEqual(value, after.sources[key]))
        .map(([key]) => problem(`sources.${key}`, 'lineage snapshot must be carried verbatim'))
    : [])
];

const interpretedProblems = (before, after) => {
  const afterByKey = new Map(after.contributions.map(item => [item.key, item]));
  const contributionChanges = before.contributions.flatMap(previous => {
    const field = name => `contributions.${previous.key}${name}`;
    const current = afterByKey.get(previous.key);
    if (!current) return [problem(field(''), 'contribution was removed; provenance history is append-only')];
    return [
      ...(!deepEqual(previous.actor, current.actor) ? [problem(field('.actor'), 'actor was changed; a contribution is never re-attributed')] : []),
      ...(previous.at !== current.at ? [problem(field('.at'), 'the time of the first recorded operation changed')] : []),
      ...previous.operations.filter(item => !current.operations.includes(item)).map(item => problem(field('.operations'), `operation '${item}' was removed`)),
      ...previous.evidence.filter(item => !current.evidence.includes(item)).map(item => problem(field('.evidence'), `evidence '${item}' was removed`)),
      ...(previous.reason !== undefined && current.reason !== previous.reason ? [problem(field('.reason'), 'reason was rewritten')] : [])
    ];
  });
  const previousOrigin = originator(before);
  const currentOrigin = originator(after);
  return [
    ...contributionChanges,
    ...(previousOrigin && currentOrigin && previousOrigin.key !== currentOrigin.key
      ? [problem('contributions', `originator changed from ${previousOrigin.key} to ${currentOrigin.key}`)]
      : []),
    ...before.derivedFrom.filter(item => !after.derivedFrom.includes(item)).map(item => problem('derivedFrom', `lineage reference '${item}' was removed`)),
    ...before.sources
      .filter(source => !after.sources.some(other => other.reference === source.reference))
      .map(source => problem(`sources.${source.reference}`, 'lineage snapshot was removed')),
    ...(before.subject !== undefined && after.subject !== before.subject
      ? [problem('subject', 'subject changed; a different subject needs its own record that derives from this one')]
      : []),
    ...(after.version.major !== before.version.major
      ? [problem('version', 'major version changed in place')]
      : after.version.minor < before.version.minor || (after.version.minor === before.version.minor && after.version.patch < before.version.patch)
        ? [problem('version', 'version was lowered; a consumer must not relabel a newer record as an older one')]
        : [])
  ];
};

// Whether `after` is a non-destructive successor of `before`
// (RQ-ROS-2026-A015). An empty list means preserved.
export const successorProblems = (before, after) => {
  const previous = read(before);
  const current = read(after);
  if (previous.kind === 'unsupported') {
    return deepEqual(before, after) ? [] : [problem('', 'a record in an unsupported major version must be carried verbatim')];
  }
  if (previous.kind === 'invalid') return [problem('', 'the previous record is malformed; refusing to judge a successor of it')];
  if (current.kind === 'invalid') return current.problems;
  if (current.kind === 'unsupported') {
    return [problem('version', 'a supported record was replaced by an unsupported major version; a major version never changes in place')];
  }
  return [...interpretedProblems(previous.record, current.record), ...preservationProblems(before, after)];
};

// ---- writing ---------------------------------------------------------------

// Canonical actor JSON: key order kind, id, provider, model, runtime. A human
// carries no provider/model/runtime; every other kind carries all three, as
// "unknown" when not known. Nothing is guessed.
export const actor = ({ kind = UNKNOWN, id, provider, model, runtime } = {}) => {
  const known = value => (isString(value) && value.trim() !== '' ? value.trim() : undefined);
  const stableId = known(id) ?? (kind !== 'human' && known(provider) && known(runtime) ? `${known(provider)}/${known(runtime)}` : UNKNOWN);
  return kind === 'human'
    ? { kind, id: stableId }
    : { kind, id: stableId, provider: known(provider) ?? UNKNOWN, model: known(model) ?? UNKNOWN, runtime: known(runtime) ?? UNKNOWN };
};

// Identity from the whitelisted, non-secret environment only
// (ROS_ACTOR_KIND, ROS_ACTOR, ROS_TELEMETRY_PROVIDER/MODEL/RUNTIME), with
// GitHub Actions resolving to automation when nothing is declared.
export const actorFromEnvironment = env => {
  const declared = ['ROS_ACTOR_KIND', 'ROS_ACTOR', 'ROS_TELEMETRY_PROVIDER', 'ROS_TELEMETRY_MODEL', 'ROS_TELEMETRY_RUNTIME']
    .some(name => isString(env?.[name]) && env[name].trim() !== '');
  if (!declared && env?.GITHUB_ACTIONS === 'true') {
    return actor({ kind: 'automation', id: 'github/github-actions', provider: 'github', runtime: 'github-actions' });
  }
  const kind = isString(env?.ROS_ACTOR_KIND) && KIND_PATTERN.test(env.ROS_ACTOR_KIND.trim()) ? env.ROS_ACTOR_KIND.trim() : UNKNOWN;
  return actor({
    kind,
    id: env?.ROS_ACTOR,
    provider: env?.ROS_TELEMETRY_PROVIDER,
    model: env?.ROS_TELEMETRY_MODEL,
    runtime: env?.ROS_TELEMETRY_RUNTIME
  });
};

// A fresh record for a subject whose provenance starts here. Throws on any
// rule violation rather than writing malformed provenance.
export const create = ({ subject, key, operations = ['created'], at: time, actor: who, reason, evidence, derivedFrom, sources }) => {
  const contribution = {
    operations: [...operations],
    at: time,
    actor: who,
    ...(reason === undefined ? {} : { reason }),
    ...(evidence === undefined || evidence.length === 0 ? {} : { evidence: [...evidence] })
  };
  const record = {
    contract: CONTRACT_NAME,
    version: CURRENT_VERSION,
    ...(subject === undefined ? {} : { subject }),
    contributions: { [key]: contribution },
    ...(derivedFrom === undefined ? {} : { derivedFrom: [...derivedFrom] }),
    ...(sources === undefined ? {} : { sources })
  };
  const reading = validate(record);
  if (reading.kind === 'invalid') {
    throw new Error(`refusing to write malformed provenance: ${reading.problems.map(item => `${item.field}: ${item.message}`).join('; ')}`);
  }
  return record;
};
