// Sealed experiment run records (PCT-036, DF-PERCEPTA-2026-0001).
//
// A run record says who ran one lane, evaluator, or reviewer run and in which
// execution. It lives only at artifacts/percepta/experiment-NNNN/runs/<run>.json,
// is written after the first-pass freeze, and names the deterministic
// evidence manifest that was hashed before it existed. Nothing in the
// evaluator (src/**) reads it, so identity can never change evidence status,
// completion, or scoring. Pure functions only; callers do the I/O.

import { createHash } from 'node:crypto';
import { create, executionKeyFrom, validate as validateProvenance } from './praxis-provenance-record.mjs';
import { validateAgainstSchema } from './json-schema-subset.mjs';

export const RUN_CONTRACT = 'percepta.experiment-run';
export const RUN_CONTRACT_VERSION = '1.0.0';
export const SYSTEM = 'percepta';

// Experiments whose artifacts predate PCT-036. They are not backfilled and
// their run-like files (for example experiment-0003/candidate-NN/run.json)
// are a different, legacy shape. Nothing is inferred for them.
export const LEGACY_EXPERIMENT_DIRECTORIES = Object.freeze(['experiment-0002', 'experiment-0003']);

export const RUN_RECORD_PATH = /^artifacts\/percepta\/(experiment-([0-9]{4}))\/runs\/([^/]+)\.json$/;

export const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

export const subjectFor = (experiment, run) => `${SYSTEM}:${experiment}/runs/${run}`;

// Builds a run record. `env` supplies ROS_EXECUTION_ID when Praxis
// propagated one; otherwise the run is keyed EXE-percepta.<runKey>. The actor
// must come from declared identity (see actorFromEnvironment), never guessed.
export const makeRunRecord = ({ experiment, run, role, env = {}, runKey = run, at, actor, reason, firstPassFreezeCommit, manifest }) => ({
  contract: RUN_CONTRACT,
  version: RUN_CONTRACT_VERSION,
  experiment,
  run,
  role,
  sealing: {
    firstPassFreezeCommit,
    deterministicEvidenceManifest: { path: manifest.path, sha256: manifest.sha256 }
  },
  provenance: create({
    subject: subjectFor(experiment, run),
    key: executionKeyFrom(env, SYSTEM, runKey),
    at,
    actor,
    ...(reason === undefined ? {} : { reason })
  })
});

// Problems with one record's content (schema + Praxis codec + Percepta
// rules). Empty means valid.
export const runRecordProblems = (schema, record) => {
  const schemaProblems = validateAgainstSchema(schema, record);
  if (schemaProblems.length) return schemaProblems;
  const reading = validateProvenance(record.provenance);
  const provenanceProblems = reading.kind === 'invalid'
    ? reading.problems.map(item => `provenance.${item.field}: ${item.message}`)
    : reading.kind === 'current' ? [] : [`provenance: must be a current praxis.provenance-record, got ${reading.kind}`];
  const contributions = Object.entries(record.provenance.contributions ?? {});
  return [
    ...provenanceProblems,
    ...(contributions.length !== 1 ? ['provenance.contributions: a run record carries exactly one contribution (the run)'] : []),
    ...contributions.filter(([, entry]) => !entry.operations.includes('created'))
      .map(([key]) => `provenance.contributions.${key}: the run's contribution must record 'created'`),
    ...(record.provenance.subject !== subjectFor(record.experiment, record.run)
      ? [`provenance.subject: expected '${subjectFor(record.experiment, record.run)}'`]
      : [])
  ];
};

// Problems with a record in its place in the repository. `readFile(path)`
// returns a Buffer or throws; paths are repository-relative with '/'.
export const placedRunRecordProblems = (schema, relativePath, record, readFile) => {
  const location = RUN_RECORD_PATH.exec(relativePath);
  if (!location) return [`${relativePath}: run records live only at artifacts/percepta/experiment-NNNN/runs/<run>.json`];
  const [, directory, number, fileRun] = location;
  const contentProblems = runRecordProblems(schema, record);
  if (contentProblems.length) return contentProblems.map(item => `${relativePath}: ${item}`);
  const manifest = record.sealing.deterministicEvidenceManifest;
  const manifestBytes = (() => { try { return readFile(manifest.path); } catch { return undefined; } })();
  return [
    ...(record.experiment.slice(-4) !== number ? [`experiment ${record.experiment} does not match directory ${directory}`] : []),
    ...(record.run !== fileRun ? [`run '${record.run}' does not match file name '${fileRun}.json'`] : []),
    ...(!manifest.path.startsWith(`artifacts/percepta/${directory}/`) || manifest.path.includes('/runs/')
      ? [`deterministic evidence manifest must be inside artifacts/percepta/${directory}/ and outside runs/`]
      : []),
    ...(manifestBytes === undefined
      ? [`deterministic evidence manifest ${manifest.path} does not exist`]
      : [
          ...(sha256(manifestBytes) !== manifest.sha256 ? [`deterministic evidence manifest ${manifest.path} hash mismatch`] : []),
          ...(/(^|[\s/])runs\//m.test(manifestBytes.toString('utf8')) ? [`deterministic evidence manifest ${manifest.path} must not list run records`] : [])
        ])
  ].map(item => `${relativePath}: ${item}`);
};
