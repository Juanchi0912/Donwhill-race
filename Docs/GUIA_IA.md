# Guía: crear el proyecto y trabajar con IA (Claude Code o Codex)

## Paso 1. Crear el proyecto en Unity

1. Abre **Unity Hub** → **New project**.
2. Elige la plantilla **3D (URP)**.
3. Nombre: `DownhillRace` → elige la carpeta → **Create project**.
4. Cuando abra, ve a **Edit → Project Settings → Editor** y configura:
   - Version Control → Mode: **Visible Meta Files**
   - Asset Serialization → Mode: **Force Text**
5. Cierra Unity.

## Paso 2. Agregar la plantilla y subir a GitHub

1. Copia **todo** el contenido del zip (incluidos `.gitignore`, `.gitattributes`,
   `CLAUDE.md`, `AGENTS.md`, `Docs/` y `Assets/`) dentro de la carpeta del proyecto.
   Si Windows pregunta por reemplazar `Assets`, di que sí (solo agrega carpetas).
2. Crea el repo privado en https://github.com/new (sin README ni .gitignore).
3. Abre una terminal en la carpeta del proyecto:

```bash
git init
git lfs install
git add .
git commit -m "Primer commit: proyecto downhill"
git branch -M main
git remote add origin https://github.com/Juanchi0912/Donwhill-race.git
git push -u origin main
```

---

## Opción A: Claude Code (recomendado)

### Requisitos
- Plan de Claude **Pro o Max** (o una cuenta de la API de Anthropic).
- Git instalado.

### Instalar

**Windows (PowerShell):**
```powershell
irm https://claude.ai/install.ps1 | iex
```

**macOS / Linux:**
```bash
curl -fsSL https://claude.ai/install.sh | bash
```

Instrucciones oficiales actualizadas: https://docs.claude.com/en/docs/claude-code/overview

### Usar

```bash
cd C:\Juegos\DownhillRace
claude
```
La primera vez te pide iniciar sesión con tu cuenta de Claude.
Claude lee `CLAUDE.md` automáticamente y ya sabe que es un juego de downhill en Unity.

### Pídele cosas así

- `Crea el script de control de la bici: pedalear con W, frenar con S, girar con A/D y saltar con espacio`
- `Haz un sistema de checkpoints y respawn cuando el jugador se cae`
- `Crea un cronómetro y una línea de meta que guarde el mejor tiempo`
- `Tengo este error en la consola de Unity: <pega el error>`
- `Explícame cómo funciona BikeController.cs`

### Comandos útiles dentro de Claude Code
| Comando | Para qué |
|---|---|
| `/init` | Revisa el proyecto y mejora el `CLAUDE.md` |
| `/install-github-app` | Conecta Claude a tu repo para mencionar **@claude** en Issues y Pull Requests |
| `/clear` | Empieza una conversación nueva |
| `Shift + Tab` | Cambia a modo plan (propone antes de editar) |

### También desde el navegador o VS Code
- **Web:** en claude.ai/code conectas tu cuenta de GitHub, eliges el repo privado
  y Claude trabaja ahí y abre Pull Requests.
- **VS Code:** instala la extensión "Claude Code" desde el Marketplace.

---

## Opción B: Codex (OpenAI)

Requiere cuenta de ChatGPT de pago o API de OpenAI y Node.js instalado.

```bash
npm install -g @openai/codex
cd C:\Juegos\DownhillRace
codex
```
Codex lee el archivo `AGENTS.md` (mismas reglas que `CLAUDE.md`).

---

## Flujo de trabajo recomendado con IA

1. Crea una rama: `git checkout -b feature/control-bici`
2. Pídele a la IA la tarea.
3. Abre Unity: espera que compile y prueba en **Play**.
4. Si algo falla, copia el error de la consola y pégaselo a la IA.
5. Cuando funcione: `git add . && git commit -m "Control de la bici" && git push`
6. Abre un Pull Request en GitHub hacia `main`.

**Consejo:** pide una cosa a la vez. "Haz todo el juego" sale peor que ir
pieza por pieza: bici → cámara → pista → checkpoints → cronómetro → menús.

## Colaboradores
Cada persona del equipo instala Claude Code o Codex en su computadora con
su propia cuenta. Todos comparten el mismo `CLAUDE.md` / `AGENTS.md` del repo,
así que la IA sigue las mismas reglas para todos.
