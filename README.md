# 🚵 DownhillRace

> Proyecto de videojuego desarrollado en **Unity**.
> 🔒 Repositorio **privado**: solo los colaboradores invitados pueden verlo.

## 📋 Descripción

Juego de **downhill en bicicleta de montaña**: baja la montaña lo más rápido posible,
salta rampas, esquiva obstáculos y supera tu mejor tiempo.

## 🛠️ Requisitos

- **Unity** versión: `6000.0.50f1` (Unity 6 LTS; todos deben usar **la misma**)
- **Git** y **Git LFS** instalados
- Cuenta de GitHub con acceso al repositorio

## 🚀 Cómo empezar (para colaboradores)

1. Acepta la invitación que te llegó por correo o en https://github.com/notifications
2. Clona el repositorio:
   ```bash
   git lfs install
   git clone https://github.com/Juanchi0912/Donwhill-race.git
   ```
3. Abre **Unity Hub** → **Add** → selecciona la carpeta clonada.
4. Espera a que Unity regenere la carpeta `Library` (la primera vez tarda).

## 📁 Estructura

```
Assets/
├── Scripts/
│   ├── Player/      # Movimiento y lógica del jugador
│   ├── Enemies/     # IA y comportamiento de enemigos
│   ├── UI/          # Menús, HUD, botones
│   └── Managers/    # GameManager, AudioManager, etc.
├── Scenes/          # Escenas (.unity)
├── Prefabs/         # Objetos reutilizables
├── Art/
│   ├── Sprites/
│   ├── Models/
│   ├── Materials/
│   └── Textures/
├── Audio/
│   ├── Music/
│   └── SFX/
└── Animations/
Docs/                # Guías (GitHub, IA) y diseño
CLAUDE.md            # Reglas para Claude Code
AGENTS.md            # Reglas para Codex
```

## 🤖 Desarrollo con IA

Este proyecto está preparado para **Claude Code** (`CLAUDE.md`) y **Codex** (`AGENTS.md`).
Ver [`Docs/GUIA_IA.md`](Docs/GUIA_IA.md).

## 🌿 Flujo de trabajo con ramas

- `main` → versión estable, siempre debe funcionar.
- `develop` → integración del trabajo del equipo.
- `feature/nombre` → cada tarea nueva (ej. `feature/salto-doble`).

```bash
git checkout develop
git pull
git checkout -b feature/mi-tarea
# ... trabajas ...
git add .
git commit -m "Agrega salto doble al jugador"
git push -u origin feature/mi-tarea
```
Luego abre un **Pull Request** hacia `develop` en GitHub.

## ⚠️ Reglas del equipo

- **No** trabajen dos personas en la misma escena a la vez (los conflictos en `.unity` son difíciles de resolver).
- Siempre hagan `git pull` antes de empezar a trabajar.
- Suban siempre los archivos `.meta` junto con su asset.
- No suban las carpetas `Library/`, `Temp/` ni builds (ya están en `.gitignore`).

## 👥 Equipo

| Nombre | Rol |
|---|---|
| _Tu nombre_ | Líder / Programación |
| | |

## 📄 Licencia

Proyecto privado. Todos los derechos reservados.
