# Mapa real desde un heightmap

Convierte un **heightmap** (imagen en gris exportada de una página de mapas: claro = alto, oscuro = bajo)
en un terreno de Unity **a escala real**. El mapa queda en una **escena aparte** (`Assets/Scenes/MapaReal.unity`):
no se agrega al juego ni al menú. Sirve para diseñar el lugar (senderos, árboles) antes de convertirlo en pista.

El heightmap actual es `Assets/Mapas/MapaReal_Alturas.png` (1024 × 1024, 8 bits).

## 1. Crear el mapa

1. En Unity: menú **DownhillRace → Crear mapa desde heightmap**.
2. En la ventana:
   - **Heightmap (PNG)**: `MapaReal_Alturas.png` (ya viene puesto).
   - **Ancho / Largo (m)**: lo que mide en la realidad el área que exportaste. La página de mapas lo muestra
     al exportar. Si no lo sabes, deja 1500 m y ajústalo después hasta que las pendientes se vean naturales.
   - **Desnivel total (m)**: del punto más bajo al más alto (si el archivo se llama `…_331m.png` se llena solo).
   - **Recorte**: si solo quieres una parte de la imagen, mueve los dos extremos de cada barra.
     Debajo dice cuánto medirá el mapa.
   - **Suavizado**: con 8 bits deja **5–8 m** (si no, el terreno queda en escalones). Con 16 bits basta 1–2 m.
   - **Ambiente**: *Bosque Nublado* (pinos, pasto verde) o *Valle Seco* (árboles bajos, pasto seco).
   - **Árboles por hectárea**: 20 es un bosque ralo; 60 o más ya es denso (y pesa más).
3. Clic en **Crear mapa**. Se abre la escena con el terreno, el cielo y la luz.

> Volver a darle **Crear mapa** rehace todo y **borra lo que pintaste**. Para cambiar solo los árboles usa
> el botón *Repartir árboles otra vez*.

Para mirar en 3D: en la vista **Scene**, mantén **clic derecho** y muévete con **W A S D** (Shift = más rápido;
la rueda del mouse acerca y aleja). **F** con el terreno seleccionado lo encuadra completo.

## 2. Pintar los senderos

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

## 3. Árboles a mano

**Paint Terrain → Paint Trees** (o el ícono del árbol en el componente Terrain):
- Elige un árbol de la lista y pinta. **Tree Density** controla cuántos pone por pincelada.
- **Shift + clic** borra árboles; **Ctrl + Shift + clic** borra solo el tipo elegido.
- Los árboles tienen colisión en el tronco (la bici choca); los arbustos no.

## 4. Lo que falta para jugarlo

Por ahora el mapa es solo para diseñar. Cuando los senderos estén listos, el siguiente paso es convertir
el sendero principal en pista de carrera (salida, checkpoints, meta) y agregarla al menú.

## Archivos

| Archivo | Qué es |
|---|---|
| `Assets/Mapas/MapaReal_Alturas.png` | El heightmap original |
| `Assets/Mapas/MapaReal_Terreno.asset` | El terreno con lo que pintes (alturas, texturas, árboles) |
| `Assets/Scenes/MapaReal.unity` | La escena del mapa (no está en la lista de escenas del juego) |
| `Assets/Prefabs/Vegetacion/` | Árboles y arbustos que usa el terreno |
| `Assets/Scripts/Editor/HeightmapMapWindow.cs` | La ventana |
| `Assets/Scripts/Editor/HeightmapMapBuilder.cs` | Crea el terreno, las texturas y los árboles |
| `Assets/Scripts/Editor/PngHeightmapReader.cs` | Lee el PNG de 8 o 16 bits sin perder precisión |
