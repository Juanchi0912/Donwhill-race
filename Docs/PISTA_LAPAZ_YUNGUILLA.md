# Pista 2: La Paz — Yunguilla (Azuay)

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
| Final | Sendero verde 1-3, Carretera entre cañaverales 1-3, Bajada al río, Río, Salida del río, Meta | Vegetación tropical, caña de azúcar alta, cruce del río (frena) y meta |

Los saltos naturales (piedras al borde y raíces) son **opcionales**: se pueden esquivar.
Las rampas de madera y concreto están en el centro del camino, con una bajada después para aterrizar.

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

### Consejos

- Una curva necesita radio de al menos 7-8 m: **giro / largo ≤ 8 grados por metro** (150° en 18 m).
- Si la pista pasa muy cerca de sí misma, el pasto se corta entre los dos tramos (se ve un escalón):
  alarga las traversas o cambia el lado de una horquilla.
- Pon las rampas en tramos que bajan y con otro tramo que baje después (para aterrizar).
- Para una pista nueva: clic derecho en Project → **Create → DownhillRace → Diseño de pista**,
  ponle otro **Scene Name** (por ejemplo `Pista03`) y construye.
