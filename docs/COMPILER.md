# Contract Compiler

The first Percepta compiler is intentionally small.

It takes a typed `ScreenContract` and either rejects it with deterministic validation issues or produces two artifacts:

1. agent implementation guidance;
2. a verification plan.

## Contract

```text
ScreenContract
    |
    v
Validation.validate
    |
    +-- invalid --> ValidationIssue list
    |
    +-- valid
          |
          v
    Compilation.compile
          |
          +--> AgentGuidance
          |
          +--> VerificationPlan
```

## Why this is a compiler

The output is not another copy of the UI specification.

The source contract expresses product semantics in typed form. The compiler projects those semantics into artifacts for different consumers while preserving a single source of meaning.

The current target artifacts are deliberately text/data oriented. Browser-specific verification comes later through adapters.

## Agent guidance

Generated guidance contains:

- screen purpose;
- primary user question;
- required semantic regions;
- hierarchy and visibility;
- capability projection;
- domain-state projections;
- forbidden outcomes;
- responsive semantic obligations;
- required and supporting verification evidence.

The guidance explicitly preserves implementation freedom where the source contract does not constrain it.

## Verification plan

The verification plan contains the declared evidence requirements in typed form.

It does not mark evidence complete and does not decide whether work is complete.

Future adapters will produce `VerificationResult` records against these obligations. ROS or another governing policy can then use those results when deciding completion.

## Non-goals of the first compiler

This milestone does not yet:

- parse YAML or JSON;
- emit DOM selectors;
- run a browser;
- capture screenshots;
- perform accessibility scans;
- call a vision model;
- infer missing product requirements.

Those capabilities belong in later adapters or serialization layers.

## Canonical example

`examples/Percepta.Examples/IndyInit.fs` is the typed canonical form of the Indy Init Investigation Workspace.

The existing YAML example remains a human-readable exploration artifact. The typed example is executable and participates in compiler tests.
