// A small, dependency-free JSON Schema (2020-12) subset evaluator for the
// repository's own provenance schemas. It supports exactly the keywords those
// schemas use and throws on any other keyword, so a schema can never be
// silently under-checked: $ref (local "#..." pointers only), $defs, type,
// const, enum, required, properties, additionalProperties, propertyNames,
// pattern, minLength, items, minItems, uniqueItems, allOf, anyOf, oneOf, not,
// if/then/else. Annotation keywords are ignored.

const ANNOTATIONS = new Set(['$schema', '$id', '$comment', 'title', 'description', 'examples', 'default', '$defs', 'format']);
const SUPPORTED = new Set([
  '$ref', 'type', 'const', 'enum', 'required', 'properties', 'additionalProperties', 'propertyNames', 'pattern',
  'minLength', 'items', 'minItems', 'uniqueItems', 'allOf', 'anyOf', 'oneOf', 'not', 'if', 'then', 'else'
]);

const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);

const typeOf = value =>
  value === null ? 'null'
    : Array.isArray(value) ? 'array'
      : Number.isInteger(value) ? 'integer'
        : typeof value;

const matchesType = (value, type) =>
  type === 'number' ? typeof value === 'number' : type === typeOf(value) || (type === 'number' && typeOf(value) === 'integer');

const equal = (left, right) => JSON.stringify(left) === JSON.stringify(right);

const resolve = (root, ref) => {
  if (ref === '#') return root;
  if (!ref.startsWith('#/')) throw new Error(`unsupported $ref '${ref}' (only local references are supported)`);
  return ref.slice(2).split('/').map(part => part.replaceAll('~1', '/').replaceAll('~0', '~'))
    .reduce((node, part) => {
      if (!isObject(node) || !Object.hasOwn(node, part)) throw new Error(`unresolvable $ref '${ref}'`);
      return node[part];
    }, root);
};

const evaluate = (root, schema, value, where) => {
  if (schema === true) return [];
  if (schema === false) return [`${where}: no value is allowed here`];
  for (const keyword of Object.keys(schema)) {
    if (!SUPPORTED.has(keyword) && !ANNOTATIONS.has(keyword)) throw new Error(`unsupported schema keyword '${keyword}' at ${where}`);
  }
  const at = key => `${where}/${key}`;
  const errors = [];
  if (schema.$ref !== undefined) errors.push(...evaluate(root, resolve(root, schema.$ref), value, where));
  if (schema.type !== undefined) {
    const types = Array.isArray(schema.type) ? schema.type : [schema.type];
    if (!types.some(type => matchesType(value, type))) return [...errors, `${where}: expected ${types.join('|')}, got ${typeOf(value)}`];
  }
  if (schema.const !== undefined && !equal(value, schema.const)) errors.push(`${where}: must equal ${JSON.stringify(schema.const)}`);
  if (schema.enum !== undefined && !schema.enum.some(item => equal(item, value))) errors.push(`${where}: must be one of ${JSON.stringify(schema.enum)}`);
  if (typeof value === 'string') {
    if (schema.minLength !== undefined && [...value].length < schema.minLength) errors.push(`${where}: shorter than ${schema.minLength}`);
    if (schema.pattern !== undefined && !new RegExp(schema.pattern, 'u').test(value)) errors.push(`${where}: does not match ${schema.pattern}`);
  }
  if (Array.isArray(value)) {
    if (schema.minItems !== undefined && value.length < schema.minItems) errors.push(`${where}: fewer than ${schema.minItems} items`);
    if (schema.uniqueItems === true && new Set(value.map(item => JSON.stringify(item))).size !== value.length) errors.push(`${where}: items must be unique`);
    if (schema.items !== undefined) value.forEach((item, index) => errors.push(...evaluate(root, schema.items, item, at(index))));
  }
  if (isObject(value)) {
    for (const key of schema.required ?? []) if (!Object.hasOwn(value, key)) errors.push(`${where}: missing required '${key}'`);
    const properties = schema.properties ?? {};
    for (const [key, item] of Object.entries(value)) {
      if (schema.propertyNames !== undefined) errors.push(...evaluate(root, schema.propertyNames, key, `${where}{${key}}`));
      if (Object.hasOwn(properties, key)) errors.push(...evaluate(root, properties[key], item, at(key)));
      else if (schema.additionalProperties !== undefined) {
        errors.push(...(schema.additionalProperties === false
          ? [`${at(key)}: property is not allowed`]
          : evaluate(root, schema.additionalProperties, item, at(key))));
      }
    }
  }
  for (const part of schema.allOf ?? []) errors.push(...evaluate(root, part, value, where));
  if (schema.anyOf !== undefined && !schema.anyOf.some(part => evaluate(root, part, value, where).length === 0)) {
    errors.push(`${where}: does not match any allowed shape`);
  }
  if (schema.oneOf !== undefined && schema.oneOf.filter(part => evaluate(root, part, value, where).length === 0).length !== 1) {
    errors.push(`${where}: must match exactly one allowed shape`);
  }
  if (schema.not !== undefined && evaluate(root, schema.not, value, where).length === 0) errors.push(`${where}: matches a forbidden shape`);
  if (schema.if !== undefined) {
    const branch = evaluate(root, schema.if, value, where).length === 0 ? schema.then : schema.else;
    if (branch !== undefined) errors.push(...evaluate(root, branch, value, where));
  }
  return errors;
};

// Returns a list of error strings; empty means the value conforms.
export const validateAgainstSchema = (schema, value) => evaluate(schema, schema, value, '#');
