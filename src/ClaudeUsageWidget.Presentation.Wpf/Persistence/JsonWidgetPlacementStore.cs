using System.IO;
using System.Text.Json;

namespace ClaudeUsageWidget.Presentation.Wpf.Persistence;

/// <summary>
/// Guarda la posicion de la ventana en LocalAppData. Todo fallo es silencioso a
/// proposito: perder la posicion no justifica impedir que el widget arranque.
/// </summary>
public sealed class JsonWidgetPlacementStore : IWidgetPlacementStore
{
    private const string FolderName = "ClaudeUsageWidget";
    private const string FileName = "placement.json";

    private readonly string _filePath;

    public JsonWidgetPlacementStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName);

        _filePath = Path.Combine(folder, FileName);
    }

    public WidgetPlacement? Load()
    {
        try
        {
            return File.Exists(_filePath)
                ? JsonSerializer.Deserialize<WidgetPlacement>(File.ReadAllText(_filePath))
                : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(WidgetPlacement placement)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(placement));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Sin persistencia de posicion. El widget sigue siendo usable.
        }
    }
}
