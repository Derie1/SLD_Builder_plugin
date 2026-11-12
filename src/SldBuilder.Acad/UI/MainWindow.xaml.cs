using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SldBuilder.Acad.Resources;
using SldBuilder.Core.Settings;

namespace SldBuilder.Acad.UI;

/// <param name="CreateFromTemplate">True: a new drawing is created from <paramref name="DwgPath"/>; the file itself is not modified.</param>
public sealed record BuildRequest(string ExcelPath, string DwgPath, bool CreateFromTemplate);

public partial class MainWindow : Window
{
    private readonly UserSettings _settings;
    private readonly List<string> _excelFiles;
    private readonly List<string> _drawings;
    private readonly List<string> _templates;

    public BuildRequest? Request { get; private set; }

    /// <summary>
    /// The fields start with the last used files; files that no longer exist are not offered.
    /// On the first run the templates shipped with the plugin are suggested.
    /// </summary>
    public MainWindow(UserSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        _excelFiles = Existing(settings.RecentExcel);
        _drawings = Existing(settings.RecentDrawings);
        _templates = Existing(settings.RecentTemplates);
        if (!_templates.Contains(TemplateLocator.DefaultDrawing, StringComparer.OrdinalIgnoreCase))
            _templates.Add(TemplateLocator.DefaultDrawing);

        ExcelPath.ItemsSource = _excelFiles;
        ExcelPath.Text = _excelFiles.FirstOrDefault() ?? TemplateLocator.DefaultExcel;

        // Without a history a template is suggested, so the box must be checked: otherwise the template itself would be edited.
        // Setting the box raises Checked only on a change, so the list is filled explicitly as well.
        NewFromTemplate.IsChecked = settings.NewFromTemplate ?? true;
        ShowDwgList();
    }

    private static List<string> Existing(IEnumerable<string> paths) => paths.Where(File.Exists).ToList();

    private void OnBrowseExcel(object sender, RoutedEventArgs e) =>
        Browse(ExcelPath, _excelFiles, Strings.BrowseExcelTitle, Strings.ExcelFilter);

    private void OnBrowseDwg(object sender, RoutedEventArgs e) =>
        Browse(DwgPath, NewFromTemplate.IsChecked == true ? _templates : _drawings, Strings.BrowseDwgTitle, Strings.DwgFilter);

    // The field stays editable so that another template or drawing can be chosen.
    private void OnNewFromTemplateChanged(object sender, RoutedEventArgs e) => ShowDwgList();

    /// <summary>The check box decides which list the drawing field offers: templates or built diagrams.</summary>
    private void ShowDwgList()
    {
        var list = NewFromTemplate.IsChecked == true ? _templates : _drawings;
        DwgPath.ItemsSource = list;
        DwgPath.Text = list.FirstOrDefault() ?? string.Empty;
    }

    private void OnBuild(object sender, RoutedEventArgs e)
    {
        string excelPath = ExcelPath.Text.Trim();
        string dwgPath = DwgPath.Text.Trim();

        if (!File.Exists(excelPath))
        {
            MessageBox.Show(this, Strings.ValidateExcelPath, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!File.Exists(dwgPath))
        {
            MessageBox.Show(this, Strings.ValidateDwgPath, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool fromTemplate = NewFromTemplate.IsChecked == true;
        UserSettings.Push(_settings.RecentExcel, excelPath);
        UserSettings.Push(fromTemplate ? _settings.RecentTemplates : _settings.RecentDrawings, dwgPath);
        _settings.NewFromTemplate = fromTemplate;

        Request = new BuildRequest(excelPath, dwgPath, fromTemplate);
        DialogResult = true;
    }

    /// <summary>The dialog opens in the folder of the current path, or of the last used file if that folder is gone.</summary>
    private void Browse(ComboBox target, List<string> recent, string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter };
        string? folder = new[] { target.Text.Trim() }.Concat(recent)
            .Select(SafeDirectoryName)
            .FirstOrDefault(Directory.Exists);
        if (folder is not null)
            dialog.InitialDirectory = folder;
        if (dialog.ShowDialog(this) == true)
            target.Text = dialog.FileName;
    }

    private static string? SafeDirectoryName(string path)
    {
        try
        {
            return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
