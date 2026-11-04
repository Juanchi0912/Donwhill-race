# Plan de trabajo: Prototipo v0.1 de DownhillRace

**Proyecto:** DownhillRace · **Motor:** Unity 6 (6000.0.50f1, URP) · **Plataforma:** PC (Windows)
**Para:** Codex o Claude Code, en la computadora del desarrollador
**Repositorio:** github.com/Juanchi0912/Donwhill-race

---

## 1. Objetivo del prototipo

Hacer **una bajada jugable de principio a fin** con una sensación de juego de *downhill arcade* clásico de consola: mucha velocidad, curvas con peralte, saltos con tiempo en el aire y carrera contra el reloj.

**Alcance (solo esto, nada más):**

| Incluido | Fuera de alcance (después) |
|---|---|
| Pantalla de inicio con el nombre del juego | Varios personajes o bicis |
| 1 modo de juego temporal: **Contrarreloj** | Rivales con IA, multijugador |
| 1 corredor genérico: **"Corredor 1"** | Trucos, combos, puntuación |
| 1 bicicleta genérica | Tienda, mejoras, guardado complejo |
| 1 pista corta (de 45 a 90 segundos) | Música y sonidos finales |
| Cronómetro, meta y mejor tiempo | Gráficos detallados o modelos finales |

**Regla de originalidad:** el juego es **original**. Se toma inspiración en la *sensación* de juego del género (velocidad, saltos, peraltes, contrarreloj), pero **no** se copian nombres, logotipos, personajes, pistas exactas, música, textos ni archivos de ningún juego existente.

**Gráficos:** no importan en esta versión. Usa formas simples (*greybox*) con colores planos. Debe correr fluido (60 FPS o más) en una PC media con gráficos integrados.

---

## 2. Reglas técnicas para la IA

- Usa **Unity 6**:
  - `Rigidbody.linearVelocity`, en lugar del antiguo `velocity`.
  - `linearDamping`, en lugar de `drag`.
  - `FindFirstObjectByType`, en lugar de `FindObjectOfType`.
  - `PhysicsMaterial`, en lugar de `PhysicMaterial`.
- Controles con el **Input System nuevo**. Envuelve el código con `#if ENABLE_INPUT_SYSTEM`, con una alternativa para el input antiguo. Centraliza la lectura en una clase estática `GameInput`.
- **No edites `.unity`, `.prefab` ni `.meta` a mano.** Para crear la escena, haz un **script de Editor** en `Assets/Scripts/Editor/` que agregue el menú **DownhillRace → Crear escena del prototipo**. Ese script construye todo por código (pista, bici, corredor, cámara, UI, luces) y guarda la escena en `Assets/Scenes/Pista01.unity`. Así el usuario no tiene que armar nada a mano.
- Escribe comentarios en español, con una clase por archivo y `[SerializeField] private` para lo que se ajusta en el Inspector.
- La física va en `FixedUpdate` y el input en `Update`.
- Trabaja **una fase a la vez**. Al terminar cada una:
  1. Lista los archivos que creaste o cambiaste.
  2. Explica qué probar en Unity, en Play Mode.
  3. Propón el mensaje de commit.
- No agregues paquetes de la Asset Store ni dependencias externas.

**Estructura de carpetas:**
```
Assets/Scripts/Managers/   GameManager.cs, GameInput.cs
Assets/Scripts/Player/     BikeController.cs, BikeVisuals.cs, CameraFollow.cs
Assets/Scripts/Track/      TrackGenerator.cs, TrackTrigger.cs
Assets/Scripts/UI/         TitleScreen.cs, RaceHUD.cs
Assets/Scripts/Editor/     PrototypeSceneBuilder.cs
Assets/Scenes/             Pista01.unity (generada por el script de Editor)
```

---

## 3. Fases de trabajo

### Fase 0: Preparación (la haces tú)

1. En la terminal, dentro de la carpeta del proyecto:
   `git checkout -b feature/prototipo-v01`
2. Guarda este archivo en `Docs/PLAN_PROTOTIPO.md`.
3. Abre Codex (`codex`) o Claude Code (`claude`) en la carpeta del proyecto.

**Prompt para la IA:**
> Lee AGENTS.md (o CLAUDE.md) y Docs/PLAN_PROTOTIPO.md. Resúmeme el plan en 5 líneas y dime si ves algo que no se pueda hacer en Unity 6. No escribas código todavía.

---

### Fase 1: Controles y flujo del juego
**Qué se construye:**

- `GameInput.cs`, con este mapa de controles:

  | Acción | Teclado | Mando |
  |---|---|---|
  | Pedalear | W / ↑ | Gatillo derecho (RT) |
  | Frenar | S / ↓ | Gatillo izquierdo (LT) |
  | Girar | A D / ← → | Stick izquierdo |
  | Agacharse (más velocidad) | Shift izquierdo | X / Cuadrado |
  | Saltar | Espacio | A / X |
  | Reaparecer | R | Y / Triángulo |
  | Empezar / confirmar | Enter | Start |
  | Volver / reiniciar | Esc | Select |

