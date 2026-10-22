#!/usr/bin/env bash
# Ajusta el base href de la PWA para publicarla bajo un subpath (p. ej. /3MustTasks/ en GitHub Pages).
# Debe ejecutarse ANTES de `dotnet publish`: service-worker-assets.js incluye el hash de index.html.
# Uso: scripts/set-pwa-base.sh /3MustTasks/
set -euo pipefail
base="${1:?Uso: set-pwa-base.sh /subpath/}"
case "$base" in /*/|/) ;; *) echo "El base debe empezar y terminar con '/': $base" >&2; exit 1;; esac
wwwroot="$(dirname "$0")/../src/ThreeMustTasks.App.Web/wwwroot"
sed -i "s|<base href=\"/\" />|<base href=\"$base\" />|" "$wwwroot/index.html"
sed -i "s|^const base = \"/\";|const base = \"$base\";|" "$wwwroot/service-worker.published.js"
grep -q "<base href=\"$base\" />" "$wwwroot/index.html" || { echo "No se pudo ajustar index.html" >&2; exit 1; }
grep -q "^const base = \"$base\";" "$wwwroot/service-worker.published.js" || { echo "No se pudo ajustar service-worker.published.js" >&2; exit 1; }
echo "Base href = $base"
