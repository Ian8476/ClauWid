# Claude Usage Widget - Fase 1 (solo UI)

Widget flotante para Windows que replica el mockup de dos barras. Esta fase no toca
ninguna fuente real: corre contra un proveedor simulado para poder iterar el diseno
sin depender de Claude Code ni de la app de escritorio.

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

## Que vas a ver

El simulador arranca en los valores del mockup (38% y 82%), sube el consumo cada
20 segundos y reinicia la ventana de 5 horas al llegar al tope. Sirve para revisar
sin esperar: la animacion del relleno, el estado de alerta a partir del 90%, el
prefijo `~` de un reinicio estimado y el texto `--` cuando no hay dato.

Arrastra con el boton izquierdo en cualquier punto. Clic derecho abre el menu con
"Siempre visible" y "Cerrar". La posicion se guarda en
`%LOCALAPPDATA%\ClaudeUsageWidget\placement.json`.

## Estructura

    src/
      ClaudeUsageWidget.Domain           Reglas y tipos. Sin dependencias.
      ClaudeUsageWidget.Application      Puertos y formateadores de etiqueta.
      ClaudeUsageWidget.Infrastructure   Reloj real y proveedor simulado.
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
| La fuente de datos | `Presentation.Wpf/Composition/WidgetCompositionRoot.cs`, una linea |

## Nota sobre el nombre "Application"

La capa `ClaudeUsageWidget.Application` choca con `System.Windows.Application`: dentro
de cualquier namespace que cuelgue de `ClaudeUsageWidget`, el identificador suelto
`Application` resuelve al espacio de nombres y el compilador responde CS0118.

El proyecto de presentacion lo resuelve con un alias global en `GlobalUsings.cs`.
Si prefieres eliminar el choque de raiz en vez de aliasarlo, renombra la capa a
`ClaudeUsageWidget.UseCases` o `ClaudeUsageWidget.Core` y borra ese archivo.

## Tipografia

Montserrat ExtraBold viene empotrada en `Assets/Fonts` bajo licencia SIL OFL 1.1
(`OFL.txt` incluido). El nombre de familia del archivo estatico es
"Montserrat ExtraBold" con subfamilia "Regular", por eso los `TextBlock` usan
`FontWeight="Normal"`: poner `Bold` aplicaria un falso negrita sintetico encima de
una fuente que ya es pesada, y se ve emborronado.

## Siguiente fase

Sustituir `SimulatedUsageProvider` por lectores reales. Nada fuera de
`WidgetCompositionRoot` deberia cambiar.
