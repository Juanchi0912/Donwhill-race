# Mapa real desde un heightmap

Hay dos mapas reales: el **Turi Bike Park** (Cuenca, escena `MapaReal`) y **La Paz — Yunguilla**
(Santa Isabel, escena `LaPaz`, ver [abajo](#la-paz--yunguilla)). En la ventana se elige con **Mapa**;
cada uno guarda sus propios valores y su propia escena, así crear uno no borra el otro.

Convierte un **heightmap** (imagen en gris exportada de una página de mapas: claro = alto, oscuro = bajo)
en un terreno de Unity **a escala real**. El mapa queda en una **escena aparte** (`Assets/Scenes/MapaReal.unity`):
no se agrega al juego ni al menú. Sirve para diseñar el lugar (senderos, árboles) antes de convertirlo en pista.

El heightmap actual es `Assets/Mapas/MapaReal_Alturas.png` (1024 × 1024, 8 bits).

## Dónde es

Ladera sur de **Cuenca (Azuay)**: de la **Circunvalación Sur** (arriba, lo más bajo, ~2 530 m) subiendo hacia
**Turi** y el **Parque Ictocruz** (abajo, lo más alto, ~2 830 m). Pasa por San Isidro, Asunción, la Vía a Ictocruz,
Chicapamba y San Antonio de Gapal.

- **Tamaño:** cuadrado de **1,77 km** por lado (medido con las coordenadas de esos lugares en OpenStreetMap).
- **Desnivel real:** unos 300–330 m.
- **Orientación:** el norte queda arriba de la imagen. En Unity el norte es **+Z** (hacia el fondo) y el este es **+X**.
- Se comprobó con elevaciones satelitales (SRTM 30 m): coinciden con el heightmap (correlación 0,93).

**Guía para marcar senderos:** [`Docs/MapaReal_Guia.png`](MapaReal_Guia.png) — el mapa de calles con el relieve
y una cuadrícula **A–H** (oeste → este) y **1–8** (norte → sur). Cada celda mide unos **221 m**.
Se puede dibujar encima (Paint, celular…) o nombrar celdas: «sale en E2, baja por F4, llega a D8».

## 1. Crear el mapa

1. En Unity: menú **DownhillRace → Crear mapa desde heightmap**.
2. Arriba, en **Mapa**, elige cuál crear y haz clic en **Usar valores recomendados** si cambiaste algo antes.
3. En la ventana:
   - **Heightmap (PNG)**: `MapaReal_Alturas.png` (ya viene puesto).
   - **Ancho / Largo (m)**: lo que mide en la realidad el área que exportaste. Para este mapa: **1770 m**
     (si la ventana muestra otro número de una prueba anterior, cámbialo a 1770).
   - **Desnivel total (m)**: del punto más bajo al más alto (si el archivo se llama `…_331m.png` se llena solo).
   - **Exagerar desnivel ×** (1,6): multiplica la altura. Tal cual es la realidad, la bajada se siente suave.
   - **Acentuar quebradas** (0,35): hace más hondas las quebradas y más marcadas las lomas.
   - **Senderos Turi Bike Park** (activado): talla las 4 rutas con sus saltos y arma toda la pista (ver abajo).
     Solo calzan con este heightmap.
   - **Jugable (menú Carrera)** (activado): pone la bici, el cronómetro, checkpoints y la meta, y agrega la
     escena al juego (Carrera → Turi Bike Park). Desactívalo si solo quieres ver el mapa desde arriba.
   - **Recorte**: si solo quieres una parte de la imagen, mueve los dos extremos de cada barra.
     Debajo dice cuánto medirá el mapa.
   - **Resolución**: **2049** (cada punto del terreno mide 0,86 m). Turi usa **4097** (43 cm) para que los saltos
     y drops queden nítidos; tarda un par de minutos en crearse.
   - **Suavizado**: con 8 bits deja **5–8 m** (si no, el terreno queda en escalones). Con 16 bits basta 1–2 m.
   - **Ambiente**: *Bosque Nublado* (pinos, pasto verde) o *Valle Seco* (árboles bajos, pasto seco).
   - **Árboles por hectárea**: 20 es un bosque ralo; 60 o más ya es denso (y pesa más).
3. Clic en **Crear mapa**. Se abre la escena con el terreno, el cielo y la luz.

> Volver a darle **Crear mapa** rehace todo y **borra lo que pintaste**. Para cambiar solo los árboles usa
> el botón *Repartir árboles otra vez*.

Para mirar en 3D: en la vista **Scene**, mantén **clic derecho** y muévete con **W A S D** (Shift = más rápido;
la rueda del mouse acerca y aleja). **F** con el terreno seleccionado lo encuadra completo.

## 2. Turi Bike Park: la pista de carrera terminada

Mapa de las rutas: [`Docs/TuriBikePark_Rutas.png`](TuriBikePark_Rutas.png).

**Las dos partes reales no se tocan:** la salida en lo alto del cerro y la llegada (los círculos grises de tu
dibujo) quedan como estaban y no se les agrega nada (ni saltos, ni cinta, ni lomas): esas las editas tú.
Todo lo demás es inventado para que sea divertido, al estilo de los juegos de downhill.

| Ruta | Color | Largo | Qué tiene |
|---|---|---|---|
| **El Bosque** | verde | 2,04 km | Por el oeste. Bosque cerrado (árboles pegados al sendero), raíces, pedregal, 3 drops de roca y el **atajo con gap sobre el camino de tierra** |
| **El Zigzag** | amarillo | 2,21 km | Curvas cerradas de **arcilla** con peralte y el **salto sobre la cerca de piedra** |
| **La Cresta** | rosado | 2,21 km | Abierta, por el filo del este; **dobles encadenados**; pasa **por arriba del puente** |
| **La Rápida** | rojo | 1,86 km | La más directa: pedregal, drops grandes; pasa **por debajo del puente** |

Las cuatro bajan **518 m** y **no suben nunca** (como mínimo bajan 4 %; las únicas "subidas" son las rampas).
Para lograrlo el mapa se inclina 180 m hacia el norte, se suavizó la ladera del zigzag y se abrió un paso en
la loma al norte de la salida este.

**Saltos y obstáculos (60 en total):**
- **Miradores con drop** de madera justo al salir de la parte real (uno en cada salida).
- **Salto enorme** en la **recta final** (compartida por El Zigzag, La Cresta y La Rápida), con público y vallas.
- En las rectas largas se ponen solos **mesas, rampas de madera, drops, dobles y lomas** (no hay rectas aburridas).
- **Curvas de arcilla** (rojizas): peralte alto y la llanta agarra menos.
- **Pedregales y raíces**: el piso tiene serrucho, la bici rebota.

**Para que se sienta pista de carrera:**
- Sendero con **orilla marcada** (bordo y franja de tierra oscura) y **cinta DOWNHILL RACE** a los dos lados.
- **Pancartas** de patrocinadores (inventados) antes de cada salto, por fuera de las curvas y en la recta final.
- **Lomas empinadas entre las rutas** (las líneas grises de tu dibujo): no se ve de una ruta a la otra.
  Se corren solas al medio y bajan en pared antes de llegar a cualquier sendero.
- **Caídas:** chocar fuerte contra un árbol, una roca, la cerca o la baranda del puente es caída
  («¡CAÍDA!» y reapareces en el último checkpoint). Hay **checkpoints cada 150 m** con un arco.
- Una **casa de campo** al final del camino de tierra y **Cuenca allá abajo** al norte (niebla lejana).

**En la escena** queda:
- **Senderos**: una línea de color por ruta (componente `TrailPath`) y las marcas de Salida y Meta.
- **Pista terminada**: Cinta, Pancartas, Saltos (rampas de madera, miradores, rocas, raíces, cerca),
  Puente, Arcilla (zonas resbalosas), Publico, Arcos, Casa de campo y Ciudad. Todo se puede mover o borrar.

**Para cambiar algo** (en `Assets/Scripts/Editor/TuriBikeParkTrails.cs`):
- Cada tramo tiene sus saltos con `.With(Feature.Drop, P(x, y))`; los tipos son `Mirador`, `Drop`, `Kicker`,
  `Tabletop`, `Doubles`, `Rollers`, `RoadGap`, `FenceGap`, `RockDrop`, `BigJump`, `RockGarden` y `Roots`.
- `Auto(...)` llena las rectas de saltos solo; `Forest`, `Clay` y `Crowd` encienden el bosque cerrado,
  la arcilla y el público. Las lomas son `set.Ridges` (su altura en metros).

## 3. Pintar senderos nuevos o retocar

1. Selecciona el objeto **Mapa** en la jerarquía.
2. En el Inspector, componente **Terrain** → botón del pincel (**Paint Terrain**) → **Paint Texture**.
3. En **Layers** elige **MapaReal_CapaTierra**.
4. Pincel: **Brush Size** pequeño (más o menos el ancho del sendero, 2–4 m) y **Opacity** 100.
5. Pinta arrastrando sobre el terreno. Para corregir, pinta encima con **MapaReal_CapaPasto**.
6. Cuando termines, en la ventana del mapa clic en **Quitar árboles de los senderos**
   (el **Margen** es cuánto espacio libre de árboles queda a cada lado).

Consejos:
- Si el sendero cruza una ladera muy inclinada, alísalo con **Paint Terrain → Smooth Height**
  (pincel chico, pasando a lo largo del sendero) o con **Set Height** para hacer una pequeña terraza.
- **Ctrl+Z** deshace cualquier pincelada.

## 4. Árboles a mano

**Paint Terrain → Paint Trees** (o el ícono del árbol en el componente Terrain):
- Elige un árbol de la lista y pinta. **Tree Density** controla cuántos pone por pincelada.
- **Shift + clic** borra árboles; **Ctrl + Shift + clic** borra solo el tipo elegido.
- Los árboles tienen colisión en el tronco (la bici choca); los arbustos no.

## 5. Jugarlo

Con **Jugable** activado, la escena `MapaReal` es también una pista de **contrarreloj**:
- La bici arranca en la **Salida**, con la misma cuenta regresiva, cronómetro, HUD y controles de las otras pistas.
- Hay **checkpoints** cada 150 m sobre cada ruta (R / Y para reaparecer en el último) y la **Meta** detiene el tiempo.
- Si caes fuera del mapa o chocas fuerte (caída), reapareces en el último checkpoint.

Para jugar:
- **Desde el menú:** abre `Menu.unity` → Play → Jugar → Carrera → **Turi Bike Park**.
  (La tarjeta aparece después de reconstruir el menú: **DownhillRace → Crear escena del menú**.)
- **Directo:** abre `MapaReal.unity` y presiona Play (útil para probar rampas mientras las pones).

Lo que agregues en esta escena (por ejemplo en la salida y la llegada) se juega tal cual. Ojo: **Crear mapa**
rehace la escena completa y borra lo que hayas agregado; si ya editaste la salida o la llegada, no lo vuelvas a usar
(o guarda tus objetos como prefab antes).

Falta: el modo **Avalancha** (salida en grupo con rivales) viene después.

## La Paz — Yunguilla

Mapa real de Yunguilla (parroquia La Paz, Santa Isabel), desde `Assets/Mapas/LaPaz_Alturas.png`:
heightmap de **16 bits** (sale liso, sin escalones), recuadro de **4,12 km** por lado **girado 8,8°**
respecto al norte, **1 265 m** de desnivel. Se ubicó con tus coordenadas de salida y meta y elevaciones
satelitales (coincidencia 0,9999).

- **Salida:** -3.34744, -79.26852 (2 196 m), abajo a la derecha del heightmap.
- **Meta:** -3.31872, -79.29635 (1 004 m), arriba a la izquierda.

**La ruta** es la que marcaste en **Google Earth** (guardada en
[`Docs/LaPaz_Ruta_GoogleEarth.kml`](LaPaz_Ruta_GoogleEarth.kml), se abre en Google Earth). Completa dura
unos **7 minutos**, así que se recortó a **~4 minutos**: va desde la salida hasta 1,3 km después de empezar
la bajada larga. El resto (bajada larga completa, zigzags de abajo y llegada al río) queda en el KML.
Mapa: [`Docs/LaPaz_Ruta.png`](LaPaz_Ruta.png).

| Parte | Metros | Ancho | Ambientación (de los videos) |
|---|---|---|---|
| Salida en el pueblo | 0 – 557 | **16 m** (arranca el grupo) | Casas, galpones y postes; luego arboleda seca |
| Zigzags | 557 – 1 510 | **5 m, de uno solo** | Matorral cerrado y después pedregal (rocas en los bordes, piedras sueltas, agaves) |
| Cresta rápida | 1 510 – 2 837 | **14 m** | Arboleda seca a los lados, un tramo con piedras, zona de costa y el río rocoso antes de la meta |

Las **3 líneas alternativas** que marcaste en los zigzags también se tallan (de uno solo, 4 m).

**Lo que dibujaste sobre el mapa:**
- **Borde gris → recorte y límite.** El terreno se recorta al rectángulo del borde (unos 1,65 × 1,13 km, con
  50 m de margen): con la misma resolución sale con más del doble de detalle. El borde además es el
  **límite de la pista**: si sales, aparece **«¡FUERA DE PISTA!»** con una cuenta de 3 s y reapareces en el
  último checkpoint (objeto `LimiteDePista`, componente `TrackBounds`). Fuera del borde quedan pocos árboles.
- **Línea azul → río rocoso** de 16 m de ancho que cruza el sendero justo antes de la meta: cauce hundido,
  agua que corre (con salpicaduras), rocas grandes con colisión en el cauce y piedras chicas donde pasa el
  sendero. El agua **frena** la bici.
- **Círculo verde → zona de costa**: pasto verde, caña de azúcar, árboles chicos de cacao y arbustos verdes.
  El resto del mapa es valle seco (árboles bajos y arbustos secos, agaves, pasto seco).

**Suelo del sendero:** color liso **#D2B48C** (tierra clara) con un poco de grano, solo por donde pasa la bici.
Para que se distinga, el pasto seco de alrededor va **más oscuro y oliva** (**Tono del pasto** #A8B47A, que
multiplica el color). Se cambian en la ventana con **Color del sendero** y **Tono del pasto** (vacío = como es).

**Cinta de carrera «DOWNHILL RACE»** (fondo blanco, letras negras y una línea naranja arriba y abajo) con postes
cada 4 m, donde uno se puede salir o perder: a los dos lados en la salida, los zigzags y la llegada, y en la
cresta ancha por fuera de las curvas cerradas. No choca (solo marca la pista) y se corta donde salen las
líneas alternativas y en el río. Las zonas de cinta están en `LaPazTrails.cs` (estilo `Cinta`).

**Día caluroso:** sol alto y fuerte, cielo cálido y calima lejana (se ve a más de 1 km) en vez de niebla.
Se activa con **Día caluroso** en la ventana.

**Más árboles en las partes anchas** (salida y cresta): árboles secos a los dos lados, con tronco que choca.

Con **desnivel ×1,5** y quebradas acentuadas 0,35: **2,7 km**, baja **741 m** en el juego (476 m reales),
pendiente media **27 %**, ~**3:55** de bajada según una simulación con la física de la bici.
Las rocas del borde tienen colisión (sirven de rampa natural o hay que esquivarlas); las piedras sueltas
dentro del sendero no.

Valores recomendados: desnivel ×1,5, quebradas 0,35, suavizado 4 m, valle seco, 12 árboles por hectárea,
recorte al borde, sendero #D2B48C, pasto #A8B47A y día caluroso. Si la ventana muestra valores viejos, clic en
**Usar valores recomendados (La Paz — Yunguilla)**.

Mientras no exista el modo Avalancha (salida en grupo con rivales), se juega en **Carrera → La Paz**
como contrarreloj.

El trazado y las zonas de ambientación están en `Assets/Scripts/Editor/LaPazTrails.cs`; la ambientación
se arma en `Assets/Scripts/Editor/RealMapDecor.cs`.

## Archivos

| Archivo | Qué es |
|---|---|
| `Assets/Mapas/MapaReal_Alturas.png` | El heightmap original |
| `Docs/MapaReal_Guia.png` | Mapa con relieve y cuadrícula para marcar senderos |
| `Docs/TuriBikePark_Rutas.png` | Mapa de la pista de carrera de Turi (rutas, saltos, puente) |
| `Assets/Scripts/Editor/TuriBikeParkTrails.cs` | Trazado de Turi: rutas, saltos, puente, camino, lomas |
| `Assets/Scripts/Editor/TrailFeatureShaper.cs` | Forma de los saltos, drops, dobles, peraltes y arcilla |
| `Assets/Scripts/Editor/TerrainSculptor.cs` | Retoques del relieve e inclinación hacia el norte |
| `Assets/Scripts/Editor/MountainRidges.cs` | Lomas empinadas entre las rutas |
| `Assets/Scripts/Editor/TrackDressing.cs` | Pista terminada: cinta, pancartas, madera, puente, público, ciudad |
| `Assets/Scripts/Track/GripZone.cs` | Zona resbalosa (curvas de arcilla) |
| `Assets/Scripts/UI/CrashNotice.cs` | Muestra «¡CAÍDA!» |
| `Assets/Scripts/Editor/LaPazTrails.cs` | Trazado de La Paz (píxeles del heightmap) |
| `Assets/Scripts/Editor/RealMapCatalog.cs` | Los mapas reales y sus valores recomendados |
| `Assets/Scripts/Editor/RealMapTrails.cs` | Tramos y rutas de un mapa (datos comunes) |
| `Assets/Mapas/LaPaz_Alturas.png` | Heightmap de La Paz (16 bits) |
| `Docs/LaPaz_Ruta.png` | Mapa de la ruta de La Paz |
| `Docs/LaPaz_Ruta_GoogleEarth.kml` | Tu ruta de Google Earth completa (con lo que quedó fuera del recorte) |
| `Assets/Scripts/Editor/RealMapDecor.cs` | Ambientación: pueblo, matorral, pedregal, arboleda, zona de costa y río rocoso |
| `Assets/Scripts/Track/TrackBounds.cs` | Límite de pista: «¡FUERA DE PISTA!» y reaparecer |
| `Assets/Scripts/Editor/TrailTerrainCarver.cs` | Talla los senderos en el terreno sin aplanar la bajada |
| `Assets/Scripts/Track/TrailPath.cs` | Componente de cada ruta en la escena (línea central) |
| `Assets/Mapas/MapaReal_Terreno.asset` | El terreno con lo que pintes (alturas, texturas, árboles) |
| `Assets/Scenes/MapaReal.unity` | La escena del mapa (no está en la lista de escenas del juego) |
| `Assets/Prefabs/Vegetacion/` | Árboles y arbustos que usa el terreno |
| `Assets/Scripts/Editor/HeightmapMapWindow.cs` | La ventana |
| `Assets/Scripts/Editor/HeightmapMapBuilder.cs` | Crea el terreno, las texturas y los árboles |
| `Assets/Scripts/Editor/PngHeightmapReader.cs` | Lee el PNG de 8 o 16 bits sin perder precisión |
