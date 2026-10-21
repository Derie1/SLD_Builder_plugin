using System.Globalization;
using ClosedXML.Excel;
using SldBuilder.Core.Mapping;
using SldBuilder.Core.Models;

namespace SldBuilder.Core.Excel;

/// <summary>
/// Reads the "AutoCAD" sheet of the calculation file. Only values cached by Excel are used (no recalculation),
/// so the file must have been saved by Excel.
/// </summary>
public sealed class SldExcelReader
{
    private readonly CultureInfo _culture;
    private readonly List<SldWarning> _warnings = [];

    /// <param name="culture">Decides the decimal separator of numbers written to attributes.</param>
    public SldExcelReader(CultureInfo? culture = null)
    {
        _culture = culture ?? CultureInfo.CurrentCulture;
    }

    public SldData Read(string path)
    {
        if (!File.Exists(path))
            throw new SldDataException(SldErrorKind.FileNotFound, path);

        _warnings.Clear();

        // FileShare.ReadWrite: the calculation file is usually still open in Excel.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);

        return new SldData(
            ReadTotals(workbook),
            ReadBalance(workbook),
            ReadIncomer(workbook),
            ReadGroups(workbook),
            _warnings.ToList());
    }

    private PanelTotals ReadTotals(XLWorkbook workbook)
    {
        var range = GetRange(workbook, ExcelNames.Total);
        return new PanelTotals(
            PanelName: Text(range.Cell(1, 1)),
            InstalledPower: Text(range.Cell(2, 2)),
            DemandFactor: Text(range.Cell(3, 2)),
            DemandPower: Text(range.Cell(4, 2)),
            PowerFactor: Text(range.Cell(5, 2)),
            Current: Text(range.Cell(6, 2)),
            ShortCircuitCurrent: Text(range.Cell(7, 2)));
    }

    private PhaseBalance ReadBalance(XLWorkbook workbook)
    {
        var range = GetRange(workbook, ExcelNames.Balance);
        var phases = Enumerable.Range(2, 3)
            .Select(row => new PhaseLoad(
                Power: Text(range.Cell(row, 2)),
                Current: Text(range.Cell(row, 3)),
                Unbalance: Text(range.Cell(row, 4))))
            .ToList();
        return new PhaseBalance(IsYes(range.Cell(1, 1).CachedValue), phases);
    }

    private IncomerData ReadIncomer(XLWorkbook workbook)
    {
        var cable = GetRange(workbook, ExcelNames.IncomerCable);

        string incomerType = Defaults.IncomerType;
        var typeRange = TryGetRange(workbook, ExcelNames.IncomerTypeAddress);
        if (typeRange is not null && Text(typeRange.FirstCell()).Trim() is { Length: > 0 } fromExcel)
            incomerType = fromExcel;
        else
            _warnings.Add(new SldWarning(SldWarningKind.IncomerTypeMissing, ExcelNames.IncomerTypeAddress));

        return new IncomerData(
            IncomerType: incomerType,
            Breaker: Column(GetRange(workbook, ExcelNames.IncomerBreaker)),
            UpstreamBreaker: Column(GetRange(workbook, ExcelNames.UpstreamBreaker)),
            Cable: Text(cable.Cell(1, 1)),
            CableLength: Text(cable.Cell(2, 1)),
            AdditionalDevice: Column(GetRange(workbook, ExcelNames.AdditionalDeviceAddress)),
            UpstreamBoardName: Text(GetRange(workbook, ExcelNames.UpstreamBoardAddress).FirstCell()));
    }

    private List<GroupData> ReadGroups(XLWorkbook workbook)
    {
        var range = GetRange(workbook, ExcelNames.Groups);
        if (range.RowCount() < GroupRows.Count)
            throw new SldDataException(SldErrorKind.GroupRangeTooShort, range.RangeAddress.ToString()!);

        // Same as the Python version: every column of the range is drawn, empty ones included.
        return Enumerable.Range(1, range.ColumnCount())
            .Select(col => new GroupData(
                Index: col,
                Rows: Enumerable.Range(1, GroupRows.Count).Select(row => Text(range.Cell(row, col))).ToList()))
            .ToList();
    }

    private List<string> Column(IXLRange range) =>
        Enumerable.Range(1, range.RowCount()).Select(row => Text(range.Cell(row, 1))).ToList();

    private string Text(IXLCell cell)
    {
        var value = cell.CachedValue;
        if (value.IsError)
            _warnings.Add(new SldWarning(SldWarningKind.ExcelError, $"{cell.Worksheet.Name}!{cell.Address}"));
        return CellFormatter.Format(value, _culture);
    }

    private static bool IsYes(XLCellValue value)
    {
        if (value.IsBoolean)
            return value.GetBoolean();
        if (!value.IsText)
            return false;
        string text = value.GetText().Trim();
        return text.Equals("да", StringComparison.OrdinalIgnoreCase)
            || text.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static IXLRange GetRange(XLWorkbook workbook, string nameOrAddress) =>
        TryGetRange(workbook, nameOrAddress)
        ?? throw new SldDataException(SldErrorKind.NamedRangeMissing, nameOrAddress);

    /// <summary>Accepts a workbook-level name ("DB_EXPORT") or a sheet address ("AutoCAD!B21:B24").</summary>
    private static IXLRange? TryGetRange(XLWorkbook workbook, string nameOrAddress)
    {
        if (!nameOrAddress.Contains('!'))
        {
            return workbook.NamedRanges.TryGetValue(nameOrAddress, out var named)
                ? named.Ranges.FirstOrDefault()
                : null;
        }

        string[] parts = nameOrAddress.Split('!', 2);
        return workbook.TryGetWorksheet(parts[0], out var sheet) ? sheet.Range(parts[1]) : null;
    }
}
