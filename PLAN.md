# Living Factions – Hoja de ruta

Mod para RimWorld 1.6 (C# + Harmony) que da vida a las facciones NPC: jerarquía de asentamientos,
guerras entre facciones, eventos con propósito y una ruta con final opcional.

- **Juego:** RimWorld 1.6 + Royalty, Ideology, Biotech, Anomaly, Odyssey (los DLC son opcionales)
- **Dependencia:** Harmony (`brrainz.harmony`)

## Principios

- Todo es **opcional**: cada fase y sistema se activa, desactiva y ajusta en las opciones del mod.
- Todo se **guarda en la partida**. Compatible con partidas nuevas y existentes.
- El **RNG es parte del diseño**: nada es totalmente predecible, pero la fuerza relativa pesa.
- **Rendimiento:** la simulación del mundo es abstracta (datos que se revisan de vez en cuando).
  Solo se generan mapas y pawns reales cuando el jugador participa.
- Desarrollo **por fases**: cada fase se prueba en el juego antes de pasar a la siguiente.
- Los números de balance son **iniciales** y se ajustan en las pruebas.

## Cómo funciona vanilla

| Aspecto | Vanilla | Dónde |
|---|---|---|
| Tamaño de base NPC | 34–38 casillas, igual para todas | `GenStep_Settlement.SettlementSizeRange` |
| Defensores | 1150–1600 puntos, fijo | `SymbolResolver_Settlement.DefaultPawnsPoints` |
| Botín | 1800 de valor de mercado, fijo | `SymbolResolver_Settlement.DefaultLootMarketValue` |
| Guerra entre NPCs | no existe; solo cambia el goodwill | – |
| Caravanas | te visitan (`TraderCaravanArrival`), encuentros (`CaravanMeeting`), emboscadas a las tuyas (`Ambush`) | no hay caravanas entre NPCs |
| Refugiados (`Hospitality_Refugee`) | facción temporal; al final se van o se unen a ti | Royalty |

---

## Fase 1 – Jerarquía de asentamientos

Cada asentamiento NPC tiene un **rango**.

| Rango | Tamaño | Defensores (pts) | Defensas | Botín |
|---|---|---|---|---|
| Puesto avanzado | 24–30 | 500–900 | mínimas | bajo |
| Pueblo | 34–40 | 1100–1600 | vanilla | vanilla |
| Ciudad | 50–60 | 2500–4000 | perímetro, más torretas y morteros, guardias | alto |
| **Capital** | 76–88 | **6000–9000** | 3 anillos defensivos, murallas, killbox, guardia de élite | muy alto |

- **Una capital por facción.** Su dificultad es de final de partida: solo se toma con preparación.
- Rango visible en el mapa del mundo.
- **Estilo por facción:**
  - Tribus: capital grande, empalizadas, sin torretas.
  - Piratas: fortaleza caótica con trampas.
  - Imperio: salas del trono, pistas de aterrizaje, guardia cataphract.
- Opciones: activar/desactivar, multiplicador de defensores y botín, máximo de ciudades.
- **Solo bases en la superficie.** Las bases orbitales de Odyssey (ej. Gremio de Comerciantes)
  usan otro generador (`SettlementPlatform`) y quedan como en vanilla. Sus rangos propios se harán
  junto con el mod compañero de naves capitales.

**Resultados de las pruebas:**
- Pueblo (Welthuu): igual a vanilla, como estaba previsto.
- Ciudad pirata yttakin (53x52, 2833 pts): se siente la diferencia con un pueblo.
- Capital del Imperio (77x82, 8506 pts): hay diferencia, pero:
  - Solo hay mini torretas: vanilla fija el tipo en `SymbolResolver_EdgeDefense`
    (`Turret_MiniTurret`).
  - Hay lag, probablemente por la cantidad de pawns (50–70).
  - Los defensores pasan al ataque total casi en cuanto hieres a uno (`LordJob_DefendBase`).
- Corregido: los guardias del perímetro formaban un grupo aparte y huían solos.

**Rediseño de la defensa (siguiente paso):**
- **Refuerzos por oleadas** (propuesta del usuario): en ciudades y capitales no están todos los
  defensores desde el inicio. Una guarnición inicial defiende, y el resto llega en oleadas durante
  el asalto. Reduce el lag y hace el asalto más interesante. Decidido:
  - **Origen según la facción** (nunca desde los cuarteles, porque el jugador podría estar ya dentro):
    - Tribus: a pie desde el borde, en grupos (`EdgeWalkInGroups`).
    - Piratas: cápsulas en el borde (`EdgeDrop`); la última, en el centro.
    - Outlanders: a pie desde el borde (`EdgeWalkIn`).
    - Imperio: cápsulas; la élite, en el centro (`CenterDrop`).
  - Reparto: guarnición inicial (ciudad 60 %, capital 40 %) + oleadas del 20 % cada una. Umbrales
    iniciales: ciudad al 40/70 % de pérdidas; capital al 30/55/75 %.
  - Límite de enemigos a la vez (~35; ~45 para tribus): si se supera, la oleada espera.
  - Puestos del perímetro (`LordJob_DefendPoint`, ~40 % de la guarnición) en los lados de la base
    hasta que existan portones, y reserva central (`LordJob_DefendBase`, ~60 %).
  - **Disparo por pérdidas:** cada oleada llega cuando la guarnición pierde cierto % de defensores.
  - **Cantidades iniciales:** ciudad con guarnición del 60 % y 1–2 oleadas; capital con guarnición
    del 40 % y 3 oleadas (la última de élite, guardia del líder).
  - ⚠️ **Tribus:** su fortaleza es el número. El límite de pawns no debe quitarles eso: tendrán más
    oleadas y/o un límite más alto con unidades baratas, en vez de menos pawns.
- **Defensa distribuida:** en vanilla todos los defensores (también los guardias del perímetro)
  defienden el centro de la base (`LordJob_DefendBase` → `rp.rect.CenterCell`) y el perímetro
  queda vacío. Propuesta:
  - Puestos del perímetro: defienden su sector y los portones.
  - Reserva central junto al líder: acude donde se rompe la línea.
  - Oleadas de refuerzo.
- **Medición base** (capital de Cerro mongol, 77x88, 8291 pts): a velocidad x1, 25 FPS y
  47/60 TPS antes del combate. Ya hay lag sin combate.
- **Línea base con la medición automática** (capital de El imperio caído, 86x86, 6351 pts,
  67 enemigos humanos en pie, velocidad x1):
  - Calma: 4.48 ms por tick (máx. 31.9), 57 FPS.
  - Combate: 4.34 ms por tick (máx. 17.3), 40 FPS.
  - A x3 (360 TPS) hace falta ≤ 2.8 ms por tick: correría al ~60 %.
  - Cada pawn cuesta ~0.05–0.06 ms/tick. **Objetivo: ~30–35 enemigos a la vez en el mapa
    (~2.5 ms/tick).** Encaja con una guarnición del 40 % más oleadas.
- **Torretas pesadas:** en ciudades y capitales, parte de las torretas pasan a autocañón
  (`Turret_Autocannon`) y francotiradora (`Turret_Sniper`). Solo con tecnología industrial o mayor.
- **Límite de pawns en el mapa:** los puntos que sobran se convierten en fortificaciones (torretas
  pesadas, morteros, muros) o en oleadas.
- **Calidad sobre cantidad:** preferir unidades élite en lugar de muchos soldados rasos.
- **Opción de rendimiento** en los ajustes del mod.
- **Medir antes y después** con Dubs Performance Analyzer (ya instalado).

**Estado:**
- [x] Rangos, tamaño, defensores, botín y perímetro.
- [x] Opciones y acción de depuración.
- [x] Pruebas de pueblo, ciudad y capital.
- [x] Rediseño de la defensa implementado: oleadas, puestos del perímetro, reserva central, torretas pesadas y límite de enemigos a la vez.
- [ ] Prueba del rediseño a x3 y comparación con la línea base.
  - Prueba 1 (capital outlander de Ithium del suroeste, x1): las torretas pesadas funcionan, las 3
    oleadas llegaron a los umbrales (33/60/80 %), 4 puestos de ~3 pawns y reserva de 26. Guarnición
    de 45 pawns con 3383 pts. **69 animales salvajes** en el mapa (115 pawns en total): calma 4.46 ms/tick.
    La medición de combate no es fiable (el juego estuvo pausado casi todo el tiempo).
  - Corregido: las oleadas se iban "satisfechas con los daños" (`canTimeoutOrFlee` en
    `LordJob_AssaultColony`) y los puestos de ~3 pawns huían tras 1–2 bajas (huida automática de
    vanilla por grupo). Ahora en la capital nadie huye; en la ciudad solo puede huir la reserva central.
  - Animales salvajes reducidos al 25 % en los mapas de bases NPC (ajustable). El límite por número
    de pawns queda para después, según la próxima medición.
- [ ] El líder de la facción vive en su capital y la defiende.
- [ ] Rango visible en el mapa del mundo (icono o marca).
- [ ] Estructura de la capital: muralla, portón o killbox, recinto central, distritos.
- [ ] Habitaciones nuevas: prisión, armería, hospital, cuartel del líder.
- [ ] Energía por facción: químico (piratas), eólico (outlanders), geotérmico (capitales con
  géiser), molino (ríos). Central de energía protegida en el recinto central, que el jugador puede
  atacar para apagar las torretas. Vanilla: `GenStep_Power` conecta todo lo que necesita energía
  y crea solares o baterías; BaseGen solo coloca plantas solares o de leña.
- [ ] Estilo por facción.
- [ ] Botín único de capital.

### Fase 1.5 – Especialización e ideología de las bases

- **Especialización:** minero, agrícola, fortaleza o comercial. Cambia el inventario de comercio,
  el botín y el estilo de defensa.
- **Ideología de la facción** (con Ideology) en la base:
  - Bases mecanoides: un **mecanizador** con su ejército.
  - Bases de árboles Gauranlen: llenas de árboles y **dríadas que defienden**. En vanilla los NPC
    nunca usan árboles ni dríadas: BaseGen no los genera y las misiones excluyen a las dríadas
    (`!RaceProps.Dryad`). El meme de árboles de una facción NPC no cambia su base.
  - La ideología afecta el armamento y las defensas.

---

## Fase 2 – Guerra y diplomacia entre facciones

**Fuerza de facción** = suma de sus asentamientos según el rango, ajustada por nivel tecnológico.
El nivel tecnológico de las facciones es **estático**.

**Tick de guerra** (periódico):
1. Una facción hostil elige como objetivo una base enemiga cercana.
2. Resolución por **fuerza relativa + RNG** (curva logística y un % de sorpresa).
3. Resultados: repelido · baja de rango · conquistada · arrasada.

**Expansión:** las facciones fundan puestos avanzados, y los puestos crecen con el tiempo.

**Anti-bola de nieve:** bonificación defensiva de las capitales, moral de resistencia,
límite de bases, enfriamientos y un mínimo de facciones vivas.

**Personalidad de facción:** sale sesgada por tipo de facción, con RNG, y puede cambiar con el líder.
- Saqueadora: más raids (ej. piratas).
- Expansionista: se expande más (ej. Imperio).
- Comerciante: más caravanas.
- Defensiva: bases más fortificadas.

**Diplomacia:** alianzas, tratados de paz y traiciones entre facciones NPC. La habilidad Social de
tus colonos y tu líder pesan en las negociaciones.

**Caída de capital:** la facción nombra una nueva capital, sufre un malus temporal y puede caer
en crisis.

**Crónica de guerra:** pestaña con el historial de batallas, conquistas y tratados.
**Noticias:** cartas con los hechos importantes (configurable).

**Refugiados que fundan colonia (opcional):** al terminar bien una misión de refugiados, pueden
fundar un puesto avanzado propio en lugar de unirse a ti. Es débil, lo pueden atacar, hay un límite
de 1–2 activos y solo ocurre si la misión terminó sin muertes.

---

## Fase 3 – Eventos y misiones con propósito

- **Petición de ayuda:** un aliado pide defender su base atacada.
- **Refugiados de guerra:** llegan refugiados cuando cae una ciudad.
- **Botín de guerra:** ventana de tiempo para saquear una base debilitada tras un asedio.
- **Contratos:** una facción te paga por atacar una base de su enemigo.
- **Presión militar:** las facciones en guerra o que ganan terreno atacan con más fuerza.
- **Mediación:** conversaciones de paz entre dos facciones NPC.
- **Rescate en zona de guerra:** rescatar a alguien en un mapa donde pelean dos facciones.
- **Recompensa por un líder** de facción: capturarlo o eliminarlo.
- **Líderes capturados:**
  - Ofertas de rescate (con valor que baja con el tiempo).
  - Raid para rescatarlo.
  - Si es de una facción aliada, te piden rescatarlo.
  - Si no se resuelve a tiempo, la facción nombra otro líder y el capturado pasa a ser un
    prisionero normal.
- **Colonos capturados en un asalto fallido** (espejo de los líderes capturados):
  - **Problema vanilla:** un colono incapacitado pero vivo mantiene abierto el mapa
    (`MapPawns.IsValidColonyPawn`). El secuestro al cerrar el mapa casi nunca ocurre: los caídos se
    desangran mientras los defensores los ignoran.
  - **Cierre forzado con tiempo de rescate:** si ya no queda ningún colono consciente en el mapa,
    empieza una cuenta atrás (por defecto 12 h de juego, ajustable), avisada con una carta. En ese
    tiempo el jugador puede enviar otra caravana o cápsulas para rescatarlos. Si nadie llega, el
    mapa se cierra y los caídos pasan a ser prisioneros de esa base.
  - ⚠️ 12 h es demasiado: la mayoría moriría desangrada. Por definir entre 3–4 h, o 3–4 h con los
    caídos estabilizados.
  - **Mejora posterior: captura visible.** Los defensores cargan a los caídos a la prisión de la
    base y los curan, pero solo cuando no hay combate cerca. Se basa en `JobGiver_Kidnap`,
    `JobDriver_CarryDownedPawn` y `JobDriver_TendPatient`, más dos tareas nuevas en la duty
    `DefendBase`. Se hace después de la habitación de prisión (Fase 1). Dificultad media:
    ~300–400 líneas y 2–3 rondas de prueba.
  - El mod guarda **en qué base** está cada prisionero, y esa base se marca en el mapa del mundo.
  - Formas de recuperarlo:
    - Rescate pagado (requiere consola de comunicaciones).
    - Canje por un prisionero suyo (requiere consola).
    - **Misión de rescate armada** contra esa base (**no** requiere consola).
  - Si no lo recuperas, se une a ellos y puede aparecer en raids contra ti.
- **Caravanas NPC entre asentamientos:** escoltarlas o emboscarlas. Son datos abstractos con un
  máximo de 3–5 activas y pawns solo si intervienes. Si llegan, el destino crece; si las emboscas,
  cambia el goodwill.

---

## Fase 4 – Progresión del jugador y final (opcional, desactivada por defecto)

**Progresión:**
- La **capital del jugador** es su colonia principal.
- **Líder del jugador:** un sistema parecido al rol de líder de Ideology, propio del mod.
- Un **disparador** por riqueza y número de asentamientos propios desbloquea la expansión, nuevos
  eventos y la ruta al final. Una colonia recién empezada no puede expandirse.

**Final:**
- Dos rutas: **Conquista** (tomar las capitales hostiles) o **Coalición** (unir a las facciones).
- Arco por etapas: tu ambición se revela, se forma una coalición y llega el asalto o la defensa final.
- **Elección final**, como en los finales vanilla:
  - **A)** Terminar la partida con créditos y una crónica de la historia.
  - **B)** Seguir jugando en un mundo cambiado.

---

## Mods compañeros

Mods aparte, compatibles con Living Factions, que también funcionan solos.

### Leyendas

Los actos de los colonos los marcan con **títulos permanentes**:
- **Héroe** para quien destruye la anomalía y **Participante** para los demás.
- **Corrompido** si la toman.
- **Conquistador**, **Libertador**, **Superviviente**.

**Efectos:** sutiles en combate y comercio. Interacción nueva "contar historias de guerra" que da
buen ánimo. Boost **fuerte** en negociaciones.

**Interacciones más ricas:**
- Vínculos por hechos compartidos: hermanos de armas, deuda de vida, rivalidad.
- Conversaciones sobre hechos concretos y recuerdos que duran años.

### Naves capitales

Gravships de Odyssey como capital móvil de una facción. Incluye rangos para las bases orbitales
(plataformas más grandes y mejor defendidas).

---

## Orden de trabajo

1. [x] Proyecto C#, About.xml, Harmony, repositorio.
2. [ ] Fase 1: pruebas y balance, estilo por facción.
3. [ ] Fase 1.5
4. [ ] Fase 2
5. [ ] Fase 3
6. [ ] Fase 4
7. [ ] Mods compañeros
