using System.Text.Json;

namespace SldBuilder.Core.Settings;

/// <summary>
/// Paths the user worked with last time, kept between AutoCAD sessions in a JSON file.
/// Drawings and templates are separate lists: the "new from template" check box switches between them.
/// </summary>
public sealed class UserSettings
{
    public const int MaxRecent = 10;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<string> RecentExcel { get; set; } = [];
    public List<string> RecentDrawings { get; set; } = [];
    public List<string> RecentTemplates { get; set; } = [];

    /// <summary>Null until the first build: the window then decides by itself.</summary>
    public bool? NewFromTemplate { get; set; }

    /// <summary>A missing or damaged file gives empty settings: losing the history must not stop the command.</summary>
    public static UserSettings Load(string path)
    {
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path)) is { } settings)
            {
                settings.RecentExcel ??= [];
                settings.RecentDrawings ??= [];
                settings.RecentTemplates ??= [];
                return settings;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return new UserSettings();
    }

    /// <summary>Written to a temporary file first, so a failure never leaves a half-written settings file.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Puts <paramref name="path"/> first, removes its older copy and keeps at most <see cref="MaxRecent"/> entries.</summary>
    public static void Push(List<string> list, string path)
    {
        string fullPath = Path.GetFullPath(path);
        list.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, fullPath);
        if (list.Count > MaxRecent)
            list.RemoveRange(MaxRecent, list.Count - MaxRecent);
    }
}
