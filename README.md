# Living Factions

Mod para RimWorld 1.6 (C# + Harmony) que da vida a las facciones NPC.
Diseño completo en [PLAN.md](PLAN.md); ideas en [IDEAS.md](IDEAS.md).

## Estado

- [x] **Fase 1 (base):** rangos de asentamiento (puesto avanzado, pueblo, ciudad, capital) que
  cambian el tamaño de la base, los defensores, el botín y el perímetro defensivo.
- [ ] Fase 1 (estilo por facción, killbox y murallas de capital)
- [ ] Fase 1.5, 2, 3, 4

## Estructura

```
About/                 Metadatos del mod (About.xml)
1.6/Assemblies/        DLL compilado (no se sube a git; se genera al compilar)
Languages/             Traducciones (Spanish, English)
Source/LivingFactions/ Código C#
  Patches/             Parches de Harmony
```

## Compilar

Requiere .NET SDK (8+). Desde la raíz:

```powershell
dotnet build Source/LivingFactions/LivingFactions.csproj -c Release
```

El DLL queda en `1.6/Assemblies/LivingFactions.dll`. Las rutas del juego y de Harmony están en
`LivingFactions.csproj` (`RimWorldDir`, `HarmonyDll`).

## Probar en el juego

`RimWorld\Mods\LivingFactions` es un enlace (junction) a esta carpeta, así que el juego carga
directamente lo que compilas. Se activa en el menú Mods, después de Harmony.

Con el modo desarrollador activo: menú de depuración → **Living Factions → List settlement tiers**
escribe en el log todos los asentamientos con su rango.

## Flujo de ramas

```
feature/xxx ──PR──> develop ──PR──> main        (main solo recibe PRs)
                       ^                 │
                       └──PR (sync)──────┘       (si main recibe un hotfix)
```

- **`develop`**: rama de trabajo diaria. Las funcionalidades grandes van en `feature/...`.
- **`main`**: versiones estables. Está protegida (ruleset `main-requiere-PR`): solo acepta
  PRs, no permite force-push y no se puede borrar.
- Para publicar una versión se abre un PR de `develop` a `main`. Si algo entra directo a
  `main`, se sincroniza con un PR de `main` a `develop`.

## Código de referencia

El código descompilado del juego está en `C:\Users\USUARIO\RimWorldDecomp` y **no** forma parte
del repositorio (no se puede redistribuir).
