# Pista 2: La Paz — Yunguilla (Azuay)

> **Reemplazada.** La Paz ahora es un **mapa real** (heightmap de Yunguilla, ruta de Google Earth, 2,8 km, ~4 min,
> pista ancha para Avalancha): ver [MAPA_REAL.md](MAPA_REAL.md#la-paz--yunguilla). El menú carga la escena
> `LaPaz`. Lo de abajo describe la versión generada anterior (escena `Pista02`), que sigue existiendo pero
> ya no aparece en el menú.

Basada en los videos de la carrera real: no es una copia exacta, sino una versión exagerada para el juego,
con sus partes más reconocibles. **≈ 1.27 km, 147 m de desnivel, ≈ 1:30 de bajada promedio**
(≈ 1:26 una bajada muy buena), según la simulación con la física del juego.

## Recorrido

| Parte | Tramos | Qué tiene |
|---|---|---|
| Salida | Salida en el pueblo, Carretera 1-3 | Pueblo con casas y postes, carretera de lastre ancha |
| Ladera abierta | Entrada al sendero, Ladera abierta 1-2, Curva con berma | Pasto seco con vista al valle, **salto de madera**, piedra-rampa, la curva polvorienta |
| Cresta | Cresta 1-3, Horquilla de tierra | Bermas de tierra, raíces, piedra-rampa, horquilla cerrada |
| Bajada empinada | Bajada empinada (18°) | Raíces y una raíz grande para saltar |
| Matorral | Curva al matorral, Matorral 1-7, Horquilla del matorral | Sendero angosto (3.6 m) dentro de arbustos secos, raíces |
| Pedregal | Pedregal 1-5, Horquilla del pedregal | **Piedras sueltas dentro del camino** y en los bordes, agaves, piedra-rampa |
| Cresta rápida | Cresta rápida 1-3, Curva al vacío | La parte más rápida, **salto grande de madera**, piedra-rampa |
| Caída y zigzag | Caída de la cresta (18°), Zigzag 1-3, Pequeña subida, Horquillas Z1-Z3 | Horquillas muy cerradas (radio ≈ 8 m) en ladera abierta |
| Pueblo | Llegada al pueblo, Calle del pueblo | Casas, galpones, postes y el **salto de concreto** en la calle |
| Final | Sendero verde 1-3, Carretera entre cañaverales 1-3, Bajada al río, Río, Salida del río, Meta | Vegetación tropical, caña de azúcar alta, cruce del río y meta |

Los saltos naturales (piedras al borde y raíces) son **opcionales**: se pueden esquivar.
Las rampas de madera y concreto están en el centro del camino, con una bajada después para aterrizar.

## Paredes de montaña

La pista corre **dentro de la montaña**: un terreno (`Montañas`) sube en paredes empinadas a ambos lados,
con roca en lo más parado, pasto en lo suave y matorral y árboles en las laderas. Ya no se ve vacío a los lados.
Si la pista pasa cerca de sí misma, entre los dos tramos queda una ladera continua.
Las montañas lejanas ahora se ubican **fuera de toda la pista** (antes una quedaba en medio y se podía atravesar).

En el diseño se ajusta en **Montañas a los lados**:
- **Mountain Walls**: activar o quitar las paredes.
- **Wall Height**: altura de las paredes (70 m por defecto).
- **Wall Steepness**: qué tan parado suben (0.8 por defecto; 1.5 ya es casi un cañón).

## El río

- **40 m de cruce** y un cauce de ≈ 100 m de ancho tallado en el terreno, con orillas que bajan al agua.
- Unas **50 piedras**: chicas dentro del camino (se pisan o se esquivan) y grandes por todo el cauce.
- El agua es transparente, con ondas que **corren**, y las ruedas sueltan **salpicaduras** en vez de polvo.
- **Frena fuerte** al entrar rápido. El frenado baja a paso lento, así que se cruza aunque no quede energía.
  Se ajusta en la bici: **Zone Full Speed** en el `BikeController`.

## Atajos ocultos

Tres senderos angostos (≈ 2 m) salen de la pista entre matorrales, así que **la entrada casi no se ve**.
Cortan una horquilla: son más empinados y tienen raíces, así que solo ahorran tiempo si se toman bien.

| Atajo | Dónde sale | Dónde vuelve | Cómo encontrarlo |
|---|---|---|---|
| Atajo de la cresta | Final de *Cresta 3* | Mitad de *Bajada empinada* | Antes de la horquilla de tierra, mira al lado de adentro de la curva |
| Atajo del matorral | *Matorral 6* | *Pedregal 2* | En el matorral cerrado, justo antes de la horquilla |
| Atajo del zigzag | Final de *Zigzag 2* | *Zigzag 3* | Saltándose la horquilla Z2 por la ladera |

Para editarlos, en el diseño abre **Atajos ocultos** y cambia:
- **From Section / To Section**: nombre exacto del tramo donde sale y donde vuelve (tiene que estar más abajo).
- **From Position / To Position**: dónde dentro del tramo (0 = inicio, 1 = final).
- **Half Width**: medio ancho del sendero. **Bend**: cuánto se curva. **Roots**: raíces en el sendero.

Si un nombre no existe o el atajo terminaría más arriba de donde sale, no se crea y aparece un aviso en la Consola.

## Construirla

- Menú de Unity: **DownhillRace → Crear escena: La Paz — Yunguilla** (crea `Assets/Scenes/Pista02.unity`).
- Para jugarla desde el menú, reconstruye también **DownhillRace → Crear escena del menú**:
  Jugar → Carrera → **La Paz — Yunguilla**.

## Editarla sin código

1. En Project abre `Assets/Settings/Pistas/LaPazYunguilla_Diseno.asset`.
2. En el Inspector aparece la lista de **Tramos**. Cada uno tiene:
   - **Length**: largo en metros.
   - **Turn**: grados que gira (+ derecha, − izquierda). Una horquilla cerrada es ±150 en 18 m.
   - **Slope**: grados de bajada (negativo = subida). 18 ya es muy empinado.
   - **Half Width**: medio ancho (1.8 = sendero angosto, 3.6 = carretera). 0 = el del diseño.
   - **Ground**: Tierra o Carretera (lastre, casi sin bermas).
   - **Buildings**: casas y postes a los lados.
   - **Vegetation**: Normal, Densa (matorral), Escasa (ladera abierta), Ninguna, Verde (tropical), Cañaveral.
   - **Rocks On Edges**, **Rock Garden** (piedras dentro del camino), **Roots** (raíces).
   - **Natural Jump** + **Jump Position**: piedra a la izquierda/derecha o raíz grande, y dónde (0 a 1 del tramo).
   - **Ramp** + **Ramp Position**: rampa de madera pequeña/grande o de concreto.
   - **Water**: cruce de río.
3. Presiona **Construir escena (Pista02)** arriba del Inspector.

Puedes agregar tramos con el **+** de la lista, moverlos arrastrando y borrarlos con el **−**.
Si algo queda mal, **DownhillRace → Restaurar diseño original de La Paz — Yunguilla** vuelve al diseño inicial.

Cuando el diseño original cambia (como ahora con el río y los atajos), al construir aparece un aviso:
**Actualizar** trae el diseño nuevo (pierdes tus cambios), **Mantener el mío** deja tu versión como está.

### Consejos

- Una curva necesita radio de al menos 7-8 m: **giro / largo ≤ 8 grados por metro** (150° en 18 m).
- Si la pista pasa muy cerca de sí misma, el pasto se corta entre los dos tramos (se ve un escalón):
  alarga las traversas o cambia el lado de una horquilla.
- Pon las rampas en tramos que bajan y con otro tramo que baje después (para aterrizar).
- Para una pista nueva: clic derecho en Project → **Create → DownhillRace → Diseño de pista**,
  ponle otro **Scene Name** (por ejemplo `Pista03`) y construye.
