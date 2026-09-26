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

**Estado:**
- [x] Rangos, tamaño, defensores, botín y perímetro.
- [x] Opciones y acción de depuración.
- [ ] Prueba en el juego y balance.
- [ ] Estilo por facción y defensas de la capital.

### Fase 1.5 – Especialización e ideología de las bases

- **Especialización:** minero, agrícola, fortaleza o comercial. Cambia el inventario de comercio,
  el botín y el estilo de defensa.
- **Ideología de la facción** (con Ideology) en la base:
  - Bases mecanoides: un **mecanizador** con su ejército.
  - Bases de árboles Gauranlen: llenas de árboles y **dríadas que defienden**.
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
