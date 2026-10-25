# Percepta semantic CLI (`percepta`)

`percepta` checks a rendered page against a Percepta screen contract and writes
evidence that follows `schemas/percepta-evidence.schema.json`. It is separate
from `percepta-repo`, which reports only whether Percepta is installed in a
repository. `percepta-repo` lifecycle health is not semantic evidence.

This archive is self-contained, so it needs no .NET runtime. Keep the
`.playwright/` directory next to the executable.

```sh
percepta version
percepta install-browser                      # once per machine or PLAYWRIGHT_BROWSERS_PATH
percepta doctor --contract .percepta/contracts/<screen>.json
percepta verify --contract .percepta/contracts/<screen>.json \
  --url <local-html-file> --fixture <state.json> [--fixture ...] \
  --out <evidence.json>
percepta validate-evidence --evidence <evidence.json> \
  --source-root <trusted-checkout> \
  --trusted-artifact-metadata <verified-archive>/percepta-artifact.json
```

`verify` exits `0` only when every required evidence category is acceptable.
It exits `3` when a required category is `Failed` or `Unavailable`, and `2` for
invalid input. Treat any exit code other than `0` as not complete.

The archive's `percepta-artifact.json` identifies the released assembly and its
resolved dependencies. Trust it only after verifying the archive checksum and
GitHub build-provenance attestation.

Full guide: <https://github.com/kemiller2002/percepta/blob/main/docs/PERCEPTA-SEMANTIC-CLI.md>

## Maintainers

- `version` holds this release line's version (tags `percepta-semantic-v<version>`).
  To release, bump it on `main`. The release workflow publishes any version
  that has no release yet.
- `package.sh RID OUTPUT_DIR` builds one archive. `smoke.sh` runs a linux-x64
  archive as a consumer would. CI runs both.
