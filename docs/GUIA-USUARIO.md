# Guía del usuario — OpenAdoration

Guía práctica para el operador que proyecta durante el servicio.

> **Nota:** por ahora la interfaz de la aplicación está en **inglés**. En esta guía,
> los nombres de los botones aparecen entre comillas en inglés (por ejemplo, `"+ New"`)
> para que los encuentres en pantalla, con la explicación en español. El soporte de
> **varios idiomas (incluido español) está planeado para la versión 2.0.**

---

## Índice

1. [¿Qué es OpenAdoration?](#1-qué-es-openadoration)
2. [Instalación](#2-instalación)
3. [La pantalla principal](#3-la-pantalla-principal)
4. [Conectar el proyector](#4-conectar-el-proyector-pantalla-secundaria)
5. [Canciones](#5-canciones)
6. [Biblia](#6-biblia)
7. [Temas (apariencia)](#7-temas-apariencia)
8. [Multimedia (imágenes y videos)](#8-multimedia-imágenes-y-videos)
9. [Programa del servicio](#9-programa-del-servicio)
10. [Vista de escenario (monitor del operador)](#10-vista-de-escenario)
11. [Anuncios](#11-anuncios)
12. [Configuración](#12-configuración)
13. [Atajos de teclado](#13-atajos-de-teclado)
14. [Flujo recomendado el día del servicio](#14-flujo-recomendado-el-día-del-servicio)
15. [Solución de problemas](#15-solución-de-problemas)

---

## 1. ¿Qué es OpenAdoration?

OpenAdoration es un programa **gratuito** para proyectar letras de canciones,
versículos bíblicos e imágenes durante el culto. Funciona **totalmente sin
internet** — no necesita cuentas ni suscripciones, y toda la información se guarda
en tu computadora.

Una sola persona (el operador) lo maneja durante el servicio para controlar lo que
se ve en el proyector (una segunda pantalla).

**Requisitos:** Windows 10 o superior. No necesitas instalar nada más (ni .NET).
Una segunda pantalla o proyector es recomendable, pero también funciona con una
sola pantalla (se abre una ventana de vista previa).

---

## 2. Instalación

1. Abre el archivo **`OpenAdoration-1.0.0-win-x64.msi`**.
2. Si Windows muestra una advertencia ("Windows protegió tu PC"), haz clic en
   **"Más información" → "Ejecutar de todas formas"** (la app no está firmada todavía).
3. Sigue el asistente. Al terminar tendrás accesos directos en el **menú Inicio** y en
   el **Escritorio**.
4. Abre **OpenAdoration**. La primera vez se crea automáticamente la base de datos.

Tus datos se guardan en: `C:\Usuarios\<tu usuario>\AppData\Local\OpenAdoration\`

---

## 3. La pantalla principal

La ventana se divide en tres partes:

- **Menú lateral (izquierda):** las secciones del programa
  - `🎵 Songs` — Canciones
  - `📖 Bible` — Biblia
  - `📋 Service Schedule` — Programa del servicio
  - `🖼 Media` — Multimedia
  - `🎨 Themes` — Temas (apariencia)
  - `📺 Stage View` — Vista de escenario
  - `⚙ Settings` — Configuración
- **Área central:** la sección que tengas abierta.
- **Barra inferior (controles de proyección):** abrir/cerrar la pantalla del
  proyector, anuncios y los controles para avanzar diapositivas cuando estás
  proyectando (`◀`, `▶`, `Blank`, `Stop`).

En el menú **"Help" → "About OpenAdoration"** verás la versión y la lista de atajos
de teclado.

---

## 4. Conectar el proyector (pantalla secundaria)

1. Conecta el proyector o segundo monitor y configúralo en Windows como
   **"Extender"** (tecla `Windows + P` → "Extender").
2. En OpenAdoration, en la barra inferior haz clic en **"Open Screen"** para mostrar
   la pantalla de proyección. Para ocultarla, **"Close Screen"**.
3. Si solo tienes una pantalla, la proyección se abre en una ventana flotante que
   puedes mover/redimensionar.

La pantalla del proyector también se abre automáticamente la primera vez que
proyectas algo.

---

## 5. Canciones

### Crear una canción
1. Entra a `🎵 Songs` y haz clic en **"+ New"**.
2. Escribe el **título** (obligatorio) y, si quieres, autor, copyright y número
   **CCLI**.
3. Agrega secciones con los botones **"+ Verse"**, **"+ Chorus"**, **"+ Bridge"**,
   etc. Cada sección es un bloque de letra que se proyecta como diapositiva.
4. Usa **▲ / ▼** para reordenar y **✕** para borrar una sección.
5. **Play Order (orden de interpretación):** opcional. Escribe el orden en que se
   cantan las secciones usando fichas como `V1 C V2 C B C`
   (V = verso, C = coro, P = pre-coro, B = puente, I = intro, O = final, T = tag).
   Si lo dejas vacío, se usa el orden en que escribiste las secciones.
6. Haz clic en **"Save Song"**.

### Buscar
Escribe en la barra de búsqueda: primero busca por **título/autor**; si no encuentra,
busca dentro de la **letra**.

### Importar canciones
Haz clic en **"Import"** y elige un archivo. Formatos aceptados:
- **OpenLyrics** (`.xml`)
- **OpenSong** (`.xml` o sin extensión)
- **Texto plano** (`.txt`)

> En texto plano, puedes marcar secciones con líneas como `Verse 1`, `Chorus`, `V1`,
> `Bridge`; si no las pones, cada bloque separado por una línea en blanco se vuelve un verso.

### Proyectar
Haz clic en **▶** en la canción. Avanza con `Espacio`/`→` y retrocede con `←`.

---

## 6. Biblia

### Importar una versión
1. Entra a `📖 Bible` y haz clic en **"Import"**.
2. Elige el archivo de la traducción. Se aceptan **8 formatos**: Zefania XML, OSIS XML,
   USFX XML, thiagobodruk JSON, OpenAdoration JSON y BibleSuperSearch (JSON / ZIP / SQLite).
3. Espera a que termine (puedes **cancelar** una importación larga). Al final verás
   cuántos versículos se importaron.

### Navegar y proyectar
1. Elige la **versión** arriba.
2. Selecciona **libro → capítulo → versículo**. Al hacer clic en un versículo se
   **proyecta de inmediato**.
3. Con `←` / `→` te mueves entre los versículos del capítulo.

### Buscar
Escribe en la búsqueda. Hay dos modos (botón para alternar):
- **Keyword (palabras):** encuentra versículos que contengan todas las palabras.
- **Phrase (frase exacta):** encuentra la frase tal cual.

### Versículos por diapositiva
En `⚙ Settings` puedes definir cuántos versículos se muestran por diapositiva.

---

## 7. Temas (apariencia)

Un tema controla cómo se ve el texto proyectado: fuente, tamaño, color, alineación y
fondo.

1. Entra a `🎨 Themes` y haz clic en **"+ New"**.
2. Ajusta **fuente, tamaño, color del texto y alineación**.
3. **Fondo:** color sólido, **imagen** o **video** (en bucle).
4. **Encabezado y pie (Header / Footer):** texto opcional que aparece arriba y abajo
   de cada diapositiva. Aquí puedes insertar **fichas (tokens)** haciendo clic en los
   botones de ficha — **no las escribas a mano**. Las fichas se reemplazan
   automáticamente con la información de cada diapositiva:

   | Ficha | Muestra |
   |---|---|
   | `[SongTitle]` | Título de la canción |
   | `[SongAuthor]` | Autor |
   | `[SongVerseTag]` | "Verse 1", "Chorus", etc. |
   | `[SongCopyright]` | Copyright |
   | `[SongCCLI]` | Número CCLI de la canción |
   | `[BibleReference]` | Referencia, p. ej. "Juan 3:16" |
   | `[BibleBookName]` | Nombre del libro |
   | `[BibleChapterID]` / `[BibleVerseID]` | Número de capítulo / versículo |
   | `[BibleDescription]` | Nombre de la versión bíblica |
   | `[ChurchName]` | Nombre de tu iglesia (de Configuración) |
   | `[SiteLicense]` | CCLI de la iglesia (de Configuración) |

   Si una ficha no aplica a la diapositiva (p. ej. una ficha bíblica en una canción),
   esa zona se oculta sola.
5. Haz clic en **"Save"**. Para que un tema sea el predeterminado, usa **"Set Default"**.

---

## 8. Multimedia (imágenes y videos)

1. Entra a `🖼 Media` y haz clic en **"Import"** para agregar imágenes o videos.
   El archivo se **copia** a la carpeta de la app (no se rompe si mueves el original).
2. Haz clic en **proyectar** para mostrarlo a pantalla completa. Los videos se
   reproducen con audio.
3. Usa **eliminar** para quitarlo de la biblioteca.

> Nota: en la versión 1.0 los videos se reproducen automáticamente; los controles de
> pausa/avance/retroceso del video llegan en la versión 2.0.

---

## 9. Programa del servicio

Arma todo el servicio por adelantado y luego solo avanza el día del culto.

### Crear el programa
1. Entra a `📋 Service Schedule` y haz clic en **"+ New Service"**. Pon nombre y fecha.
2. Agrega elementos con **"Add Song"**, **"Add Bible"** y **"Add Media"**.
3. Reordena con **▲ / ▼**.
4. **Avance automático (`⏱`):** con los botones `[−] [⏱] [+]` defines cada cuántos
   segundos avanza solo (o lo dejas en manual).
5. **Orden por servicio (solo canciones):** en la caja de texto debajo de la canción
   puedes poner un orden distinto solo para ese servicio (p. ej. `V1 C V2`), sin
   cambiar la canción original.

### En vivo
1. Haz clic en **"Start"** (modo en vivo).
2. Haz clic en un elemento de la lista para cargarlo, o usa **"◀ Prev Item"** /
   **"Next Item ▶"**.
3. Dentro de cada elemento avanza las diapositivas con `Espacio` / flechas.

---

## 10. Vista de escenario

`📺 Stage View` es el **monitor del operador**: muestra en grande la diapositiva
**actual** y, al lado, **"UP NEXT"** (lo que sigue) — incluso la primera diapositiva
del **siguiente elemento** del programa cuando llegas al final del actual. Es ideal
para una segunda pantalla del operador o para prepararte sin mirar el proyector.

Durante un servicio en vivo también aparecen los botones **"◀ Prev Item"** /
**"Next Item ▶"**.

---

## 11. Anuncios

Para mostrar un mensaje breve (p. ej. "Bienvenidos") **sin cambiar** la diapositiva
actual:

1. En la barra inferior, escribe el texto en la caja **"Announcement…"**.
2. Haz clic en **"📢 Announce"**. Aparece como una **banda azul** en la parte
   inferior del proyector.
3. Desaparece solo después de unos segundos (configurable), o puedes quitarlo con
   **"Clear"**.

---

## 12. Configuración

En `⚙ Settings` puedes definir:

- **Nombre de la iglesia** → ficha `[ChurchName]`.
- **CCLI de la iglesia** → ficha `[SiteLicense]`.
- **Avance automático predeterminado** (segundos) para elementos nuevos.
- **Versículos por diapositiva** (Biblia).
- **Duración del anuncio** (segundos).
- **Velocidad de transición** entre diapositivas (en milisegundos; 0 = sin animación).

Recuerda **guardar** los cambios.

---

## 13. Atajos de teclado

| Tecla | Acción |
|---|---|
| `Espacio` / `→` / `AvPág` | Siguiente diapositiva |
| `←` / `RePág` | Diapositiva anterior |
| `B` | Pantalla en negro |
| `Esc` | Detener la proyección |
| `1` – `9` | Ir a la diapositiva N |
| `Ctrl + 1` a `Ctrl + 5` | Cambiar de sección (Canciones, Biblia, Programa, Multimedia, Temas) |

---

## 14. Flujo recomendado el día del servicio

1. Abre OpenAdoration y conecta el proyector → **"Open Screen"**.
2. Abre `📋 Service Schedule`, selecciona el servicio del día y haz clic en **"Start"**.
3. (Opcional) Abre `📺 Stage View` en tu monitor para ver lo que sigue.
4. Avanza con `Espacio` / flechas; cambia de elemento con **"Next Item ▶"**.
5. Usa `B` para poner la pantalla en negro entre momentos.
6. Al terminar, `Esc` o **"Stop"**, y **"Close Screen"**.

---

## 15. Solución de problemas

- **No veo nada en el proyector:** verifica que Windows esté en modo "Extender"
  (`Windows + P`) y que hayas pulsado **"Open Screen"**.
- **El proyector muestra el escritorio en vez de la diapositiva:** vuelve a pulsar
  **"Open Screen"** o proyecta un elemento.
- **Una importación falló:** revisa que el archivo sea de un formato compatible. Para
  canciones: OpenLyrics, OpenSong o texto. Para Biblia: los 8 formatos listados.
- **Quiero respaldar mi información:** copia toda la carpeta
  `C:\Usuarios\<tu usuario>\AppData\Local\OpenAdoration\` a una memoria USB.
  Ahí están la base de datos, la multimedia y la configuración.
  *(El respaldo/restauración con un solo archivo llegará en la versión 2.0.)*
- **Registros (logs)** para diagnóstico:
  `C:\Usuarios\<tu usuario>\AppData\Local\OpenAdoration\logs\`

---

¿Dudas o sugerencias? Abre un *issue* en el repositorio del proyecto.
OpenAdoration es software libre bajo licencia MIT.
