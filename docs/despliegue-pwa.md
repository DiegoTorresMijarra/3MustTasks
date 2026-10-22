# Despliegue de la PWA (GitHub Pages)

Workflow: `.github/workflows/deploy-pages.yml`. Se lanza en cada push a `main` (y a mano con *Run workflow*).

## Qué hace
1. `scripts/set-pwa-base.sh "/<repo>/"` cambia el `<base href>` de `index.html` y la constante `base` de `service-worker.published.js`. Debe ir **antes** de `dotnet publish`, porque `service-worker-assets.js` lleva el hash de `index.html`. Con el repo `DiegoTorresMijarra/3MustTasks` la URL será `https://diegotorresmijarra.github.io/3MustTasks/` y el base `/3MustTasks/`.
2. `dotnet publish src/ThreeMustTasks.App.Web -c Release`.
3. Copia `index.html` a `404.html` (fallback SPA para rutas profundas) y añade `.nojekyll` (para servir `_framework/` y `_content/`).
4. Sube el artefacto y lo despliega con `actions/deploy-pages`.

El `manifest.webmanifest` usa rutas relativas (`start_url`/`scope` = `./`), así que no necesita cambios por subpath. En desarrollo (`/`) no se toca nada: el script solo se ejecuta en CI o a mano.

## Activación (una vez, por el usuario)
Repo en GitHub → Settings → Pages → *Build and deployment* → Source: **GitHub Actions**. Después, un push a `main` (o *Run workflow*) publica. HTTPS lo da Pages.

## Probar en local bajo subpath
```
bash scripts/set-pwa-base.sh /3MustTasks/
dotnet publish src/ThreeMustTasks.App.Web -c Release -o out
git checkout -- src/ThreeMustTasks.App.Web/wwwroot   # revertir el base
```
Servir `out/wwwroot` bajo `/3MustTasks/` con cualquier servidor estático.

## Checklist de verificación manual (tras el primer despliegue)
- Abre la URL de Pages: carga sin 404 en `_framework/*`.
- DevTools → Application → Service Workers: registrado con scope `/3MustTasks/`; Cache Storage `offline-cache-*` con los assets.
- Recarga con *Offline* marcado: la app arranca y funciona (datos en IndexedDB).
- Chrome/Edge: icono *Instalar* en la barra; en Android, *Añadir a pantalla de inicio*.
- Al publicar una versión nueva, el SW nuevo se activa tras cerrar todas las pestañas (comportamiento estándar de Blazor).
