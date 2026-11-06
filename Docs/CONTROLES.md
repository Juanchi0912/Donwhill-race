# Controles

El juego se juega con **teclado** o con **mando** (Xbox, PlayStation u otro compatible).
Las ayudas en pantalla cambian solas a las teclas o a los botones del mando según con qué estés jugando,
y nombran los botones de PlayStation (Cruz, Cuadrado, R1…) si el mando es de PlayStation.

## En la carrera

| Acción | Teclado | Xbox | PlayStation |
|---|---|---|---|
| Pedalear | W / ↑ | **A** (o RT, dosificable) | **Cruz** (o R2) |
| Frenar | S / ↓ | **X** (o LT) | **Cuadrado** (o L2) |
| Girar | A D / ← → | Stick izquierdo / cruceta | Stick izquierdo / cruceta |
| Saltar | Espacio | RB | R1 |
| Agacharse (mantener) | Shift izq. | LB | L1 |
| Reaparecer en el checkpoint | R | Y | Triángulo |
| Reiniciar la bajada | Esc | Select | Share |

El pedaleo va con el botón de abajo (A / Cruz), como en los juegos clásicos de bajada.
Saltar y agacharse están en los botones de arriba (RB / LB) para usarlos sin soltar el pedaleo.
**B / Círculo no reinicia** en plena carrera: es fácil apretarlo sin querer.

## En la meta

| Acción | Teclado | Xbox | PlayStation |
|---|---|---|---|
| Correr otra vez | Enter | A | Cruz |
| Volver al menú | Esc | B | Círculo |

Durante el primer segundo en la meta no se aceptan botones, para no reiniciar sin querer.

## En los menús

| Acción | Teclado | Mando |
|---|---|---|
| Elegir | Flechas | Stick / cruceta |
| Aceptar | Enter | A / Cruz |
| Volver | Esc | B / Círculo (o Select) |

## Vibración

Con mando, la bici vibra al **aterrizar** (más fuerte cuanto más tiempo en el aire), al **entrar al río**,
al **quedarse sin energía** y al **reaparecer**, más un temblor suave del terreno según la velocidad.
Se ajusta en el componente **GamepadRumble** de la bici (`Bici`): **Strength** (0 = apagada) y **Terrain Feel**.

## Energía (apagada por ahora)

La energía para pedalear está **apagada** mientras se prueban las pistas largas: la barra queda llena y dice
«ENERGÍA (APAGADA)». No se borró: para volver a activarla, en `Assets/Scripts/Managers/GameSession.cs`
cambia `EnergyEnabled` a `true`.

## Cambiar los botones

Todo el mapeo está en `Assets/Scripts/Managers/GameInput.cs`, en un solo lugar.
