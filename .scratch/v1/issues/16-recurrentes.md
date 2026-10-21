# 16 · Tareas recurrentes

Status: ready-for-agent
Blocked by: 08, 10
Cubre: 3MT-24, 25

## What to build

El usuario define una regla (diaria, laborables, días de la semana, cada N días, mensual) y cada día se generan las instancias en el plan; si superan el cupo van al pool con aviso.

## Acceptance criteria

- [ ] `RecurrenceRule` y `RecurrenceGenerator` integrados en el cierre de día
- [ ] Tests con calendario (meses de 28 a 31 días)
- [ ] Sección de recurrencia en el formulario y lista de recurrentes en Ajustes (pausar, editar, borrar)
- [ ] Editar una instancia pregunta 'solo esta' o 'esta y las siguientes'

## Blocked by

- 08-cierre-de-dia
- 10-editar-tareas-categorias

## Comments
