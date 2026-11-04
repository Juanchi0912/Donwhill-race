# Menús y pantalla de carga

## Flujo

```
Presentación  →  Menú principal  →  Modos de juego  →  Carga  →  Carrera (Pista01)
"DOWNHILL RACE"   Jugar               Arcade     (próximamente)
                  Configuraciones     Avalancha  (próximamente)
                  (vacía por ahora)   Carrera    ✔
                                      Freeride   (próximamente)
```

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
y la deja primera en Build Settings (la pista queda segunda). Para jugar el flujo completo,
abre `Menu.unity` y presiona Play.

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
