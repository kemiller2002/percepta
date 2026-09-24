# Percepta bootstrap

Percepta is initialized through `scripts/bootstrap-echelon.sh`.

The script is intentionally idempotent. After a successful installation and verification it writes
`.echelon/percepta-bootstrap.complete`; subsequent bootstrap workflow runs leave the installed
baseline unchanged.

The bootstrap owns only initial Echelon capability installation. Later upgrades must use each
capability's lifecycle command and normal Praxis/Ordo work protocol rather than silently following
a moving branch.

## Installed baseline

The current committed baseline is:

- Praxis 3.4.0
- Ordo 1.4.0
- Aegis Core 1.0.0
- Aegis GitHub integration 1.0.0
- Aegis GitHub store 1.0.0
- Forma 0.2.0, consumed from the immutable GitHub release artifact
- Folio 0.3.0, pinned to commit `2b101b6d840a670abb959148fff8e1477c059eda` until its npm package is available
- Limen 0.6.1
- Visual Engineering 1.0.0
- Communication Engineering 1.0.0, pinned to commit `4590d2fe6f7e80b339117d3fbee5803f2dd39122`

The authoritative installed-state records live under `.echelon/`; npm resolution is locked by
`package-lock.json`; Aegis NuGet versions are centrally pinned in `Directory.Packages.props`.
