# CLAUDE.md

Instrucciones para Claude Code sobre este proyecto. Claude lee este archivo al iniciar.

## Proyecto

Videojuego **downhill de bicicleta de montaña** hecho en **Unity** (C#).
El jugador baja una montaña en bici, esquivando obstáculos, saltando rampas
y tratando de llegar a la meta en el menor tiempo posible.

- Motor: Unity 6 (6000.0.50f1) (render pipeline: URP)
- Lenguaje: C#
- Idioma del equipo: **español** (comentarios, commits y explicaciones en español)

## Estructura

- `Assets/Scripts/Player/` → bici y control del jugador
- `Assets/Scripts/Managers/` → GameManager, RaceManager, AudioManager
- `Assets/Scripts/UI/` → HUD, menús
- `Assets/Scripts/Enemies/` → obstáculos y peligros de la pista
- `Assets/Scripts/Track/` → generación de la pista y zonas (checkpoints, meta)
- `Assets/Scripts/Editor/` → herramientas del Editor (constructor de la escena del prototipo)
- `Assets/Scenes/` → escenas (.unity)
- `Assets/Prefabs/` → objetos reutilizables
- `Docs/` → documento de diseño y guías

## Reglas de código

- Una clase pública por archivo; el archivo se llama igual que la clase.
- Clases y métodos en `PascalCase`; campos privados en `camelCase`.
- Usa `[SerializeField] private` en lugar de campos `public` para lo que se edita en el Inspector.
- Física (fuerzas, velocidad) en `FixedUpdate`; lectura de input en `Update`.
- Evita `GameObject.Find` y `GetComponent` dentro de `Update`; guarda referencias en `Awake`.
- Comentarios breves en español explicando el "por qué", no el "qué".

## Reglas importantes

- **No modifiques** archivos `.unity`, `.prefab` ni `.meta` a mano salvo que se pida:
  son YAML generado por Unity y se rompen fácil. Explica los cambios de escena
  como pasos que el usuario hace en el Editor.
- **No crees** carpetas `Library/`, `Temp/` ni `Logs/`.
- Cuando crees un script, di en qué GameObject ponerlo y qué valores configurar en el Inspector.
- Antes de cambios grandes, propón un plan corto y espera confirmación.
- Trabaja en ramas `feature/...` y haz commits pequeños con mensajes en español.

## Cómo probar

Unity no se puede ejecutar desde la terminal en este proyecto. Para verificar:
1. Revisa que el código compile (sin errores de sintaxis ni `using` faltantes).
2. Indica al usuario qué probar en Play Mode y qué resultado esperar.
