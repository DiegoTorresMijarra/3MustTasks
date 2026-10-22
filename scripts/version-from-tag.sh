#!/usr/bin/env bash
# Deriva la versión de la app Android a partir de un tag.
# Uso: scripts/version-from-tag.sh v1.2.3[-rc.1]
# Salida (KEY=VALUE, apta para $GITHUB_OUTPUT):
#   display=1.2.3[-rc.1]   -> ApplicationDisplayVersion (versionName)
#   code=1002003           -> ApplicationVersion (versionCode) = MAJOR*1000000 + MINOR*1000 + PATCH
#   prerelease=true|false
# Límites: MINOR y PATCH < 1000; MAJOR < 2147 (versionCode máx. de Android = 2100000000).
set -euo pipefail

tag="${1:-}"
re='^v([0-9]+)\.([0-9]+)\.([0-9]+)(-[0-9A-Za-z.-]+)?$'
if [[ ! "$tag" =~ $re ]]; then
  echo "Tag no válido: '$tag' (se espera vMAJOR.MINOR.PATCH[-sufijo], p. ej. v1.2.3)" >&2
  exit 1
fi

major=$((10#${BASH_REMATCH[1]}))
minor=$((10#${BASH_REMATCH[2]}))
patch=$((10#${BASH_REMATCH[3]}))
suffix="${BASH_REMATCH[4]:-}"

if (( minor > 999 || patch > 999 || major > 2146 )); then
  echo "Versión fuera de rango para el versionCode: $tag" >&2
  exit 1
fi

code=$(( major * 1000000 + minor * 1000 + patch ))
echo "display=${major}.${minor}.${patch}${suffix}"
echo "code=${code}"
if [[ -n "$suffix" ]]; then echo "prerelease=true"; else echo "prerelease=false"; fi
