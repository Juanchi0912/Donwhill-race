# Modo BMX — Pista La Concordia (Cuenca)

**BMX Racing** sobre la pista de bicicross del Parque La Concordia. En el menú: **Jugar → BMX** (va directo a la
pista). Por ahora es por tiempo (cronómetro y mejor tiempo), como Carrera; los rivales vendrán con Avalancha.

## Cómo se hizo

Un heightmap no sirve para BMX (los datos satelitales no ven saltos de 1–2 m), así que el terreno se arma por
código desde el trazado que marcaste sobre la foto aérea: `Assets/Scripts/Editor/BmxTrackBuilder.cs`.
Medida con los 316 m que da Google Earth y **agrandada ×1,35** para la velocidad del juego (unos 430 m),
así entre rampa y rampa hay tiempo de tomar velocidad. La forma es la de la foto.

| Parte | Qué tiene |
|---|---|
| Salida | Rampa de cemento de 4,5 m con **portón rojo** que cae con el ¡YA! |
| Recta 1 | Un doble grande y una loma |
| Recta 2 | Mesa y doble |
| Recta 3 | Tres lomas seguidas (ritmo) y un doble |
| Recta 4 | Doble, mesa y una loma |
| Recta 5 | Tres lomas, un doble, el sprint y la **línea blanca de meta** |
| Curvas | Las 4 curvas de 180° de **asfalto negro** con peralte alto |

Los saltos son **lomas grandes de todo el ancho**, como en la pista real, con 6–8 m planos entre uno y otro.
Cada recta va un poco más abajo que la anterior (la pista baja 5 m en terrazas) y entre recta y recta hay
un talud pintado de **piedra**, como los muros del video.

**Colores medidos en la foto:** rojo coral **#CF5F5A** en rectas y saltos, asfalto **#3A3B3D** en las curvas,
**líneas blancas** en los bordes y cemento gris en la salida.

**Manejo de BMX:** acelera más rápido pero llega a menos velocidad (15 m/s, 17 agachado), salta más alto y gira
más cerrado. (Por ahora es la misma bici con otros valores; el modelo de bici BMX viene después.)

Es una versión provisional: cuando haya un **modelo 3D** de la pista real, se usa ese para que quede igual.

## Crear o rehacer la pista

Menú de Unity: **DownhillRace → Crear pista BMX: La Concordia** (crea `Assets/Scenes/BMX_Concordia.unity`).
Para que aparezca la tarjeta en el menú: **DownhillRace → Crear escena del menú**.

Para cambiar saltos o su altura: en `BmxTrackShape.cs` (constructor), cada elemento tiene tipo
(`Rollers`, `Double`, `Table`, `StartHill`), desde/hasta (metros del juego) y altura.
