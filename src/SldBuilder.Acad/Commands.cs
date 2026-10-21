using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using SldBuilder.Acad.Drawing;
using SldBuilder.Acad.Resources;
using SldBuilder.Acad.UI;
using SldBuilder.Core.Excel;
using SldBuilder.Core.Models;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcUiApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(SldBuilder.Acad.Commands))]

namespace SldBuilder.Acad;

public class Commands
{
    /// <summary>
    /// Session context: the command may open or create another drawing and make it current.
    /// </summary>
    [CommandMethod("SLDBUILDER", "SLDBUILD", CommandFlags.Session)]
    public void BuildSld()
    {
        var window = new MainWindow(TemplateLocator.DefaultExcel, TemplateLocator.DefaultDrawing);
        if (AcUiApp.ShowModalWindow(window) != true || window.Request is not { } request)
            return;

        SldData data;
        try
        {
            data = new SldExcelReader().Read(request.ExcelPath);
        }
        catch (SldDataException ex)
        {
            AcApp.ShowAlertDialog(Describe(ex));
            return;
        }

        try
        {
            var document = OpenTarget(request);
            DrawResult result;
            using (document.LockDocument())
                result = new SldDrawer(document.Database).Draw(data);

            Report(document, data, result);
        }
        catch (MissingBlocksException ex)
        {
            AcApp.ShowAlertDialog(string.Format(Strings.ErrMissingBlocks, string.Join(", ", ex.Blocks)));
        }
        catch (System.Exception ex)
        {
            AcApp.ShowAlertDialog(string.Format(Strings.ErrUnexpected, ex.Message));
        }
    }

    /// <summary>
    /// A template is never opened for editing: a new drawing is created from it.
    /// An existing diagram is reused if it is already open.
    /// </summary>
    private static Document OpenTarget(BuildRequest request)
    {
        var documents = AcApp.DocumentManager;

        Document document = request.CreateFromTemplate
            ? documents.Add(request.DwgPath)
            : documents.Cast<Document>().FirstOrDefault(d =>
                  string.Equals(Path.GetFullPath(d.Name), Path.GetFullPath(request.DwgPath), StringComparison.OrdinalIgnoreCase))
              ?? documents.Open(request.DwgPath, false);

        documents.MdiActiveDocument = document;
        return document;
    }

    private static void Report(Document document, SldData data, DrawResult result)
    {
        var editor = document.Editor;
        foreach (var warning in data.Warnings)
            editor.WriteMessage("\n" + Describe(warning));
        foreach (var warning in result.Warnings)
            editor.WriteMessage("\n" + Describe(warning));

        int warnings = data.Warnings.Count + result.Warnings.Count;
        editor.WriteMessage("\n" + string.Format(Strings.Done, result.GroupsDrawn, result.Erased, warnings) + "\n");
        editor.Regen();
    }

    private static string Describe(SldDataException ex) => ex.Kind switch
    {
        SldErrorKind.FileNotFound => string.Format(Strings.ErrFileNotFound, ex.Subject),
        SldErrorKind.NamedRangeMissing => string.Format(Strings.ErrNamedRangeMissing, ex.Subject),
        SldErrorKind.GroupRangeTooShort => string.Format(Strings.ErrGroupRangeTooShort, ex.Subject),
        _ => ex.Message,
    };

    private static string Describe(SldWarning warning) => warning.Kind switch
    {
        SldWarningKind.ExcelError => string.Format(Strings.WarnExcelError, warning.Subject),
        SldWarningKind.IncomerTypeMissing =>
            string.Format(Strings.WarnIncomerTypeMissing, warning.Subject, Core.Mapping.Defaults.IncomerType),
        _ => warning.ToString(),
    };

    private static string Describe(DrawWarning warning) => warning.Group is { } group
        ? string.Format(Strings.WarnVisibilityGroup, warning.Block, warning.Property, warning.Value, group)
        : string.Format(Strings.WarnVisibility, warning.Block, warning.Property, warning.Value);
}
