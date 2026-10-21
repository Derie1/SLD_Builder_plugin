using System.Globalization;

namespace SldBuilder.Acad;

/// <summary>Finds the template files shipped next to the plugin (Templates folder, also inside the bundle).</summary>
public static class TemplateLocator
{
    public static string Folder { get; } =
        Path.Combine(Path.GetDirectoryName(typeof(TemplateLocator).Assembly.Location)!, "Templates");

    private static bool IsRussian => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";

    public static string DefaultExcel =>
        Path.Combine(Folder, IsRussian ? "DB_имя щита_(как хотите)_v1.5.xlsx" : "DB_NAME_(discription)_v1.5_EN.xlsx");

    public static string DefaultDrawing =>
        Path.Combine(Folder, IsRussian ? "SLD_template.dwg" : "SLD_template_EN.dwg");
}
