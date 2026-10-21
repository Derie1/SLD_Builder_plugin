using System.Windows;
using Microsoft.Win32;
using SldBuilder.Acad.Resources;

namespace SldBuilder.Acad.UI;

/// <param name="CreateFromTemplate">True: a new drawing is created from <paramref name="DwgPath"/>; the file itself is not modified.</param>
public sealed record BuildRequest(string ExcelPath, string DwgPath, bool CreateFromTemplate);

public partial class MainWindow : Window
{
    public BuildRequest? Request { get; private set; }

    public MainWindow(string excelPath, string dwgPath)
    {
        InitializeComponent();
        ExcelPath.Text = excelPath;
        DwgPath.Text = dwgPath;
    }

    private void OnBrowseExcel(object sender, RoutedEventArgs e) =>
        Browse(ExcelPath, Strings.BrowseExcelTitle, Strings.ExcelFilter);

    private void OnBrowseDwg(object sender, RoutedEventArgs e) =>
        Browse(DwgPath, Strings.BrowseDwgTitle, Strings.DwgFilter);

    private void OnNewFromTemplateChanged(object sender, RoutedEventArgs e)
    {
        // The field stays editable so that another template can be chosen.
        if (NewFromTemplate.IsChecked == true)
            DwgPath.Text = TemplateLocator.DefaultDrawing;
    }

    private void OnBuild(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(ExcelPath.Text))
        {
            MessageBox.Show(this, Strings.ValidateExcelPath, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!File.Exists(DwgPath.Text))
        {
            MessageBox.Show(this, Strings.ValidateDwgPath, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Request = new BuildRequest(ExcelPath.Text, DwgPath.Text, NewFromTemplate.IsChecked == true);
        DialogResult = true;
    }

    private void Browse(System.Windows.Controls.TextBox target, string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter };
        if (File.Exists(target.Text))
            dialog.InitialDirectory = Path.GetDirectoryName(target.Text);
        if (dialog.ShowDialog(this) == true)
            target.Text = dialog.FileName;
    }
}
