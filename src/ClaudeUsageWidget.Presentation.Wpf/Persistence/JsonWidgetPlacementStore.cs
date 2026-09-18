using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeUsageWidget.Presentation.Wpf.Persistence;

/// <summary>
/// Guarda en LocalAppData una posicion por cada combinacion de monitores: mover el widget
/// con dos pantallas no pisa la posicion que tenia con una sola, y abrirlo con un monitor
/// menos no lo deja en coordenadas que ya no existen. Todo fallo es silencioso a
/// proposito: perder la posicion no justifica impedir que el widget arranque.
/// </summary>
public sealed class JsonWidgetPlacementStore : IWidgetPlacementStore
{
    private const string FolderName = "ClaudeUsageWidget";
    private const string FileName = "placement.json";

    // Legible porque es un archivo que el usuario puede querer leer o editar a mano: indentado,
    // y sin escapar el "+" de las claves, que el codificador por defecto vuelve "\u002B".
    // Escapar solo protege al incrustar JSON en HTML, y este archivo nunca sale del equipo.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;

    public JsonWidgetPlacementStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName);

        _filePath = Path.Combine(folder, FileName);
    }

    public WidgetPlacement? Load(string layoutKey) =>
        ReadLayouts().GetValueOrDefault(layoutKey);

    /// <summary>
    /// Lee, modifica y reescribe el archivo entero. Es un guardado por gesto, y asi se
    /// conservan los casos de monitores que no estan conectados ahora.
    /// </summary>
    public void Save(string layoutKey, WidgetPlacement placement)
    {
        var layouts = ReadLayouts();
        layouts[layoutKey] = placement;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(
                _filePath,
                JsonSerializer.Serialize(new WidgetPlacementFile { Layouts = layouts }, SerializerOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Sin persistencia de posicion. El widget sigue siendo usable.
        }
    }

    /// <summary>
    /// Un placement.json de la version anterior, con una sola posicion y sin casos, se lee
    /// como vacio: no dice para que monitores era, y puede ser justo la que quedo fuera de pantalla.
    /// </summary>
    private Dictionary<string, WidgetPlacement> ReadLayouts()
    {
        try
        {
            if (File.Exists(_filePath)
                && JsonSerializer.Deserialize<WidgetPlacementFile>(File.ReadAllText(_filePath)) is { Layouts: { } layouts })
            {
                return layouts;
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Archivo ilegible: se empieza sin posiciones guardadas.
        }

        return new Dictionary<string, WidgetPlacement>();
    }
}
