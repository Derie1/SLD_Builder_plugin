using SldBuilder.Core.Settings;

namespace SldBuilder.Core.Tests;

public sealed class UserSettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SldBuilderTests_" + Guid.NewGuid().ToString("N"));

    private string SettingsFile => Path.Combine(_folder, "sub", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Push_puts_the_path_first_without_duplicates()
    {
        var list = new List<string> { @"C:\a.xlsx", @"C:\b.xlsx" };

        UserSettings.Push(list, @"c:\B.XLSX");

        Assert.Equal([@"c:\B.XLSX", @"C:\a.xlsx"], list);
    }

    [Fact]
    public void Push_keeps_only_the_last_ten()
    {
        var list = new List<string>();
        for (int i = 1; i <= 12; i++)
            UserSettings.Push(list, $@"C:\file{i}.dwg");

        Assert.Equal(UserSettings.MaxRecent, list.Count);
        Assert.Equal(@"C:\file12.dwg", list[0]);
        Assert.Equal(@"C:\file3.dwg", list[^1]);
    }

    [Fact]
    public void Push_stores_full_paths() =>
        Assert.Equal(Path.GetFullPath("rel.xlsx"), Push(new List<string>(), "rel.xlsx")[0]);

    [Fact]
    public void Missing_file_gives_empty_settings()
    {
        var settings = UserSettings.Load(SettingsFile);

        Assert.Empty(settings.RecentExcel);
        Assert.Empty(settings.RecentDrawings);
        Assert.Empty(settings.RecentTemplates);
        Assert.Null(settings.NewFromTemplate);
    }

    [Fact]
    public void Damaged_file_gives_empty_settings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, "{ not json");

        Assert.Empty(UserSettings.Load(SettingsFile).RecentExcel);
    }

    [Fact]
    public void Null_lists_in_the_file_become_empty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, """{ "RecentExcel": null }""");

        Assert.Empty(UserSettings.Load(SettingsFile).RecentExcel);
    }

    [Fact]
    public void Save_and_load_round_trip_with_cyrillic_paths()
    {
        var settings = new UserSettings { NewFromTemplate = false };
        UserSettings.Push(settings.RecentExcel, @"C:\Проекты\DB_имя щита_(как хотите)_v1.5.xlsx");
        UserSettings.Push(settings.RecentDrawings, @"C:\Проекты\ЩР-1.dwg");
        UserSettings.Push(settings.RecentTemplates, @"C:\Templates\SLD_template.dwg");

        settings.Save(SettingsFile);
        var loaded = UserSettings.Load(SettingsFile);

        Assert.Equal(settings.RecentExcel, loaded.RecentExcel);
        Assert.Equal(settings.RecentDrawings, loaded.RecentDrawings);
        Assert.Equal(settings.RecentTemplates, loaded.RecentTemplates);
        Assert.False(loaded.NewFromTemplate);
        Assert.False(File.Exists(SettingsFile + ".tmp"));
    }

    private static List<string> Push(List<string> list, string path)
    {
        UserSettings.Push(list, path);
        return list;
    }
}
