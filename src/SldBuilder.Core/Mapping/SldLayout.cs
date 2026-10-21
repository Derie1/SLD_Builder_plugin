namespace SldBuilder.Core.Mapping;

/// <summary>Names of the blocks in the DWG template that the builder inserts (and erases on rebuild).</summary>
public static class BlockNames
{
    public const string Total = "TOTAL";
    public const string Balance = "KNF";
    public const string Incomer = "INCOMER";
    public const string Automat = "AUTOMAT";
    public const string Line = "LINE";

    /// <summary>Blocks that belong to the generated diagram. Everything else in the drawing is left untouched.</summary>
    public static readonly IReadOnlySet<string> Generated =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Total, Balance, Incomer, Automat, Line };
}

/// <summary>Visibility parameters of the dynamic blocks.</summary>
public static class DynamicProperties
{
    public const string BreakerType = "BreakerType";
    public const string IncomerType = "IncomerType";

    /// <summary>Spelled this way in the LINE block of the template.</summary>
    public const string Consumer = "Cunsumer";
}

/// <summary>Named ranges and addresses in the Excel calculation file (sheet "AutoCAD").</summary>
public static class ExcelNames
{
    public const string Sheet = "AutoCAD";

    /// <summary>A1 = panel name, B2:B7 = Pi, K, Pd, cos f, Ib, Isc(3).</summary>
    public const string Total = "TOTAL";

    /// <summary>A9 = insert balance table (Yes/No), B10:D12 = Pd / Ib / unbalance per phase.</summary>
    public const string Balance = "BALANCE";

    /// <summary>A21:A24 = incoming breaker of this board.</summary>
    public const string IncomerBreaker = "X_AUTOM";

    /// <summary>C21:C24 = breaker in the upstream board.</summary>
    public const string UpstreamBreaker = "Y_AUTOM";

    /// <summary>F21:F22 = incoming cable and its length.</summary>
    public const string IncomerCable = "Y_CABLE";

    /// <summary>Additional incomer device. The template has no named range for it.</summary>
    public const string AdditionalDeviceAddress = "AutoCAD!B21:B24";

    /// <summary>Upstream board name. The template has no named range for it.</summary>
    public const string UpstreamBoardAddress = "AutoCAD!D1";

    /// <summary>
    /// Visibility state of the INCOMER block (e.g. SWITCH_3pole), chosen from the list "IncomerType".
    /// Older calculation files have no value there; then <see cref="Defaults.IncomerType"/> is used.
    /// </summary>
    public const string IncomerTypeAddress = "Расчет!U14";

    /// <summary>One column per outgoing group, one row per parameter (see <see cref="GroupRows"/>).</summary>
    public const string Groups = "DB_EXPORT";
}

/// <summary>Row indices (0-based) inside the DB_EXPORT range.</summary>
public static class GroupRows
{
    public const int BreakerType = 0;
    public const int Phase = 16;
    public const int Consumer = 30;
    public const int Count = 31;
}

/// <summary>Attribute tag → DB_EXPORT row index, for the blocks inserted once per group.</summary>
public static class AttributeMap
{
    public static readonly IReadOnlyDictionary<string, int> Automat = new Dictionary<string, int>
    {
        ["PHASE"] = GroupRows.Phase,
        ["AT1_1"] = 1,
        ["AT1_2"] = 2,
        ["AT1_3"] = 3,
        ["AT1_4"] = 4,
        ["AT1_5"] = 5,
        ["AT2_1"] = 8,
        ["AT2_2"] = 9,
        ["AT2_3"] = 10,
        ["AT2_4"] = 11,
    };

    public static readonly IReadOnlyDictionary<string, int> Line =
        Enumerable.Range(1, 13).ToDictionary(i => $"LINE_{i}", i => 16 + i);
}

public static class Defaults
{
    public const string IncomerType = "SWITCH_3pole";

    /// <summary>Horizontal distance between neighbouring groups, drawing units.</summary>
    public const double GroupStepX = 25.0;
}
