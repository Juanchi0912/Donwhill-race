# Prototipo v0.1: cómo usarlo y ajustarlo

Guía rápida de lo construido según [PLAN_PROTOTIPO.md](PLAN_PROTOTIPO.md).

## Crear las escenas (un clic cada una)

1. Abre el proyecto en Unity 6 y espera a que compile (la consola no debe tener errores rojos).
2. En el menú superior: **DownhillRace → Crear escena del prototipo** (crea `Assets/Scenes/Pista01.unity`).
3. Luego: **DownhillRace → Crear escena del menú** (crea `Assets/Scenes/Menu.unity`, que queda
   **primera** en Build Settings y la pista segunda). Ver [MENUS.md](MENUS.md).
4. Abre `Menu.unity` y presiona **Play** (o abre `Pista01.unity` para probar la carrera directo).

Puedes repetir los pasos 2 y 3 cuando quieras: reconstruye la escena desde cero (pide confirmación
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
| `RaceHUD` | `UI` | HUD, controles durante la cuenta y pantalla de meta |
| `MainMenuController` / `LoadingScreen` | `UI` (escena Menu) | Menús y pantalla de carga ([MENUS.md](MENUS.md)) |
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

- [ ] Desde el menú (Jugar → Carrera) carga la pista y empieza sola la cuenta 3, 2, 1, ¡YA!; la bici no se mueve antes del ¡YA!.
- [ ] La bici acelera sola en las bajadas; **W** solo ayuda hasta ≈ 40 km/h.
- [ ] **Shift** agacha al corredor y deja llegar a ≈ 100 km/h, pero gira menos.
- [ ] Las bermas de las curvas en S y de la curva cerrada ayudan a girar.
- [ ] Las rampas dan tiempo en el aire; **Espacio** salta.
- [ ] **R** (o caer fuera de la pista) reaparece en el último checkpoint.
- [ ] En la meta: "¡META!", tiempo, mejor tiempo y "¡Nuevo récord!" la primera vez.
- [ ] **Enter** corre otra vez; **Esc** vuelve al menú principal (en carrera, Esc reinicia).
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

## Pedaleo y energía

- Pedalear (W / RT) empuja con **potencia constante**: mucho al arrancar y menos a más velocidad.
  Ayuda hasta ≈ 50 km/h; más rápido, solo acelera la pendiente.
- Pedalear a fondo gasta la **energía** (barra abajo a la izquierda) en ≈ 9 s. Sin pedalear,
  se recupera después de 0.8 s a 10 por segundo (más rápido con la bici detenida). Cada salto cuesta un poco.
- Con menos de 30 % de energía las piernas empujan menos. En 0 % quedas **AGOTADO**: solo pedaleas
  lento y sin fuerza, no puedes agacharte y el aire te frena más, hasta recuperar el 35 %.
- **En bajada la bici acelera sola**: la gravedad a lo largo del suelo está aumentada ×1.5
  (`BikeController` → **Slope Gravity Multiplier**; 1 = física real). Sin pedalear se llega a ≈ 56 km/h.
- Según la simulación en la Pista 1: administrando la energía ≈ 37 s; pedaleando siempre a fondo ≈ 40 s
  (te agotas varias veces); sin pedalear nunca ≈ 54 s.

### Cómo se ve el pedaleo

- El corredor pedalea **de pie**: las bielas giran al ritmo del pedaleo (más rápido a más velocidad)
  y las piernas siguen a los pedales con IK (`RiderLegIK`), con las rodillas hacia adelante.
- Los **brazos** siguen a los puños del manubrio (`RiderArmIK`). Pedaleando con energía, el torso se corre
  hacia el pedal que empuja y los brazos tiran del manubrio por turnos, de izquierda a derecha.
  Agotado, el pedaleo es lento y casi sin balanceo.
- Cada pedalada **empuja más con la biela horizontal** y casi nada arriba y abajo, como en una bici real.
- Pedaleando fuerte, la bici **se balancea de lado a lado** y el cuerpo sube y baja con cada pedalada.
- Al soltar W hay rueda libre: las bielas se detienen y quedan **niveladas** (un pie adelante y otro atrás).

| Si se ve… | Prueba a cambiar (en `BiciVisual`) |
|---|---|
| La bici se balancea demasiado | BikeVisuals → **Max Pedal Rock** (7 → 4) |
| El cuerpo rebota mucho | BikeVisuals → **Pedal Bob** (0.02 → 0.01) |
| Las piernas giran muy rápido o lento | `Bici` → BikeController → **Min/Max Cadence** |
| Los golpes de pedal se sienten demasiado | BikeController → **Pedal Stroke Pulse** (0.35 → 0.2) |

| Si sientes que… | Prueba a cambiar |
|---|---|
| La energía se acaba muy rápido | `Bici` → RiderStamina → **Drain Per Second** (11 → 8) |
| Tarda mucho en recuperarse | RiderStamina → **Regen Per Second** (10 → 14) |
| En bajada va demasiado rápido o lento | BikeController → **Slope Gravity Multiplier** (1.5) |
| Los brazos se mueven poco o mucho | `BiciVisual` → BikeVisuals → **Pedal Sway** (0.04) |
| Pedalear no se nota | BikeController → **Pedal Power** (22 → 28) |
| Pedaleando se va demasiado rápido | BikeController → **Pedal Top Speed** (14 → 12) |
| Agotarse castiga poco o demasiado | BikeController → **Exhausted Drag Multiplier** (1.5) |
