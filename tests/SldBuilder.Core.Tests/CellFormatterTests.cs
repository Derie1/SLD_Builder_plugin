using System.Globalization;
using SldBuilder.Core.Excel;

namespace SldBuilder.Core.Tests;

public class CellFormatterTests
{
    [Theory]
    [InlineData(5.0, "5")]
    [InlineData(5.802, "5.8")]
    [InlineData(0.53192, "0.53")]
    [InlineData(0.068, "0.07")]
    [InlineData(-0.001, "0")]
    [InlineData(100, "100")]
    public void Rounds_to_two_decimals_without_trailing_zeros(double input, string expected) =>
        Assert.Equal(expected, CellFormatter.FormatNumber(input, CultureInfo.InvariantCulture));

    [Fact]
    public void Uses_culture_decimal_separator() =>
        Assert.Equal("0,95", CellFormatter.FormatNumber(0.95, CultureInfo.GetCultureInfo("ru-RU")));
}
