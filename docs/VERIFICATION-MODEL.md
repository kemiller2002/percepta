# Verification Model

Percepta uses multiple layers of evidence because no single UI testing technique proves product meaning.

## A. Contract validation

Deterministic validation that the contract itself is well formed.

Examples:

- stable identifiers are unique
- required regions exist
- hierarchy is assigned
- required evidence is declared

## B. Structural verification

Verify semantic implementation structure rather than brittle CSS structure.

A DOM adapter may use markers such as:

```html
<section data-percepta-region="hypotheses">
```

Markers are implementation evidence, not product authority.

## C. State-projection verification

Given a known domain state, verify that required user-facing meaning is present.

Example:

```text
Given H1 is Falsified
Then H1 remains represented
And Falsified is perceivable
And actions requiring an active H1 are unavailable
```

## D. Interaction verification

Verify outcomes and discoverability rather than only click mechanics.

Example:

> A user can determine why Confirm Root Cause is unavailable and reach the blocking contradiction without guessing.

## E. Accessibility verification

Accessibility is contract evidence.

Adapters may verify:

- accessible names
- semantic structure
- keyboard behavior
- focus
- text scaling
- reduced motion
- contrast
- non-color state representation

## F. Responsive evidence

Responsive evidence proves preservation of meaning, not identical geometry.

## G. Visual regression

Visual regression is supporting evidence for:

- overlap
- clipping
- missing regions
- severe hierarchy drift
- component regression

Pixel identity is not the primary correctness criterion.

## H. Semantic visual review

A vision-capable reviewer may assess questions such as:

- Does primary information actually appear primary?
- Is blocking work visually buried?
- Does the screen communicate the intended state?

This evidence is probabilistic and should not replace deterministic checks.

## Verification states

A verification obligation can be:

- Passed
- Failed
- Unavailable
- NotApplicable
- AcceptedDeviation

Unavailable is never equivalent to Passed.

## Completion

Percepta produces evidence. A governing system such as ROS decides whether that evidence is sufficient for completion.