- `GameManager.cs`, con los estados **Inicio → Cuenta regresiva (3, 2, 1, ¡YA!) → Carrera → Meta**. Lleva el cronómetro, guarda el mejor tiempo en `PlayerPrefs` y reinicia la carrera.

**Prompt:**
> Haz la Fase 1 del plan: GameInput y GameManager. Todavía no hay escena; solo los scripts. Verifica que compilen en Unity 6.

**Listo cuando:** Unity compila sin errores rojos en la consola.

---

### Fase 2: Bicicleta y física de juego
**Sensación buscada:** arcade, no simulador. Que sea fácil de controlar, que la velocidad se sienta mucho y que caerse sea raro.

**Diseño recomendado:**

- La física la lleva una **esfera invisible** con `Rigidbody`, un `SphereCollider` de radio 0.5, un `PhysicsMaterial` sin fricción y la rotación congelada.
- El **modelo visual** de la bici sigue a la esfera y se alinea con la normal del suelo, usando un raycast hacia abajo.
- **Agarre:** en cada `FixedUpdate` se elimina parte de la velocidad lateral, para que la bici vaya hacia donde apunta.
- **La pendiente acelera sola** con la gravedad. Pedalear solo ayuda hasta unos 40 km/h.

**Valores iniciales** (todos ajustables en el Inspector):

| Parámetro | Valor |
|---|---|
| Velocidad máxima normal | 22 m/s (≈ 80 km/h) |
| Velocidad máxima agachado | 28 m/s (≈ 100 km/h) |
| Pedaleo: aceleración / tope | 7 m/s² / 11 m/s |
| Freno | 14 m/s² |
| Giro: lento / rápido | 120 °/s / 60 °/s (agachado: ×0.6) |
| Salto | 4.5 m/s hacia arriba |
| Gravedad extra en el aire | +60 % |
| Control en el aire | 35 % del giro |

- **Reaparecer:** con la tecla R, o automáticamente si cae por debajo de la pista. Aparece en el último checkpoint.
- **`BikeVisuals.cs`:** las ruedas giran según la velocidad, la bici se inclina hasta 28° en las curvas, el manubrio gira y el corredor se agacha al usar Shift.

**Prompt:**
> Haz la Fase 2: BikeController y BikeVisuals según el plan (esfera física + modelo visual). Expón todos los valores en el Inspector con [Header] y [Tooltip].

**Listo cuando:** compila, y los valores aparecen ordenados en el Inspector.

---

### Fase 3: Corredor 1 y bici genérica
Se arman **por código, con primitivas de Unity** (cubos, cilindros, esferas), sin colliders en las piezas visuales:

- **Bici:** cuadro de tubos, dos ruedas de unos 0.7 m de diámetro (llanta oscura y aro gris), horquilla, manubrio ancho y asiento. Distancia entre ejes de unos 1.15 m.
- **Corredor 1:** de pie sobre los pedales en posición de ataque, con casco de color llamativo, torso, brazos al manubrio y piernas a los pedales.
- La jerarquía debe permitir animar por código: `Inclinacion` → (`Cuadro`, `RuedaTrasera`, `Direccion` → `RuedaDelantera`, `Corredor`).
- Colores originales, por ejemplo: cuadro rojo, jersey verde azulado y casco naranja.

**Prompt:**
> Haz la Fase 3 dentro del script de Editor PrototypeSceneBuilder: una función que construya la bici y el Corredor 1 con primitivas, con la jerarquía del plan, y los conecte a BikeController y BikeVisuals.

---

### Fase 4: La pista corta
**Generación:** `TrackGenerator.cs` crea la malla por código a partir de una lista de puntos de control, con una curva Catmull-Rom. El perfil transversal es:
`pasto | berma elevada | camino de tierra (9 m) | berma elevada | pasto`.
La misma malla sirve de `MeshCollider`. Usa 3 submallas: tierra, bermas y pasto.

**Recorrido:** entre 450 y 550 m, para unos 60 segundos de carrera, siempre bajando:

| Tramo | Qué tiene | Para qué |
|---|---|---|
| 1. Salida | Plano corto con arco de salida | Arrancar pedaleando |
| 2. Recta rápida | Pendiente fuerte | Primera sensación de velocidad |
| 3. Curvas en S | Dos curvas con bermas altas | Aprender a girar y frenar |
| 4. Primer salto | Rampa en recta, con caída en bajada | Tiempo en el aire |
| 5. Bosque | Camino con muchos árboles a los lados | Sensación de velocidad |
| 6. Zona de rocas | Rocas en los bordes, camino algo más angosto | Precisión |
| 7. Curva cerrada | La más cerrada de la pista | Obliga a frenar |
| 8. Salto final | Rampa grande | Momento espectacular |
| 9. Sprint y meta | Recta final con arco de meta | Cierre |

- **Checkpoints** invisibles cada 100 m (`TrackTrigger`, tipo Checkpoint) y un trigger de **Meta**.
- **Árboles:** cono más cilindro, en el pasto, a 2 m o más del borde de cualquier tramo del camino. Sin colliders, marcados como static.
- **Rampas:** bloques inclinados 14°, de 5 m de largo y 3.5 m de ancho, con collider.
- **Barreras** detrás de la salida y después de la meta.
- **Altura de caída para reaparecer:** el punto más bajo de la pista menos 25 m.

