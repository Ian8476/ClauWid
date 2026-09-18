# Claude Usage Widget

Widget flotante para Windows que muestra el consumo de tu suscripción de Claude
directamente en el escritorio: dos barras, la ventana de 5 horas y la semanal, con los
mismos datos que ves al escribir `/usage` dentro de Claude Code.

Sin pestañas que abrir ni comandos que recordar — el número que importa, siempre a la vista.

## Qué hace

- **Datos reales**, no una estimación: consulta el mismo endpoint que usa Claude Code,
  autenticado con tu sesión ya iniciada.
- **Dos barras**, consumo de 5 horas y consumo semanal, cada una con su porcentaje y su
  hora de reinicio.
- **Alerta visual** cuando una ventana pasa del 90% de uso.
- **Ventana redimensionable**: arrastra un borde o una esquina y el texto, las barras y
  los márgenes escalan juntos. Arrastra desde el centro para moverla.
- **Siempre visible** opcional, y recuerda su posición y tamaño entre sesiones.
- Sin marco, sin barra de título, sin icono en la barra de tareas: es un widget, no una
  ventana más.

## Descargar y ejecutar

1. Ve a [Releases](https://github.com/Ian8476/ClauWid/releases/latest) y descarga
   `ClaudeUsageWidget-win-x64.zip`.
2. Descomprímelo en cualquier carpeta (el `.exe` necesita la carpeta `Assets` a su lado,
   por la tipografía).
3. Ejecuta `ClaudeUsageWidget.exe`.

**Requisitos:**

- Windows 10 o superior.
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) instalado
  (el `.exe` no lo trae empotrado).
- [Claude Code](https://claude.com/claude-code) instalado con sesión iniciada
  (`claude`, luego `/login`). El widget lee el token de ahí; no pide credenciales propias.

## Uso

| Acción | Cómo |
|---|---|
| Mover la ventana | Arrastrar con el botón izquierdo desde cualquier punto de la superficie |
| Cambiar el tamaño | Arrastrar desde cualquier borde o esquina |
| Restablecer el tamaño | Clic derecho → "Restablecer tamaño" |
| Mantenerla siempre visible | Clic derecho → "Siempre visible" |
| Cerrarla | Clic derecho → "Cerrar" |

Posición y tamaño se guardan en `%LOCALAPPDATA%\ClaudeUsageWidget\placement.json` y se
restauran la próxima vez que la abras.

Si ves `--` en vez de un porcentaje, es que el widget no pudo leer tu consumo: revisa que
Claude Code tenga una sesión activa (`claude` en una terminal). El dato no desaparece de
golpe — mientras la fuente esté caída, se queda con la última lectura conocida.

## De dónde salen los datos

El widget lee el token OAuth que Claude Code deja en
`%USERPROFILE%\.claude\.credentials.json` (o en `CLAUDE_CONFIG_DIR`, si lo tienes
definido) y consulta `https://api.anthropic.com/api/oauth/usage` cada 2 minutos.

- **Nunca renueva el token por su cuenta:** hacerlo rotaría el refresh token y cerraría la
  sesión de Claude Code. Cuando lo encuentra caducado, algo habitual si trabajas solo con
  la app de escritorio, ejecuta `claude doctor` en segundo plano, sin ventana y como
  mucho una vez cada 15 minutos: así es el propio CLI de Claude Code quien revisa su sesión
  y la renueva. Ese comando no consume cuota. Mientras tanto, el widget conserva el último
  dato conocido.
- **Nada sale de tu equipo** salvo esa consulta a la API de Anthropic con tu propio token.
  No hay telemetría ni servidor intermedio.
- **Endpoint no documentado**, el mismo que usa Claude Code internamente y que puede
  cambiar sin aviso. Si cambia, el widget muestra `--` en vez de inventar un número.

## Créditos

Montserrat ExtraBold viene empotrada en `src/ClaudeUsageWidget.Presentation.Wpf/Assets/Fonts`
bajo licencia [SIL Open Font License 1.1](src/ClaudeUsageWidget.Presentation.Wpf/Assets/Fonts/OFL.txt).

Este proyecto no está afiliado a Anthropic. "Claude" es una marca de Anthropic PBC.
