# Claude Usage Widget - Fase 2 (datos reales)

Widget flotante para Windows que muestra el consumo de tu suscripcion de Claude en dos
barras: la ventana de 5 horas y la semanal. Lee los mismos datos que `/usage` dentro de
Claude Code.

## Abrir

1. Abrir `ClaudeUsageWidget.sln` en Visual Studio 2022 o posterior.
2. Marcar `ClaudeUsageWidget.Presentation.Wpf` como proyecto de inicio.
3. F5.

Requiere el SDK de .NET 8 o superior con la carga de trabajo ".NET Desktop Development"
instalada desde el Visual Studio Installer.

El target framework esta centralizado en `Directory.Build.props`, en la raiz. Para
subir a .NET 10 basta cambiar ahi las dos lineas, no los cuatro `.csproj`.

Si al pulsar F5 aparece "el archivo ejecutable de depuracion ... no existe", la
compilacion fallo y Visual Studio ofrecio ejecutar la ultima version correcta, que no
existe todavia. El error real esta en la ventana Lista de errores (Ctrl+\, E) o en
Salida con el desplegable en "Compilar". Compila con Ctrl+Shift+B antes de F5 para
verlo sin el dialogo de por medio.

## De donde salen los datos

Requiere Claude Code con la sesion iniciada con tu cuenta de Claude (`claude`, luego
`/login`). El widget lee el token OAuth de `%USERPROFILE%\.claude\.credentials.json`
(o de `CLAUDE_CONFIG_DIR` si lo tienes definido) y consulta
`https://api.anthropic.com/api/oauth/usage` cada 2 minutos.

- **Solo lectura.** El widget nunca renueva el token: hacerlo rotaria el refresh token
  y cerraria la sesion de Claude Code. Si el token caduco porque no usas Claude Code desde
  hace horas, el widget conserva el ultimo dato hasta que Claude Code lo renueve.
- **Endpoint no documentado.** Es el que usa Claude Code internamente y puede cambiar.
  Si cambia, el widget muestra `--` en lugar de inventar un numero.
- **Diagnostico.** Si ves `--`, ejecuta con F5 y mira la ventana Salida (Depurar): cada
  fallo deja una linea que empieza por `Claude usage:`.

Arrastra con el boton izquierdo en cualquier punto para moverlo, o desde cualquier borde
o esquina para cambiar su tamano. El alto decide el zoom (texto, barras y margenes crecen
juntos) y el ancho sobrante alarga las barras; si la ventana es demasiado estrecha para ese
zoom, el contenido se queda centrado. Clic derecho abre el menu con
"Siempre visible", "Restablecer tamaño" y "Cerrar". Posicion y tamano se guardan en
`%LOCALAPPDATA%\ClaudeUsageWidget\placement.json`.

## Estructura

    src/
      ClaudeUsageWidget.Domain           Reglas y tipos. Sin dependencias.
      ClaudeUsageWidget.Application      Puertos y formateadores de etiqueta.
      ClaudeUsageWidget.Infrastructure   Reloj real, proveedor de Claude Code y simulador.
      ClaudeUsageWidget.Presentation.Wpf Ventana, control de barra, ViewModels.

La direccion de dependencia apunta siempre hacia Domain. La UI habla con
`IUsageSnapshotProvider`, nunca con una fuente concreta.

## Donde se cambian las cosas

| Quiero cambiar | Archivo |
|---|---|
| Colores, tamanos, tipografia, espaciados | `Presentation.Wpf/Theme/DesignTokens.xaml` |
| El umbral de alerta | `Domain/UsageAlertPolicy.cs` |
| El formato `3:20h` | `Application/Formatting/CountdownResetLabelFormatter.cs` |
| El formato `Sun 5:00 PM` | `Application/Formatting/WeekdayResetLabelFormatter.cs` |
| La fuente de datos o la frecuencia de consulta | `Presentation.Wpf/Composition/WidgetCompositionRoot.cs` |
| Lectura del token | `Infrastructure/ClaudeCode/ClaudeCodeCredentialsReader.cs` |
| Llamada y traduccion de la respuesta | `Infrastructure/ClaudeCode/ClaudeOAuthUsageProvider.cs` |
| Zoom minimo y maximo, ancho minimo al redimensionar | `Presentation.Wpf/Windows/MainWindow.xaml.cs` |
| Grosor de la franja de redimensionado | `Presentation.Wpf/Theme/DesignTokens.xaml` |

Para iterar el diseno sin datos reales, sustituye en `WidgetCompositionRoot` el
proveedor por `new SimulatedUsageProvider(clock, new SimulatedUsageOptions())`: arranca
en 38% y 82% y sube cada lectura para ver la animacion, la alerta y los reinicios.

## Nota sobre el nombre "Application"

La capa `ClaudeUsageWidget.Application` choca con `System.Windows.Application`: dentro
de cualquier namespace que cuelgue de `ClaudeUsageWidget`, el identificador suelto
`Application` resuelve al espacio de nombres y el compilador responde CS0118.

Un `global using Application = ...` no lo arregla: los alias solo se consultan despues
de los espacios de nombres que contienen el codigo. Por eso `App.xaml.cs` escribe
`System.Windows.Application` completo. Si prefieres eliminar el choque de raiz, renombra
la capa a `ClaudeUsageWidget.UseCases` o `ClaudeUsageWidget.Core`.

## Tipografia

Montserrat ExtraBold viene empotrada en `Assets/Fonts` bajo licencia SIL OFL 1.1
(`OFL.txt` incluido). El nombre de familia del archivo estatico es
"Montserrat ExtraBold" con subfamilia "Regular", por eso los `TextBlock` usan
`FontWeight="Normal"`: poner `Bold` aplicaria un falso negrita sintetico encima de
una fuente que ya es pesada, y se ve emborronado.