**Prompt:**
> Haz la Fase 4: TrackGenerator, TrackTrigger y, en PrototypeSceneBuilder, la colocación de checkpoints, meta, rampas, rocas, árboles y barreras según la tabla de tramos.

---

### Fase 5: Cámara, pantalla de inicio y HUD

- **Cámara (`CameraFollow`):** detrás y arriba del corredor (5 m atrás, 2.1 m arriba), con movimiento suave. El campo de visión sube de 60° a 76° con la velocidad. No debe atravesar el suelo; usa un linecast.
- **Pantalla de inicio (`TitleScreen`):** el título **DOWNHILL RACE** en grande, el subtítulo "Prototipo v0.1", el texto parpadeante "Presiona ENTER para comenzar" y la lista de controles. De fondo, la cámara gira lentamente alrededor del Corredor 1 en la salida.
- **Modo temporal:** debajo del título, solo una opción, "Contrarreloj · Pista 1".
- **HUD de carrera (`RaceHUD`):** el tiempo arriba al centro, la velocidad en km/h abajo a la derecha, el mejor tiempo en pequeño y la cuenta regresiva en grande.
- **Pantalla de meta:** "¡META!", el tiempo, el mejor tiempo, "¡Nuevo récord!" si corresponde y "ENTER para correr otra vez".
- La UI se hace con uGUI y texto Legacy, usando la fuente `LegacyRuntime.ttf`, para no depender de importar TextMeshPro.

**Prompt:**
> Haz la Fase 5: CameraFollow, TitleScreen y RaceHUD, y agrégalos en PrototypeSceneBuilder.

---

### Fase 6: Escena completa y rendimiento
`PrototypeSceneBuilder` debe:

1. Crear una escena nueva y construir todo lo anterior.
2. Agregar una luz direccional con sombras suaves, niebla lineal (de 80 a 350 m) y una cámara con FXAA.
3. Guardar la escena en `Assets/Scenes/Pista01.unity` y ponerla **primera** en Build Settings.

Ajustes para una PC media: distancia de sombras de unos 60 m, árboles marcados como static y nada de post-procesado pesado.

**Prompt:**
> Haz la Fase 6: termina PrototypeSceneBuilder para que con un solo clic en el menú DownhillRace → Crear escena del prototipo quede todo listo para jugar.

**Lo que haces tú en Unity:**

1. Espera a que compile.
2. Haz clic en el menú **DownhillRace → Crear escena del prototipo**.
3. Presiona **Play**.

---

### Fase 7: Ajuste de jugabilidad (la más importante)
Juega varias bajadas y dile a la IA lo que sientes, con frases concretas. Por ejemplo:

- "Gira demasiado brusco a alta velocidad."
- "Los saltos se sienten flotantes."
- "Se siente lenta en la recta rápida."
- "La cámara marea en las curvas."

**Prompt:**
> Ajustemos la jugabilidad. Siento esto: <tu descripción>. Propón qué valores cambiar en el Inspector y por qué, antes de tocar código.

**Prueba de aceptación del prototipo:**

- [ ] Se ve la pantalla de inicio con el nombre y se empieza con Enter.
- [ ] La cuenta regresiva funciona y la bici no se mueve antes del "¡YA!".
- [ ] Se puede bajar toda la pista en 45 a 90 segundos.
- [ ] Las bermas ayudan a tomar las curvas y los saltos dan tiempo en el aire.
- [ ] R y las caídas reaparecen en el último checkpoint.
- [ ] En la meta se muestran el tiempo y el récord, y Enter reinicia.
- [ ] Va a 60 FPS o más en tu PC (revísalo en la pestaña Game → **Stats**).
- [ ] No hay errores rojos en la consola.

---

### Fase 8: Subir a GitHub
```
git add .
git commit -m "Prototipo v0.1: inicio, contrarreloj, Corredor 1 y Pista 1"
git push -u origin feature/prototipo-v01
```
Después, en GitHub, abre un **Pull Request** de `feature/prototipo-v01` hacia `main` y, si todo está bien, haz **Merge**.

---

## 4. Consejos para trabajar con la IA

- Pide **una fase por mensaje**. No pidas "haz todo el juego".
- Después de cada fase: vuelve a Unity, espera a que compile, prueba y luego haz un commit.
- Si aparece un error rojo en la consola de Unity, **cópialo completo** y pégaselo a la IA.
- Lee lo que la IA quiere cambiar antes de aprobarlo.
- Si algo se rompe y no sabes por qué, puedes volver al último commit con `git restore .` (con cuidado: borra los cambios que no guardaste).

## 5. Próximas versiones (no hacer ahora)

- v0.2: sonidos (rueda en tierra, viento según la velocidad, aterrizajes) y partículas de polvo.
- v0.3: un rival con IA que siga la pista.
- v0.4: trucos simples en el aire.
- v0.5: modelos 3D reales del corredor y la bici, y una segunda pista.
