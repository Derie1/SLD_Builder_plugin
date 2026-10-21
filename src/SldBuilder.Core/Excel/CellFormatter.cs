using System.Globalization;
using ClosedXML.Excel;

namespace SldBuilder.Core.Excel;

/// <summary>Turns cached Excel values into attribute text the same way the Python version did.</summary>
public static class CellFormatter
{
    /// <summary>
    /// Numbers are rounded to 2 decimals and written without trailing zeros (5.0 → "5", 0.531 → "0.53").
    /// Blank cells and Excel errors become an empty string.
    /// </summary>
    public static string Format(XLCellValue value, CultureInfo culture) => value.Type switch
    {
        XLDataType.Blank => string.Empty,
        XLDataType.Text => value.GetText(),
        XLDataType.Number => FormatNumber(value.GetNumber(), culture),
        XLDataType.Boolean => value.GetBoolean().ToString(culture),
        XLDataType.DateTime => value.GetDateTime().ToString("d", culture),
        XLDataType.TimeSpan => value.GetTimeSpan().ToString("c", culture),
        _ => string.Empty, // XLDataType.Error
    };

    public static string FormatNumber(double number, CultureInfo culture)
    {
        double rounded = Math.Round(number, 2, MidpointRounding.ToEven);
        if (rounded == 0)
            rounded = 0; // avoid "-0"
        return rounded.ToString("0.##", culture);
    }
}
