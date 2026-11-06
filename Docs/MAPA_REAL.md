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
   - **Senderos Turi Bike Park** (activado): talla y pinta las 4 rutas (ver abajo). Solo calzan con este heightmap.
   - **Jugable (menú Carrera)** (activado): pone la bici, el cronómetro, checkpoints y la meta, y agrega la
     escena al juego (Carrera → Turi Bike Park). Desactívalo si solo quieres ver el mapa desde arriba.
   - **Recorte**: si solo quieres una parte de la imagen, mueve los dos extremos de cada barra.
     Debajo dice cuánto medirá el mapa.
   - **Resolución**: **2049** (recomendado con senderos: cada punto del terreno mide 0,86 m y el sendero sale nítido).
   - **Suavizado**: con 8 bits deja **5–8 m** (si no, el terreno queda en escalones). Con 16 bits basta 1–2 m.
   - **Ambiente**: *Bosque Nublado* (pinos, pasto verde) o *Valle Seco* (árboles bajos, pasto seco).
   - **Árboles por hectárea**: 20 es un bosque ralo; 60 o más ya es denso (y pesa más).
3. Clic en **Crear mapa**. Se abre la escena con el terreno, el cielo y la luz.

> Volver a darle **Crear mapa** rehace todo y **borra lo que pintaste**. Para cambiar solo los árboles usa
> el botón *Repartir árboles otra vez*.

Para mirar en 3D: en la vista **Scene**, mantén **clic derecho** y muévete con **W A S D** (Shift = más rápido;
la rueda del mouse acerca y aleja). **F** con el terreno seleccionado lo encuadra completo.

## 2. Senderos del Turi Bike Park

Al crear el mapa con **Senderos Turi Bike Park** activado, las rutas quedan talladas en el terreno, pintadas
de tierra (con roca asomando en lo más empinado) y sin árboles encima; a los lados el bosque es más cerrado,
y la salida y la meta quedan despejadas. Se trazaron con tus capturas del mapa de calor y los senderos de
OpenStreetMap; no es una copia exacta. Mapa de las rutas: [`Docs/TuriBikePark_Rutas.png`](TuriBikePark_Rutas.png).

**Todas salen del mismo lugar y llegan al mismo lugar:**
- **Salida (S):** arriba, junto al cruce de la Vía a Ictocruz (celda D7).
- **Meta (M):** abajo, cerca de la Circunvalación Sur (celda E2).

| Ruta | Color | Por dónde va | Largo |
|---|---|---|---|
| **El Bosque** | verde | Sale por el oeste, rodea la hondonada y baja recta por el bosque del lado de San Isidro | ~1,4 km |
| **El Zigzag** | amarillo | Por la loma de la vía; en el bosque hace un zigzag de ida y vuelta | ~1,6 km |
| **La Cresta** | rosado | Por la loma; baja por el lomo del medio | ~1,6 km |
| **La Rápida** | rojo | Por la loma; la línea del este, la más directa abajo | ~1,65 km |

Con la exageración (×1,6 y acento 0,35) cada ruta baja unos **400 m**, con pendiente media de **24–29 %**
y tramos de **45–54°** (verdaderos drops). No se aplanó la bajada: el sendero sigue la pendiente del cerro
y solo se nivela de lado a lado. Lo único que se ajusta son las subidas: las salidas no suben más de 3 %
(arranque de sprint) y las rutas no más de 8 % (se suben pedaleando); para eso el sendero rebaja algunas
lomas y rellena algunas hondonadas (hasta ~15 m en la loma de la vía).

**En la escena** queda el objeto **Senderos** con:
- **Ruta El Bosque**, **Ruta El Zigzag**, **Ruta La Cresta**, **Ruta La Rápida**: cada una dibuja su línea de color
  en la vista **Scene** (componente `TrailPath`). Sirven para ubicar rampas y puentes, y más adelante para
  checkpoints, reaparecer y rivales.
- **Salida** y **Meta**: marcas con ícono, mirando hacia donde va la ruta.

Las rampas, puentes de madera y demás obstáculos los pones tú encima del sendero.

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
- Hay **checkpoints** cada ~250 m sobre cada ruta (R / Y para reaparecer en el último) y la **Meta** detiene el tiempo.
- Si caes fuera del mapa, reapareces en el último checkpoint.

Para jugar:
- **Desde el menú:** abre `Menu.unity` → Play → Jugar → Carrera → **Turi Bike Park**.
  (La tarjeta aparece después de reconstruir el menú: **DownhillRace → Crear escena del menú**.)
