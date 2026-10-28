# Guía: subir el proyecto a GitHub (repositorio privado)

## 1. Instalar lo necesario (una sola vez)

- **Git**: https://git-scm.com/downloads
- **Git LFS**: https://git-lfs.com (en Windows ya viene incluido con Git)
- Configura tu nombre y correo:
  ```bash
  git config --global user.name "Tu Nombre"
  git config --global user.email "tucorreo@ejemplo.com"
  git lfs install
  ```

## 2. Configurar Unity para Git

En Unity: **Edit → Project Settings → Editor**
- **Version Control → Mode:** `Visible Meta Files`
- **Asset Serialization → Mode:** `Force Text`

## 3. Crear el repositorio PRIVADO en GitHub

1. Entra a https://github.com/new
2. **Repository name:** `DownhillRace`
3. Marca **🔒 Private**
4. **NO** marques "Add a README", ".gitignore" ni "license" (ya los tienes aquí).
5. Clic en **Create repository**.

## 4. Subir el proyecto

Copia los archivos de esta plantilla (`.gitignore`, `.gitattributes`, `README.md`,
`Docs/` y las carpetas de `Assets/`) dentro de la carpeta de tu proyecto de Unity.
Luego, abre una terminal en esa carpeta:

```bash
git init
git lfs install
git add .
git commit -m "Primer commit: estructura del proyecto"
git branch -M main
git remote add origin https://github.com/Juanchi0912/Donwhill-race.git
git push -u origin main

# Crear la rama de desarrollo
git checkout -b develop
git push -u origin develop
```

> La primera vez GitHub te pedirá iniciar sesión (se abre el navegador).

## 5. Invitar colaboradores (sin hacerlo público)

1. En tu repositorio: **Settings → Collaborators** (menú izquierdo).
2. Clic en **Add people**.
3. Escribe el usuario de GitHub o correo de la persona.
4. Ella recibe una invitación y debe **aceptarla**.

El repositorio sigue siendo privado: solo tú y los invitados pueden verlo.
En una cuenta gratuita puedes invitar colaboradores ilimitados a repos privados.

## 6. Proteger la rama `main` (recomendado)

**Settings → Branches → Add branch ruleset** (o *Add rule*):
- Rama: `main`
- Activa **Require a pull request before merging**

Así nadie sube cambios directo a `main` sin revisión.

## 7. Organizar tareas (opcional)

- **Issues**: para reportar bugs o ideas.
- **Projects**: tablero tipo Trello (Por hacer / En progreso / Hecho).

## Problemas comunes

| Problema | Solución |
|---|---|
| "File exceeds 100 MB" | Asegúrate de haber hecho `git lfs install` **antes** de `git add`. |
| El colaborador no ve el repo | Debe aceptar la invitación en https://github.com/notifications |
| Conflictos en escenas | Usen prefabs y que cada uno trabaje en escenas distintas. |
| Faltan referencias/materiales rosados | Alguien no subió los archivos `.meta`. |
