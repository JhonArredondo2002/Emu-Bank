# PokeBank

**Un banco de Pokémon para tus partidas de emulador.**

PokeBank lleva la idea del Pokémon Bank a tus partidas guardadas de emulador. Abre tus `.sav`, mira las cajas con sus sprites y mueve Pokémon entre juegos, de Rojo/Azul hasta Ultrasol/Ultraluna.

## Características

- **Hasta 4 partidas a la vez:** eliges una carpeta y la app encuentra tus partidas de mGBA, DeSmuME, melonDS o Citra.
- **Banco propio:** guarda Pokémon fuera de cualquier juego, en archivos `.pkX` que también abre PKHeX.
- **Transferencias oficiales:** Máquina del Tiempo, Parque Compi, Pokétransfer, Poké Transporter y Consola Virtual, igual que en los juegos.
- **Recreación entre generaciones:** si no existe una ruta oficial (por ejemplo, de Gen 4 a Gen 3), crea una copia con todo lo compatible. Antes te muestra qué se pierde.
- **Varios Pokémon de una vez:** selecciona varios y muévelos juntos.
- **Editor completo:** especie, mote, nivel, naturaleza, objeto, shiny, movimientos, IVs/EVs con stats en vivo, evolución y creación de Pokémon. Incluye el análisis de legalidad de PKHeX.
- **Seguro:** antes de modificar una partida, la app hace una copia de seguridad automática.
- **Con estilo:** cada panel toma los colores de su juego, y puedes ponerle tu propio fondo.

## Tecnologías

C# · .NET 10 · Avalonia UI · [PKHeX.Core](https://github.com/kwsch/PKHeX) · sprites de [PokeAPI](https://github.com/PokeAPI/sprites)

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`dotnet --list-sdks` debe mostrar una versión 10.x)
- Visual Studio 2022 (17.14+), Rider o VS Code con C# Dev Kit

## Generar el ejecutable (sin consola)

Haz doble clic en **`Generar-ejecutable.bat`**. Crea `Publicado\PokeBank.exe` (un solo archivo, con el ícono de la app) y un acceso directo **PokeBank** en el escritorio. El .exe incluye .NET, así que también funciona en otros PC con Windows sin instalar nada.

Cada vez que cambies el código, vuelve a ejecutar el .bat para actualizar el .exe.

## Ejecutar desde el código

```bash
dotnet run --project src/PokeBank.App
```

O abre `PokeBank.slnx` en tu IDE y ejecuta `PokeBank.App`.

## Cómo se usa

1. **Cierra el juego en el emulador.** Los emuladores guardan el `.sav` al salir y te pueden pisar los cambios.
2. Pulsa **Abrir carpeta de partidas…** y elige la carpeta donde están tus partidas: `.sav` (mGBA, Gambatte…), `.dsv` (DeSmuME/melonDS) o los archivos `main` de Citra (3DS). La app busca partidas de Gen 1 a 7 hasta 12 niveles de subcarpetas y recuerda la carpeta para la próxima vez.
3. Elige cuántas partidas ver a la vez (**Partidas visibles: 1–4**) y, en cada panel, qué partida mostrar con su lista desplegable. También puedes abrir un archivo suelto con **Archivo…**.
4. Selecciona uno o varios Pokémon (Ctrl/Shift + clic) y pulsa **→ Banco**.
5. En otra partida, selecciona Pokémon en el banco y pulsa **← Traer**. Si tienes seleccionado un espacio vacío, empiezan a ocupar desde ahí; si no, desde el inicio de la caja actual.

Cada movimiento se guarda en el archivo al instante.

### Editor (doble clic sobre un Pokémon, o botón **Editar…**)

- Especie, mote, nivel, naturaleza, objeto y shiny
- Los 4 movimientos (o **Movimientos sugeridos por nivel**)
- IVs y EVs con los stats reales calculados al momento
- **Evolucionar** a cualquiera de sus evoluciones directas que existan en ese juego
- Indicador de legalidad con el informe completo de PKHeX

