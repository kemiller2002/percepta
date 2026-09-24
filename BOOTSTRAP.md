# Percepta bootstrap

Percepta is initialized through `scripts/bootstrap-echelon.sh`.

The script is intentionally idempotent. After a successful installation and verification it writes
`.echelon/percepta-bootstrap.complete`; subsequent bootstrap workflow runs leave the installed
baseline unchanged.

The bootstrap owns only initial Echelon capability installation. Later upgrades must use each
capability's lifecycle command and normal Praxis/Ordo work protocol rather than silently following
a moving branch.
