# Diseño — Mocks v1

**Lienzo interactivo:** https://claude.ai/artifact/WiH9sxjTXU2KEHEwYX7qbj
(privado; se comparte desde el menú *Share* del lienzo). La pantalla *Hoy* es interactiva y la barra de navegación enlaza las pantallas entre sí.

## Pantallas

| # | Pantalla | Fuente | Qué valida |
|---|---|---|---|
| 1 | Hoy (en curso) | [Main.dc.html](mocks/Main.dc.html) | Grupos por nivel, progreso de mínimos a completar, tarea fijada, tocar el círculo tacha / tocar el texto edita |
| 2 | Hoy (plan incompleto) | [HoyIncompleto.dc.html](mocks/HoyIncompleto.dc.html) | Aviso de mínimo a añadir + insistencia |
| 3 | Planificar | [Planificar.dc.html](mocks/Planificar.dc.html) | Panel con cupos y pestañas Nueva / Favoritas / Pool |
| 4 | Editar tarea | [NuevaTarea.dc.html](mocks/NuevaTarea.dc.html) | Nivel, categoría, cuándo, fijar, hora, repetición, notas, favorita, archivar/eliminar |
| 5 | Pool | [Pool.dc.html](mocks/Pool.dc.html) | Pendientes / A futuro / Archivadas, filtros, selección múltiple |
| 6 | Calendario · Semana | [Semana.dc.html](mocks/Semana.dc.html) | Resultado por día |
| 7 | Calendario · Mes | [Mes.dc.html](mocks/Mes.dc.html) | Anillos de cumplimiento, rachas |
| 8 | Onboarding | [Onboarding.dc.html](mocks/Onboarding.dc.html) | Combinaciones predefinidas de niveles |
| 9 | Ajustes | [Ajustes.dc.html](mocks/Ajustes.dc.html) | Mínimos añadir/completar/máximo por nivel, recordatorios |

## Sistema visual (provisional)
- Tipografía: **Manrope**.
- Fondo `#F5F6F8`, superficie `#FFFFFF`, texto `#15171C`, texto secundario `#5B6170`, bordes `#E3E6EB`.
- Niveles: Must `#C9372C` · Recommended `#E0A100` (texto `#8A5A00`) · Maybe `#2559C7`.
- Objetivos táctiles ≥ 44 px. El nivel siempre se indica también con texto, no solo con color.

## Sobre las fuentes
`mocks/*.dc.html` + `mocks/canvas.json` son el código fuente del lienzo. Usan el runtime del editor de diseño de claude.ai, así que **no se ven bien abriéndolas directamente en el navegador**. Sirven como copia de seguridad y para regenerar o modificar el lienzo.

Para tener las imágenes en GitHub: exportar cada pantalla desde el lienzo (*Share › Export*) y subirlas a `mocks/png/`.
