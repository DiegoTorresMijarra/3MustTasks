# 30 · Despliegue de la PWA en GitHub Pages

Status: in-review
Blocked by: 05
Cubre: 3MT-46

## What to build

Publicación automática de la PWA desde `main` con el base href correcto, HTTPS y funcionamiento offline verificado.

## Acceptance criteria

- [x] Despliegue automático desde `main` (workflow escrito, NO ejecutado; ver Comments)
- [x] Base href correcto en Pages (comprobado en local bajo /3MustTasks/)
- [ ] Instalación y modo offline verificados

## Blocked by

- 05-persistencia-web

## Comments
- pm-3 (2026-10-22): asignado a worker-8. Rama issue/30-despliegue-pwa.
- worker-8: entrega. `.github/workflows/deploy-pages.yml` (build + deploy-pages en push a main y manual), `scripts/set-pwa-base.sh` (base href en index.html y `base` del SW publicado, antes de dotnet publish), 404.html + .nojekyll. Doc en `docs/despliegue-pwa.md`.
- Verificado en local: dotnet publish con base /3MustTasks/ servido bajo ese subpath con servidor estatico: la app arranca y enruta (/3MustTasks/onboarding), manifest en el subpath, y los 77 hashes de service-worker-assets.js coinciden con los ficheros (incl. index.html modificado). Build -warnaserror y 596 tests en verde.
- NO verificado: el navegador embebido no soporta service workers (ni uno trivial se registra) y Chrome no estaba conectado, asi que registro del SW, instalacion y modo offline NO estan comprobados; tampoco el despliegue real. El criterio "Instalacion y modo offline verificados" queda sin marcar.
- Usuario: Settings > Pages > Source = GitHub Actions; merge/push a main o Run workflow; luego checklist de docs/despliegue-pwa.md (SW registrado, offline, instalar). Los actions (upload-pages-artifact@v3, deploy-pages@v4) no se han ejecutado nunca.
