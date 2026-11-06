# Menús y pantalla de carga

## Flujo

```
Presentación  →  Menú principal  →  Modos de juego  →  Elige la pista  →  Carga  →  Carrera
"DOWNHILL RACE"   Jugar               Arcade     (próximamente)   Pista 1
                  Configuraciones     Avalancha  (próximamente)   La Paz
                  (vacía por ahora)   Carrera    ✔                 Turi Bike Park (en construcción)
                                      Freeride   (próximamente)
```

- **Elige la pista** (solo en Carrera): **Pista 1** (bosque nublado, escena `Pista01`),
  **La Paz** (Yunguilla, Azuay, mapa real, escena `LaPaz`; en el juego nunca se muestra como "Pista 2") y
  **Turi Bike Park** (mapa real de Cuenca, escena `MapaReal`, todavía sin rampas ni puentes).

- **Presentación:** solo el nombre del juego. Aparece y se desvanece en unos 3 s;
  cualquier botón (Enter, Esc, Espacio, clic) la salta.
- **Configuraciones:** existe y dice "Próximamente"; tiene el botón Volver.
- **Modos:** los modos no disponibles muestran "Próximamente" y no salen del menú.
- **Carga:** barra de progreso y un consejo al azar; se ve al menos 1.2 s.
- **Al terminar la carrera:** Enter corre otra vez; Esc vuelve al **menú principal**
  (sin repetir la presentación).

## Controles del menú

| Acción | Teclado | Mando | Mouse |
|---|---|---|---|
| Moverse entre botones | Flechas | Stick / cruceta | Pasar por encima |
| Aceptar | Enter | A / Cruz | Clic |
| Volver | Esc | Select | Botón "Volver" |

## Crear o reconstruir la escena

Menú de Unity: **DownhillRace → Crear escena del menú**. Crea `Assets/Scenes/Menu.unity`
y la deja primera en Build Settings (después van `Pista01`, `LaPaz` y `MapaReal`). Para jugar el flujo completo,
abre `Menu.unity` y presiona Play. Las pistas tienen que estar construidas antes
(**Crear escena del prototipo**, y para La Paz y el Turi Bike Park **Crear mapa desde heightmap**
eligiendo el mapa y con *Jugable* activado; ver [MAPA_REAL.md](MAPA_REAL.md)).

## Scripts

| Script | Dónde está | Qué hace |
|---|---|---|
| `MainMenuController` | `UI` (escena Menu) | Cambia de pantalla, conecta los botones y maneja Esc |
| `LoadingScreen` | `UI` (escena Menu) | Carga la pista en segundo plano y mueve la barra |
| `GameSession` | (clase estática) | Modo elegido y nombres de escenas; viaja entre escenas |
| `GameMode` | (enum) | Arcade, Avalancha, Carrera, Freeride |

En el Inspector de `MainMenuController` se ajustan los tiempos de la presentación
(aparecer, quedarse y desvanecerse) y cuánto dura el aviso "Próximamente".
En `LoadingScreen` se ajustan el tiempo mínimo de carga y la lista de consejos.

## Para más adelante

- Fondos con imagen (por ahora son degradados simples, en `Assets/Art/Textures/Prototipo/FondoMenu.png`).
- Contenido de Configuraciones (volumen, calidad gráfica, controles).
- Activar los otros modos: en `GameSession.IsAvailable` y con su propia escena.
