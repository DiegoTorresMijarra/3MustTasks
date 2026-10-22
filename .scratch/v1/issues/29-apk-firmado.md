# 29 · APK firmado publicado en GitHub Releases

Status: in-review
Blocked by: 18, 28
Cubre: 3MT-45

## What to build

Pipeline que firma el APK con un keystore guardado como secreto y lo publica en GitHub Releases al crear un tag de versión.

## Acceptance criteria

- [x] Keystore como secreto de CI (workflow lee los 4 secretos; firma verificada en local con keystore de prueba temporal, ya borrado. Los secretos reales los crea el usuario: ver docs/release-android.md)
- [x] Versión derivada del tag (scripts/version-from-tag.sh: v1.2.3 -> 1.2.3 / versionCode 1002003; comprobado en el APK con aapt2)
- [ ] APK publicado en Releases e instalable (NO verificado: requiere ejecutar el workflow en GitHub con secretos reales e instalar en un móvil; ver docs/release-android.md)

## Blocked by

- 18-recordatorio-planificacion-android
- 28-pulido-identidad-a11y-rendimiento

## Comments
- pm-3 (2026-10-22): asignado a worker-11. Rama issue/29-apk-firmado.
- worker-11 (2026-10-22): entregado. Workflow release-android.yml, scripts/version-from-tag.sh, docs/release-android.md. Publicación en Releases e instalación pendientes de verificar por el usuario.