- **Directo:** abre `MapaReal.unity` y presiona Play (útil para probar rampas mientras las pones).

Las rampas y puentes que pongas en esta escena se juegan tal cual. Ojo: **Crear mapa** rehace la escena
completa y borra lo que hayas agregado, así que una vez que empieces a poner rampas no lo vuelvas a usar.

Falta: fuera del sendero no frena el pasto (solo en las pistas generadas), no hay arco de salida ni de meta,
y el modo **Avalancha** (salida en grupo) viene después.

## La Paz — Yunguilla

Mapa real de Yunguilla (parroquia La Paz, Santa Isabel), desde `Assets/Mapas/LaPaz_Alturas.png`:
heightmap de **16 bits** (sale liso, sin escalones), recuadro de **4,12 km** por lado **girado 8,8°**
respecto al norte, **1 265 m** de desnivel. Se ubicó con tus coordenadas de salida y meta y elevaciones
satelitales (coincidencia 0,9999).

- **Salida:** -3.34744, -79.26852 (2 196 m), abajo a la derecha del heightmap.
- **Meta:** -3.31872, -79.29635 (1 004 m), arriba a la izquierda.

**Primer trazado** (automático, la bajada más natural del cerro; se afinará con la ruta real por partes):
**4,7 km**, baja **1 193 m**, pendiente media **25 %**, ~1,1 km de tramos de más de 40 %, casi sin subidas.
Baja por la loma larga hacia el valle del noroeste. Es una pista de **Avalancha**: **14 m** de ancho casi
todo el recorrido (3 o 4 corredores juntos) y dos **cuellos de botella de 8 m** (*Cuello de la quebrada*
y *Cuello del pueblo*). Mapa: [`Docs/LaPaz_Ruta.png`](LaPaz_Ruta.png).

Valores recomendados: sin exagerar el desnivel (×1, ya baja casi 1 200 m), quebradas acentuadas 0,3,
suavizado 4 m, valle seco y 6 árboles por hectárea (el mapa es grande). Cuando la ruta esté lista se
recorta lo que sobra, como en Turi, para ganar detalle.

Mientras no exista el modo Avalancha (salida en grupo con rivales), se juega en **Carrera → La Paz**
como contrarreloj. La versión generada anterior (`Pista02`) ya no sale en el menú.

El trazado está en `Assets/Scripts/Editor/LaPazTrails.cs` (píxeles del heightmap, 2048 × 2048).

## Archivos

| Archivo | Qué es |
|---|---|
| `Assets/Mapas/MapaReal_Alturas.png` | El heightmap original |
| `Docs/MapaReal_Guia.png` | Mapa con relieve y cuadrícula para marcar senderos |
| `Docs/TuriBikePark_Rutas.png` | Las 4 rutas del Turi Bike Park sobre la guía |
| `Assets/Scripts/Editor/TuriBikeParkTrails.cs` | Trazado de las rutas de Turi (puntos sobre el mapa) |
| `Assets/Scripts/Editor/LaPazTrails.cs` | Trazado de La Paz (píxeles del heightmap) |
| `Assets/Scripts/Editor/RealMapCatalog.cs` | Los mapas reales y sus valores recomendados |
| `Assets/Scripts/Editor/RealMapTrails.cs` | Tramos y rutas de un mapa (datos comunes) |
| `Assets/Mapas/LaPaz_Alturas.png` | Heightmap de La Paz (16 bits) |
| `Docs/LaPaz_Ruta.png` | Mapa de la ruta de La Paz |
| `Assets/Scripts/Editor/TrailTerrainCarver.cs` | Talla los senderos en el terreno sin aplanar la bajada |
| `Assets/Scripts/Track/TrailPath.cs` | Componente de cada ruta en la escena (línea central) |
| `Assets/Mapas/MapaReal_Terreno.asset` | El terreno con lo que pintes (alturas, texturas, árboles) |
| `Assets/Scenes/MapaReal.unity` | La escena del mapa (no está en la lista de escenas del juego) |
| `Assets/Prefabs/Vegetacion/` | Árboles y arbustos que usa el terreno |
| `Assets/Scripts/Editor/HeightmapMapWindow.cs` | La ventana |
| `Assets/Scripts/Editor/HeightmapMapBuilder.cs` | Crea el terreno, las texturas y los árboles |
| `Assets/Scripts/Editor/PngHeightmapReader.cs` | Lee el PNG de 8 o 16 bits sin perder precisión |
