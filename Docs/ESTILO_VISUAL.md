# Estilo visual: bosque nublado y bici GY RAVEN

Adaptación de las imágenes de referencia (bosque con niebla, barro, bici GY RAVEN azul
y corredor con jersey naranja) a la pista del prototipo, pensada para **PC de gama media**.

Todo se genera por código al usar **DownhillRace → Crear escena del prototipo**:
no hay archivos externos, ni paquetes de la Asset Store, ni modelos importados.

## Qué se genera

| Parte | Cómo se hace | Dónde queda |
|---|---|---|
| Texturas (tierra con barro, bermas, pasto con hojas, roca, corteza, agujas, madera, cielo) | Ruido procedural "tileable" en `ProceduralTextures.cs` | `Assets/Art/Textures/Prototipo/` |
| Mapas normales | Calculados a partir de la altura de cada textura | mismo lugar (`...Normal.png`) |
| Bici GY RAVEN | Mallas por código en `ProceduralModels.cs` (cuadro DH, horquilla dorada de doble corona, resorte naranja, ruedas con tacos y rayos) | `Assets/Art/Models/Prototipo/` |
| Corredor | Casco integral, antiparras naranjas, jersey naranja con el 22 en la espalda, rodilleras con barro | mismo lugar |
| Pinos, árboles de hoja ancha, helechos, rocas, montañas | Mallas por código con varias variantes | mismo lugar |
| Materiales | URP/Lit con textura, normal y brillo (el barro mojado brilla) | `Assets/Art/Materials/Prototipo/` |
| Post-procesado | Tonos neutros, contraste, bloom suave y viñeta | `Assets/Settings/Pista01_PostProceso.asset` |

El corredor y la bici son **rígidos** (sin huesos). Se mueven por pivotes, igual que antes:
`Inclinacion → (Cuadro, RuedaTrasera, EjeDireccion → Direccion → RuedaDelantera, Corredor → Piernas, Torso)`.
El eje de dirección está inclinado 27° como en una bici de DH real.

## Presupuesto para gama media

| Elemento | Valor |
|---|---|
| Bici + corredor | ≈ 14 000 triángulos |
| Pino / árbol de hoja ancha | 400 – 520 triángulos |
| Helecho | ≈ 160 triángulos |
| Texturas | 256 – 1024 px, comprimidas, con mipmaps |
| Sombras | Una luz, hasta 60 m |
| Niebla | Lineal de 25 a 380 m (esconde la distancia y da el ambiente) |
| Post-procesado | Solo efectos baratos (sin SSAO, sin desenfoque de movimiento) |

Todos los árboles, rocas y helechos son **estáticos**: Unity los agrupa y dibuja en pocas llamadas.

## Si va lento en una PC

1. **Project Settings → Quality**: baja a un nivel con menos calidad de texturas.
2. En `Assets/Settings/PC_RPAsset`: baja la **distancia de sombras** (60 → 40) o las cascadas (4 → 2).
3. En la escena, desactiva el objeto `PostProceso`.
4. En `PrototypeSceneBuilder.cs`, baja la cantidad de árboles (probabilidad 0.3) o helechos (0.55) y reconstruye.

## Cómo cambiar el aspecto

- **Colores de la bici o el corredor**: `BikeModelBuilder.cs` (materiales) y `ProceduralTextures.cs`
  (`BikeCamo`, `Jersey`, `Helmet`).
- **Forma de la bici o el corredor**: `ProceduralModels.cs` (todas las medidas están en metros).
- **Barro y polvo de la rueda**: componente `WheelSpray` en `BiciVisual` (cantidades en el Inspector).
- **Niebla y luz**: `BuildLighting` en `PrototypeSceneBuilder.cs`.

Después de cambiar un script de Editor, vuelve a usar el menú para reconstruir la escena.

## Más adelante (no incluido)

- Animación con huesos del corredor (pedaleo, brazos que siguen el manubrio).
- LOD para los árboles (versión simple a lo lejos) si se agregan muchos más.
- Texturas pintadas a mano o escaneadas para reemplazar las procedurales.