En Gen 3 y 4 la naturaleza, el shiny, el género y la habilidad salen del mismo número (PID), así que cambiar uno puede cambiar los otros.

### Crear Pokémon

**Crear Pokémon…** pone uno nuevo (nivel 5, con tus datos de entrenador) en el espacio vacío seleccionado o en el primero libre, y abre el editor. Como no tiene un lugar de captura real, PKHeX puede marcarlo como no legal.

Para editar un Pokémon que está en el banco hay que tener abierta una partida de la misma generación (el editor la usa para saber qué especies, movimientos y objetos existen).

## Dónde quedan los datos

- Banco: `%AppData%\PokeBank\Bank\` (un archivo `.pk3`/`.pk4` por Pokémon; también se abren con PKHeX)
- Copias de seguridad automáticas de cada partida abierta: `%AppData%\PokeBank\Backups\`
- Sprites descargados (se bajan de PokeAPI la primera vez): `%AppData%\PokeBank\Sprites\`
- Registro de errores: `%AppData%\PokeBank\logs\errores.log`
- Preferencias (carpeta y número de paneles): `%AppData%\PokeBank\settings.json`
- Fondos propios por juego: `%AppData%\PokeBank\Fondos\`

## Reglas de transferencia

Rutas oficiales (como en los juegos):

- Gen 1 → Gen 2 (Máquina del Tiempo)
- Gen 1/2 → Gen 7 (Consola Virtual + Poké Transporter)
- Gen 3 → 4 → 5 → 6 → 7 (Parque Compi, Pokétransfer, Poké Transporter/Bank)
- Mismo formato (Esmeralda ↔ Rojo Fuego, Platino ↔ HGSS, X ↔ Rubí Omega, Sol ↔ Ultraluna…): directo

Cualquier otra combinación (bajar de generación, Gen 2 → Gen 3, Gen 7 → Gen 1, etc.) no tiene ruta oficial, así que la app ofrece **recrearlo**. Crea el Pokémon en el juego destino con la misma especie, entrenador, mote, nivel, IVs y EVs (re-escalados en Gen 1-2, que usan DVs 0-15 y Stat Exp), naturaleza, género, habilidad y shiny cuando el juego los tiene, y los movimientos, objeto y Ball que existan ahí. Antes muestra qué se pierde y pide confirmación. PKHeX marcará la copia como no legal.

Los huevos no se transfieren (en las listas se muestran con un ícono de huevo).

## Fondos

Cada panel se pinta con los colores del juego de su partida y su nombre de marca de agua. Con **Fondo…** puedes elegir tu propia imagen para ese juego; se guarda en `%AppData%\PokeBank\Fondos\` con el código del juego (p. ej. `E.png`, `HG.jpg`, `SN.png`) y se usa en todas las partidas de ese juego. Si abres **Fondo…** y cancelas, te ofrece quitar la imagen actual.

## Estructura

```
src/
  PokeBank.Core/   lógica: SaveSession (partida), SaveScanner (buscar partidas), BankStore (banco),
                   Transfer (conversión y recreación), PokemonEdits (editor, evolución, creación, legalidad),
                   AppSettings (preferencias), GameNames (nombres y colores de cada juego), Names (textos)
  PokeBank.App/    interfaz Avalonia: MainWindow, SavePanel, EditorWindow, ConfirmDialog, GameTheme, SpriteCache, CrashLog
```

## Pendiente / ideas

- Sprites de formas alternativas (Unown, Deoxys, Castform...)
- Elegir habilidad, bola, lugar de captura y formas desde el editor
- Equipo (party) además de las cajas
- Gen 8+ (Espada/Escudo en adelante) y Let's Go

## Licencia

PokeBank se distribuye bajo la licencia **GPL-3.0** (ver [LICENSE](LICENSE)), requerida por PKHeX.Core.

## Aviso

Proyecto de fans sin fines de lucro, sin relación con Nintendo, Game Freak, Creatures Inc. ni The Pokémon Company. Pokémon y sus nombres son marcas registradas de sus dueños. Este repositorio no incluye ROMs ni partidas guardadas. Úsalo con copias de juegos que tengas legalmente.
