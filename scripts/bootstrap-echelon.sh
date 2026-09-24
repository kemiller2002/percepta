#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

MARKER=".echelon/percepta-bootstrap.complete"

if [[ -f "$MARKER" ]]; then
  echo "Percepta Echelon bootstrap is already complete."
  exit 0
fi

export ECHELON_HOME="${ECHELON_HOME:-$HOME/.echelon}"
export PATH="$ECHELON_HOME/bin:$PATH"

echo "Installing native Echelon toolchain..."
curl -fsSL https://raw.githubusercontent.com/kemiller2002/praxis/main/scripts/install-native.sh | sh
export PATH="$ECHELON_HOME/bin:$PATH"

echo "Installing Praxis + Ordo..."
echelon setup

echo "Initializing repository capabilities..."
praxis init
ordo init

echo "Installing application and context packages..."
npm install

echo "Initializing Visual Engineering..."
npx visual-engineering init

echo "Initializing Communication Engineering..."
npx communication-engineering init

echo "Initializing Limen..."
npx limen init

echo "Restoring and compiling the Aegis-enabled F# foundation..."
dotnet restore src/Percepta.Foundation/Percepta.Foundation.fsproj
dotnet build src/Percepta.Foundation/Percepta.Foundation.fsproj --configuration Release --no-restore

echo "Verifying Echelon repository capabilities..."
praxis verify
ordo verify
npx visual-engineering verify --strict
npx communication-engineering verify --strict
npx limen verify

mkdir -p .echelon
cat > "$MARKER" <<'EOF'
Percepta Echelon baseline installed and verified.

Required stack:
- Praxis
- Ordo
- Aegis Core
- Aegis GitHub integration
- Aegis GitHub store
- Forma
- Folio
- Limen
- Visual Engineering
- Communication Engineering

Package and lifecycle versions are pinned by the committed manifests and lockfiles.
EOF

echo "Percepta Echelon bootstrap completed successfully."
