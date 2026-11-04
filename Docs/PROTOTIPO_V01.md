# Prototipo v0.1: cómo usarlo y ajustarlo

Guía rápida de lo construido según [PLAN_PROTOTIPO.md](PLAN_PROTOTIPO.md).

## Crear la escena (un solo clic)

1. Abre el proyecto en Unity 6 y espera a que compile (la consola no debe tener errores rojos).
2. En el menú superior: **DownhillRace → Crear escena del prototipo**.
3. Se crea y se abre `Assets/Scenes/Pista01.unity`, que queda **primera** en Build Settings.
4. Presiona **Play**.

Puedes repetir el paso 2 cuando quieras: reconstruye la escena desde cero (pide confirmación
antes de reemplazarla). Los materiales y mallas generados quedan en
`Assets/Art/Materials/Prototipo/` y `Assets/Art/Models/Prototipo/`.

> Los cambios que hagas a mano en `Pista01.unity` se pierden al reconstruirla.
> Para cambios permanentes (recorrido, colores, objetos) edita `PrototypeSceneBuilder.cs`.
> Los valores de juego (velocidad, giro, cámara) sí conviene ajustarlos en el Inspector
> y luego copiarlos como valores por defecto en el script.

## Scripts

| Script | Dónde está en la escena | Qué hace |
|---|---|---|
| `GameInput` | (clase estática) | Lee teclado y mando en un solo lugar |
| `GameManager` | `GameManager` | Estados Inicio → Cuenta → Carrera → Meta, cronómetro y récord |
| `BikeController` | `Bici` (esfera invisible) | Física arcade: pendiente, agarre, giro, salto, reaparecer |
| `BikeVisuals` | `BiciVisual` | Inclina el modelo, gira ruedas y manubrio, agacha al corredor |
| `CameraFollow` | `Main Camera` | Sigue al corredor; en el inicio gira a su alrededor |
| `TrackGenerator` | `Pista` | Malla y collider de la pista (Catmull-Rom, bermas, peralte) |
| `TrackTrigger` | `Checkpoints/*` | Checkpoints cada 100 m y la meta |
| `TitleScreen` / `RaceHUD` | `UI` | Pantalla de inicio, HUD y pantalla de meta |
| `PrototypeSceneBuilder` | (Editor) | Construye toda la escena por código |

## La pista (≈ 548 m, ≈ 50 m de desnivel)

| Tramo | Desde (m) | Qué tiene |
|---|---|---|
| 1. Salida | 0 | Plano corto, arco azul de salida y barrera detrás |
| 2. Recta rápida | 30 | Pendiente de 9° |
| 3. Curvas en S | 112 | Derecha 70° y luego izquierda 80°, con bermas de hasta 2 m |
| 4. Primer salto | 200 | Rampa de 14° (5 × 3.5 m) a los 222 m, caída más empinada |
| 5. Bosque | 260 | Tres curvas suaves con árboles densos a los lados |
| 6. Zona de rocas | 335 | Camino de 7 m con rocas en los bordes |
| 7. Curva cerrada | 400 | 135° con radio de unos 10 m: hay que frenar |
| 8. Salto final | 428 | Rampa grande de 14° (8 × 5 m) a los 448 m |
| 9. Sprint y meta | 488 | Meta a ≈ 520 m, barrera al final |

Según una simulación del modelo de física, una bajada perfecta dura unos 40-45 s;
una bajada normal, con frenadas y saltos, debería quedar entre 50 y 70 s.

## Qué probar en Play Mode

- [ ] Se ve **DOWNHILL RACE**, "Prototipo v0.1", "Contrarreloj · Pista 1" y la cámara gira alrededor del corredor.
- [ ] **Enter** inicia la cuenta 3, 2, 1, ¡YA!; la bici no se mueve antes del ¡YA!.
- [ ] La bici acelera sola en las bajadas; **W** solo ayuda hasta ≈ 40 km/h.
- [ ] **Shift** agacha al corredor y deja llegar a ≈ 100 km/h, pero gira menos.
- [ ] Las bermas de las curvas en S y de la curva cerrada ayudan a girar.
- [ ] Las rampas dan tiempo en el aire; **Espacio** salta.
- [ ] **R** (o caer fuera de la pista) reaparece en el último checkpoint.
- [ ] En la meta: "¡META!", tiempo, mejor tiempo y "¡Nuevo récord!" la primera vez.
- [ ] **Enter** corre otra vez; **Esc** vuelve al inicio (en carrera, Esc reinicia).
- [ ] 60 FPS o más en Game → **Stats**, y la consola sin errores rojos.

Para borrar el récord guardado: selecciona `GameManager`, haz clic derecho en el
componente y elige **Borrar mejor tiempo**.

## Dónde ajustar la sensación de juego (Fase 7)

| Si sientes que… | Prueba a cambiar (en `Bici` → BikeController) |
|---|---|
| Gira demasiado brusco a alta velocidad | Bajar **Turn Rate Fast** (60 → 45) |
| Cuesta girar en curvas lentas | Subir **Turn Rate Slow** (120 → 140) |
| Derrapa o "patina" en las curvas | Subir **Lateral Grip** (10 → 14) |
| Los saltos se sienten flotantes | Subir **Extra Air Gravity** (0.6 → 0.9) |
| Se siente lenta en la recta rápida | Bajar **Air Drag Coefficient** (0.005 → 0.004) |
| Va demasiado rápido en todo | Subir **Air Drag Coefficient** (0.005 → 0.006) |
| Se despega en las lomas | Subir **Ground Stick Force** (6 → 9) |

| Si sientes que… | Prueba a cambiar (en `Main Camera` → CameraFollow) |
|---|---|
| La cámara marea en las curvas | Bajar **Heading Smoothing** (5 → 3) |
| Se queda muy atrás | Subir **Position Smoothing** (7 → 10) |
| No se siente la velocidad | Subir **Max Field Of View** (76 → 82) |

Los cambios hechos en Play Mode se pierden al salir: anótalos y aplícalos fuera de Play.
