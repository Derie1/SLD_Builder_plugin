using System.Globalization;
using SldBuilder.Core.Excel;
using SldBuilder.Core.Models;

namespace SldBuilder.Core.Tests;

public class SldExcelReaderTests
{
    private const string EnTemplate = "DB_NAME_(discription)_v1.5_EN.xlsx";
    private const string RuTemplate = "DB_имя щита_(как хотите)_v1.5.xlsx";

    private static SldData Read(string fileName) =>
        new SldExcelReader(CultureInfo.InvariantCulture).Read(Path.Combine(AppContext.BaseDirectory, "TestData", fileName));

    [Fact]
    public void Reads_totals()
    {
        var totals = Read(EnTemplate).Totals;

        Assert.Equal("DB-HD1", totals.PanelName);
        Assert.Equal("5.8", totals.InstalledPower);
        Assert.Equal("0.53", totals.DemandFactor);
        Assert.Equal("4.79", totals.Current);
        Assert.Equal("5.38", totals.ShortCircuitCurrent);
    }

    [Fact]
    public void Reads_balance()
    {
        var balance = Read(EnTemplate).Balance;

        Assert.True(balance.IsRequired);
        Assert.Equal(new PhaseLoad("1.67", "7.8", "27.86"), balance.Phases[0]);
        Assert.Equal("0", balance.Phases[2].Unbalance);
        Assert.Equal("27.86", balance.ToAttributes()["L1_KNF"]);
    }

    [Fact]
    public void Reads_incomer()
    {
        var data = Read(EnTemplate);
        var attributes = data.Incomer.ToAttributes();

        Assert.Equal("SWITCH_3pole", data.Incomer.IncomerType);
        Assert.DoesNotContain(data.Warnings, w => w.Kind == SldWarningKind.IncomerTypeMissing);
        Assert.Equal("QS", attributes["DB-CB_1"]);
        Assert.Equal("C40A", attributes["DB-CB_3"]);
        Assert.Equal("NSX100N", attributes["MDB-CB_2"]);
        Assert.Equal("100", attributes["MDB-CB_3"]);
        Assert.Equal("ППГнг(А)-HF 5x4", attributes["CABLE"]);
        Assert.Equal("5м", attributes["CABLE_LENGHT"]);
        Assert.Equal("SDB1", attributes["MDB_NAME"]);
        Assert.Equal("", attributes["ADD_1"]);
    }

    [Theory]
    [InlineData(EnTemplate)]
    [InlineData(RuTemplate)]
    public void Incomer_type_comes_from_cell_U14(string template)
    {
        string path = CopyWithIncomerType(template, "CB_3pole+Wh");
        try
        {
            Assert.Equal("CB_3pole+Wh", new SldExcelReader().Read(path).Incomer.IncomerType);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Empty_incomer_type_falls_back_to_default_with_warning()
    {
        string path = CopyWithIncomerType(EnTemplate, null);

        var data = new SldExcelReader().Read(path);
        File.Delete(path);

        Assert.Equal("SWITCH_3pole", data.Incomer.IncomerType);
        Assert.Contains(data.Warnings, w => w.Kind == SldWarningKind.IncomerTypeMissing && w.Subject == "Расчет!U14");
    }

    private static string CopyWithIncomerType(string template, string? value)
    {
        string path = Path.Combine(Path.GetTempPath(), $"sld_{Guid.NewGuid():N}.xlsx");
        using (var workbook = new ClosedXML.Excel.XLWorkbook(Path.Combine(AppContext.BaseDirectory, "TestData", template)))
        {
            var cell = workbook.Worksheet("Расчет").Cell("U14");
            if (value is null)
                cell.Clear();
            else
                cell.Value = value;
            workbook.SaveAs(path);
        }
        return path;
    }

    [Fact]
    public void Reads_groups()
    {
        var groups = Read(EnTemplate).Groups;

        Assert.Equal(12, groups.Count);

        var first = groups[0];
        Assert.Equal("DIFF_2pole+KM", first.BreakerType);
        Assert.Equal("ЩР", first.ConsumerType);

        var automat = first.ToAutomatAttributes();
        Assert.Equal("L1", automat["PHASE"]);
        Assert.Equal("QFD1", automat["AT1_1"]);
        Assert.Equal("30мА", automat["AT1_4"]);
        Assert.Equal("KM1", automat["AT2_1"]);
        Assert.Equal("220В", automat["AT2_4"]);

        var line = first.ToLineAttributes();
        Assert.Equal(13, line.Count);
        Assert.Equal("N1", line["LINE_1"]);
        Assert.Equal("0.07", line["LINE_2"]);
        Assert.Equal("Convector", line["LINE_12"]);

        Assert.Equal("DIFF_2pole", groups[2].BreakerType);
        Assert.Equal("L3", groups[2].ToAutomatAttributes()["PHASE"]);
    }

    [Fact]
    public void Reads_russian_template()
    {
        var data = Read(RuTemplate);

        Assert.Equal(12, data.Groups.Count);
        Assert.NotEmpty(data.Totals.PanelName);
    }

    [Fact]
    public void Missing_file_is_reported()
    {
        var error = Assert.Throws<SldDataException>(() => new SldExcelReader().Read("no_such_file.xlsx"));
        Assert.Equal(SldErrorKind.FileNotFound, error.Kind);
    }
}
