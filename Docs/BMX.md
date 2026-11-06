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
| Salida | Rampa de 4,5 m con **portón rojo** que cae con el ¡YA! |
| Recta 1 | Un doble y lomas |
| Recta 2 | Dos líneas: **pro** (doble, mesa y doble) y **fácil** (lomas suaves), una al lado de la otra |
| Recta 3 | Sección rítmica de 6 lomas y un doble |
| Recta 4 | Mesa y dos dobles |
| Recta 5 | Lomas, un doble, el sprint y la **línea blanca de meta** |
| Curvas | Las 4 curvas de 180° con **peralte** |

**Colores como en la foto:** cemento **gris** en curvas, rectas planas y la rampa de salida; arcilla **naranja**
donde están los saltos y las lomas; pasto alrededor. Cinta «DOWNHILL RACE» a los dos lados.

**Manejo de BMX:** acelera más rápido pero llega a menos velocidad (15 m/s, 17 agachado), salta más alto y gira
más cerrado. (Por ahora es la misma bici con otros valores; el modelo de bici BMX viene después.)

## Crear o rehacer la pista

Menú de Unity: **DownhillRace → Crear pista BMX: La Concordia** (crea `Assets/Scenes/BMX_Concordia.unity`).
Para que aparezca la tarjeta en el menú: **DownhillRace → Crear escena del menú**.

Para cambiar saltos o su altura: en `BmxTrackBuilder.cs`, método `Layout()`, cada elemento tiene tipo
(`Rollers`, `Double`, `Table`, `StartHill`), desde/hasta (metros de la pista real) y altura. Si cuál es la línea
pro y cuál la fácil está al revés, se cambian ahí.
