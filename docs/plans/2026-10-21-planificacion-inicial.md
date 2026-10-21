# Plan: llevar el trabajo de la sesión a GitHub (DiegoTorresMijarra/3MustTasks)

## Contexto
Todo lo hecho en la sesión vive en el contenedor y se pierde cuando caduque:
- Repo local `/home/user/3musttasks`: README, `docs/` (PRD, spec técnica, preguntas, backlog) y `design/mocks/` (fuentes de los 9 mocks). Tiene 4 commits.
- Fichero del plan de la sesión: `/root/.claude/plans/vivid-floating-whale.md`.

El usuario quiere subirlo todo a su repo `DiegoTorresMijarra/3MustTasks`, incluidos los planes, y conservar los mocks.

## Pasos
1. **Conectar y clonar:** `add_repo(owner="DiegoTorresMijarra", repo="3MustTasks", access="push")`, clonar en `/home/user/3MustTasks` con el comando que devuelva y llamar a `register_repo_root`. Si da un error de permisos, se lo paso al usuario y paro.
2. **Añadir los planes:** copiar el fichero de plan a `docs/plans/`. Le doy un nombre descriptivo, p. ej. `docs/plans/2026-10-21-planificacion-inicial.md`.
3. **Mocks** (ver la sección siguiente): añadir `design/README.md` con el enlace al lienzo, la lista de pantallas y cómo se ven las fuentes.
4. **Subir según lo que haya en GitHub:**
   - **Repo vacío:** se añade como `origin` al repo local y se hace `git push -u origin main`. Así se conserva el historial de commits.
   - **Repo con contenido:** rama `claude/specs-y-mocks` desde la rama por defecto, copiar README (fusionándolo si ya existe), `docs/` y `design/`, hacer commit y push de la rama. Después le pregunto al usuario si abro un PR.
5. Reintentar el push hasta 4 veces con espera creciente si falla la red.

## Mocks: cómo no perderlos
- **El lienzo no se pierde:** https://claude.ai/artifact/WiH9sxjTXU2KEHEwYX7qbj está guardado en claude.ai, no en el contenedor, y sigue accesible aunque la sesión caduque.
- **Fuentes en git:** `design/mocks/*.dc.html` + `canvas.json` ya están en el repo. Sirven como copia de seguridad y para regenerar el lienzo, pero necesitan el editor de diseño de claude.ai para verse bien (usan su runtime).
- **Imágenes para ver en GitHub:** las genera el usuario desde el lienzo (menú Share › Export, en PNG/PDF) y las sube a `design/mocks/png/`, o me las pasa y yo las subo. Generarlas yo no es fiable: el artefacto es privado y el navegador del contenedor no tiene la sesión de claude.ai iniciada.
- **Opcional, en otro paso:** pasar los mocks a HTML estático puro (sin runtime) en `design/static/` y publicarlos con GitHub Pages, para verlos en el navegador desde GitHub.

## Verificación
- `git log origin/<rama> --oneline` muestra los commits.
- `git ls-tree -r --name-only HEAD` incluye `docs/plans/…`, `docs/0*.md` y `design/mocks/*`.
- Darle al usuario el enlace a la rama o al repo en GitHub.
