# Changelog

Formato: [versionado semántico](https://semver.org/lang/es/). `0.x` = en desarrollo.

## [Sin publicar]

### Añadido
- Estilos tribales en ciudades y capitales: anillos elípticos concéntricos con entradas escalonadas;
  pukará de piedra local en terreno rocoso y empalizada de madera en bosque o llanura.
- Aldea orgánica tribal: chozas de tamaños variados, caminos de tierra, granjas en la periferia y
  fogatas.
- Gran salón circular de piedra en las capitales tribales (plantilla `LF_GreatHall`), con el jefe, patio
  con hogar, despensa, almacén y prisión.
- Trincheras, búnkeres con techo grueso (inmunes a morteros), trampas de púas y emboscadas tribales.

## [0.2.0] – 2026-09-27

Distritos, ciudadela y comandante.

### Añadido
- Distritos en ciudades y capitales: cuadrícula de calles (avenidas por los ejes y calles cada
  14–18 casillas) con manzanas militares, civiles y agrícolas. La capital reserva el centro para la
  ciudadela. Pesos editables en `Defs/RuleDefs/LF_Districts.xml`.
- Formas de edificios: en L, claustro con patio, varios edificios con callejones y retranqueados.
- Ciudadela pentagonal en el centro de la capital: salón del mando, despensa, prisión, energía y patio
  de armas, con torretas en las cinco puntas. Plantilla editable (`LF_Citadel`).
- Los defensores comen de la despensa cuando se les acaban las raciones; técnicos de la reserva
  recargan generadores y cañones de torretas.
- Comandante de la capital con guardia de élite en el salón del mando.

### Cambiado
- Los asentamientos empiezan en pueblo (pueblo, ciudad, capital); los puestos avanzados serán parte de
  la expansión de las facciones (Fase 2). Las partidas anteriores se convierten solas.
- Sin cuenta atrás de detección (raids de 4 días) en ciudades y capitales: ya tienen oleadas.
- Los técnicos solo usan el combustible y el acero del almacén de la ciudadela.

### Corregido
- Los defensores ignoraban a otros enemigos (mecanoides, otras facciones) que destruían sus defensas:
  ahora salen si cualquier enemigo daña edificios de la base, no solo el jugador.
- Las oleadas retrasadas por el límite de enemigos llegaban una tras otra: ahora hay al menos 1 hora
  de juego entre oleadas.
- Suelo de madera al aire libre bajo el techo de roca delgado de las montañas.
- La reserva salía a atacar por "hambre urgente" con comida de sobra: la guarnición nace alimentada y
  solo sale si la mayoría tiene hambre.

## [0.1.0] – 2026-09-26

Primera versión de la Fase 1 (jerarquía de asentamientos).

### Añadido
- Rangos de asentamiento: puesto avanzado, pueblo, ciudad y capital, con tamaño, defensores, botín
  y perímetro según el rango. Una capital por facción.
- Refuerzos por oleadas en ciudades y capitales, con llegada según la facción.
- Defensa distribuida: puestos del perímetro y reserva central. Los puestos salen a atacar si los
  hieren, si dañan la base, si pierden defensores, si tienen hambre o tras 2 días.
- Raciones para los defensores; solo comen de ellas.
- Traza italiana para facciones industriales o más: baluartes, portones con revellín, foso y glacis.
- Piezas de fortificación dibujadas en XML (`Defs/FortPieceDefs`).
- Suelos, avenidas y suelos de interior por facción.
- Asedio: la reserva de ciudades y capitales no sale por tiempo ni azar; morteros con munición extra
  y cañones de repuesto.
- Opciones del mod: multiplicadores de defensores, botín y puntos tribales, oleadas, límite de
  enemigos a la vez, animales salvajes.
- Herramientas de depuración: listar rangos, medir rendimiento, generar base de prueba, log del
  motivo de ataque.

### Cambiado
- Menos animales salvajes (25 %) en los mapas de bases NPC.
- Ciudades y capitales evitan sitios con mucha roca y despejan la roca dentro de la muralla.

### Corregido
- Los guardias del perímetro huían solos.
- Las oleadas se retiraban "satisfechas con los daños".
- Morteros ausentes en bases con muralla.
- Puertas eléctricas y paneles solares en bases tribales.
