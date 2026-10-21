# 27 · Backup JSON versionado

Status: ready-for-agent
Blocked by: 08
Cubre: 3MT-40, 41

## What to build

Exportar toda la información a JSON con `schemaVersion` y restaurarla reemplazando o fusionando por Id; sirve para mover datos entre Android y la PWA.

## Acceptance criteria

- [ ] Exportar configuración, tareas, asignaciones e historial
- [ ] Restaurar con confirmación, en modo reemplazar o fusionar
- [ ] Ida y vuelta Android ↔ PWA verificada
- [ ] Fixture de backup v1 que sigue restaurándose tras cambios de esquema

## Blocked by

- 08-cierre-de-dia

## Comments
