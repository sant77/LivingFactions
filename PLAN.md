# Living Factions – Plan del mod

Mod para RimWorld 1.6 (C# + Harmony) que da vida a las facciones NPC: jerarquía de asentamientos,
guerras entre facciones, eventos con propósito y una ruta con final opcional.

- **Juego:** RimWorld 1.6.4871 rev590 + Royalty, Ideology, Biotech, Anomaly, Odyssey
- **Dependencia:** Harmony (brrainz.harmony, ya instalado desde el Workshop)
- **Ruta de desarrollo del mod:** `RimWorld\Mods\LivingFactions\` (por crear)
- **Ideas sin fase asignada:** ver [IDEAS.md](IDEAS.md)

## Principios

- Todo es **opcional**: cada fase y sistema se activa/desactiva y ajusta en las opciones del mod.
- Todo se **guarda en la partida**. Compatible con partidas nuevas y existentes (los asentamientos
  que ya existen reciben rango al cargar).
- El **RNG es parte del diseño**: nada es totalmente predecible, pero la fuerza relativa pesa.
- Desarrollo **por fases**: cada fase se prueba en el juego antes de pasar a la siguiente.

## Cómo funciona vanilla (investigado en el código descompilado)

| Aspecto | Vanilla | Dónde |
|---|---|---|
| Tamaño de base NPC | 34–38 casillas, igual para todas | `GenStep_Settlement.SettlementSizeRange` |
| Defensores | 1150–1600 puntos, fijo | `SymbolResolver_Settlement.DefaultPawnsPoints` |
| Botín | 1800 de valor de mercado, fijo | `SymbolResolver_Settlement.DefaultLootMarketValue` |
| Perímetro defensivo | ancho 0/2/4 al azar (más probable si tech ≥ industrial) | `SymbolResolver_Settlement.Resolve` |
| Guerra entre NPCs | no existe; solo cambian goodwill | – |
| Refugiados (`Hospitality_Refugee`, Royalty) | facción temporal; al final se van o se unen al jugador | `Royalty/Defs/QuestScriptDefs/Script_Hospitality_Refugee.xml` |

---

## Fase 1 – Jerarquía de asentamientos

Cada asentamiento recibe un **rango** guardado en la partida.

| Rango | Tamaño base | Defensores (pts) | Defensas | Botín |
|---|---|---|---|---|
| Puesto avanzado | ~24–30 | 500–900 | mínimas | bajo |
| Pueblo | ~34–40 (vanilla) | 1100–1600 | vanilla | vanilla |
| Ciudad | ~50–60 | 2500–4000 | perímetro, torretas, morteros | alto |
| **Capital** | ~75–90 | **6000–9000** | murallas, killbox, torretas pesadas, varias líneas, guardia de élite | muy alto, único |

> ⚠️ **Valores iniciales, pendientes de balance en pruebas.** Ver "Pendiente de balance".

- **Una capital por facción.** Proporción inicial propuesta: 1 capital + 2–3 ciudades + resto
  pueblos/puestos (a validar en pruebas).
- **Dificultad de la capital:** nivel final de partida. Solo se toma con preparación
  (asedio, morteros, escuadrón de élite). Multiplicador ajustable en opciones.
- Rango visible en el mapa del mundo (etiqueta/icono + tooltip, ej. "Capital – Nueva Roma").
- **Estilo por facción:**
  - Tribus: capital grande y numerosa, empalizadas, sin torretas.
  - Piratas: fortaleza caótica llena de trampas.
  - Imperio: salas del trono, varias pistas de aterrizaje, guardia cataphract.
  - Casos especiales (mecanoides, facciones de DLC) a revisar.

**Fase 1.5 – Especialización:** cada asentamiento tiene además un tipo (minero, agrícola,
fortaleza, comercial) que cambia su inventario de comercio, su botín y su estilo de defensa.

**Puntos técnicos:**
- `WorldObjectComp` en `Settlement` para guardar el rango.
- Parches Harmony a `GenStep_Settlement.ScatterAt` (tamaño) y `SymbolResolver_Settlement.Resolve`
  (puntos de defensores, botín, perímetro).
- Asignación de rangos al generar el mundo y al cargar partidas antiguas.

---

## Fase 2 – Guerra entre facciones (simulación abstracta)

**Fuerza de facción** = suma de pesos de sus asentamientos por rango, ajustada por nivel tecnológico.

**Tick de guerra** (periódico):
1. Una facción hostil elige como objetivo una base enemiga **cercana** (distancia en el mundo).
2. Resolución por **fuerza relativa + RNG**: probabilidad = curva logística de
   (atacante / defensor), con siempre una posibilidad de sorpresa (~5–10 %).
3. Resultados: **repelido** · **baja de rango** · **conquistada** (cambia de facción) ·
   **arrasada** (ruinas).

**Expansión:** las facciones fundan puestos avanzados, y los puestos crecen con el tiempo
(puesto → pueblo → ciudad).

**Anti-bola de nieve:**
- Bonificación defensiva fuerte para las capitales.
- "Moral de resistencia" para facciones debilitadas.
- Límite de bases por facción.
- Enfriamientos entre ataques.
- Mínimo de facciones vivas.

**Diplomacia entre NPCs:** alianzas, tratados de paz y traiciones. Tus colonos con buena
habilidad Social deben pesar en esto (negociar, mediar), para darle más importancia a la
socialización. Diseño en [IDEAS.md](IDEAS.md).

**Crónica de guerra:** pestaña con el historial de batallas, conquistas y tratados.

**Noticias:** cartas con resumen ("Los Piratas Sangrientos arrasaron Villanueva, ciudad del
Imperio"). Opción: todas / solo importantes.

**Caída de capital:** la facción nombra una nueva capital entre sus ciudades, sufre un malus
temporal y puede entrar en crisis.

### Refugiados que fundan colonia propia (opcional, cuidar balance)

Al terminar bien una misión de refugiados, en vez de unirse al jugador **pueden** (con
probabilidad) fundar un **puesto avanzado** propio: una facción menor aliada, o un puesto de una
facción aliada existente.

- **Contrapesos:**
  - Sin recompensa directa grande.
  - Empiezan débiles y otras facciones pueden atacarlos y destruirlos.
  - Límite de 1–2 activos.
  - Solo si la misión terminó sin muertes.
- **Beneficio:** socio comercial, ayuda o regalos ocasionales. Si los atacan, recibes una misión
  de defensa (Fase 3).
- ⚠️ Pendiente de diseño fino para no desbalancear.

---

## Fase 3 – Eventos y misiones con propósito

Eventos que nacen de la guerra de la Fase 2:

- **Petición de ayuda:** un aliado pide defender su base atacada. Si ayudas, resiste y ganas
  goodwill; si no, se decide por RNG.
- **Refugiados de guerra:** al caer una ciudad llegan refugiados (reutiliza hospitalidad).
- **Botín de guerra:** ventana de tiempo para saquear una base debilitada tras un asedio.
- **Contratos:** una facción te paga por atacar una base de su enemigo. (Tú eres el
  contratado. Contratar mercenarios NPC queda descartado por balance.)
- **Presión militar:** las facciones en guerra contigo o que ganan terreno atacan con más fuerza.
- **Mediación:** misión de conversaciones de paz entre dos facciones NPC.
- Más misiones: ver [IDEAS.md](IDEAS.md).

---

## Fase 4 – Ruta con final (opcional, desactivada por defecto)

Como los finales vanilla, el clímax ofrece **dos opciones**. Estructura propuesta:

- **Dos rutas para llegar al clímax:**
  - **Conquista** ("Hegemonía"): tomar o destruir las capitales hostiles.
  - **Coalición** ("Pacificador"): unir a las facciones restantes en una alianza.
- **Arco por etapas:**
  - Se revela tu ambición y las facciones reaccionan.
  - Se forma una coalición en tu contra (o a tu favor).
  - Asalto final a la última capital, o defensa final.
- **Elección final (2 opciones):**
  - **A) Terminar la partida:** créditos + texto final generado con la historia de la partida.
  - **B) Seguir jugando:** el mundo cambia de forma permanente (ej. eres la hegemonía,
    nuevo equilibrio). **Sin vasallos.**
- ⚠️ Pendiente definir condiciones exactas.

---

## Pendiente de balance (se decide probando)

- [ ] Tamaños y puntos de defensores por rango.
- [ ] Proporción de rangos por facción (¿1 capital + 2–3 ciudades?).
- [ ] Frecuencia del tick de guerra y velocidad de expansión.
- [ ] Curva de probabilidad de la resolución y % de sorpresa.
- [ ] Probabilidad y límites de la colonia de refugiados.
- [ ] Condiciones del final.

## Orden de trabajo

1. [ ] Crear el proyecto C# (`RimWorld\Mods\LivingFactions\`), About.xml, dependencia de Harmony.
2. [ ] Fase 1, luego prueba en juego y balance.
3. [ ] Fase 2, luego prueba y balance.
4. [ ] Fase 3.
5. [ ] Fase 4.
