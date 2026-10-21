# 04 · Esqueleto Android: host MAUI con SQLite

Status: claimed
Blocked by: 03
Cubre: 3MT-05, parte de 07

## What to build

Host MAUI Blazor Hybrid que arranca en emulador Android mostrando el mismo shell, con la persistencia SQLite (EF Core) detrás de los puertos.

## Acceptance criteria

- [ ] La app arranca en emulador y muestra el shell
- [ ] Migración inicial con el modelo de datos de la spec; base de datos en el directorio de datos de la app
- [ ] La implementación SQLite pasa la suite de contrato (SQLite en memoria)
- [ ] Job de CI que genera el APK debug como artefacto

## Blocked by

- 03-esqueleto-web

## Comments

- pm-1: asignado a worker-3 (rama issue/04-esqueleto-android).
