# 28 · Pulido: identidad visual, accesibilidad y rendimiento

Status: claimed
Blocked by: 07, 11
Cubre: 3MT-42, 43, 44

## What to build

Icono, splash y colores definitivos; contraste AA, etiquetas para lectores de pantalla y áreas táctiles ≥ 48 dp; arranque en frío < 2 s en Android de gama media y WASM con trimming y Brotli.

## Acceptance criteria

- [ ] Iconos de la PWA (maskable) y splash
- [ ] Contraste AA y nivel indicado también por icono o texto
- [ ] Medición de arranque en frío documentada
- [ ] Trimming y compresión Brotli activos

## Blocked by

- 07-tachar-progreso-banner
- 11-pool-ver-recuperar

## Comments
- pm-3 (2026-10-22): incorporar aquí la carga diferida (lazy assembly loading) de las vistas de calendario (Día/Semana/Mes), trasladada desde el criterio no cumplido del 24.
- usuario (2026-10-22, vía pm-3): los estilos del frontal NO se parecen lo suficiente a los mocks de design/. El 28 debe dedicar un pase serio de fidelidad visual pantalla por pantalla (comparar con cada mock, capturas lado a lado en el navegador integrado si es viable) y documentar las diferencias que queden. Revisión por el usuario el 2026-10-23.
- pm-3 (2026-10-22): asignado a worker-10. Rama issue/28-pulido-identidad-a11y-rendimiento. Se lanza en paralelo con el 21 para no perder tiempo; los conflictos se resuelven al integrar.
