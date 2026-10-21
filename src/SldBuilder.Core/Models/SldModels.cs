using SldBuilder.Core.Mapping;

namespace SldBuilder.Core.Models;

/// <summary>Everything read from the Excel calculation file that is needed to draw one board.</summary>
public sealed record SldData(
    PanelTotals Totals,
    PhaseBalance Balance,
    IncomerData Incomer,
    IReadOnlyList<GroupData> Groups,
    IReadOnlyList<SldWarning> Warnings);

public enum SldWarningKind
{
    /// <summary>A cell contains an Excel error (#N/A, #REF!, ...); written as empty text.</summary>
    ExcelError,

    /// <summary>The incomer type cell is missing or empty (older calculation file); the default incomer type is used.</summary>
    IncomerTypeMissing,
}

/// <param name="Subject">Cell address or range name the warning refers to.</param>
public sealed record SldWarning(SldWarningKind Kind, string Subject);

public enum SldErrorKind
{
    FileNotFound,
    NamedRangeMissing,
    GroupRangeTooShort,
}

/// <summary>The Excel file cannot be used to build a diagram.</summary>
public sealed class SldDataException(SldErrorKind kind, string subject)
    : Exception($"{kind}: {subject}")
{
    public SldErrorKind Kind { get; } = kind;
    public string Subject { get; } = subject;
}

/// <summary>Block TOTAL.</summary>
public sealed record PanelTotals(
    string PanelName,
    string InstalledPower,
    string DemandFactor,
    string DemandPower,
    string PowerFactor,
    string Current,
    string ShortCircuitCurrent)
{
    public IReadOnlyDictionary<string, string> ToAttributes() => new Dictionary<string, string>
    {
        ["PANEL_NAME"] = PanelName,
        ["PWR"] = InstalledPower,
        ["DEMAND_FACTOR"] = DemandFactor,
        ["PWR_DEMAND"] = DemandPower,
        ["PWR_FACTOR"] = PowerFactor,
        ["CURRENT"] = Current,
        ["SHORT_CURRENT"] = ShortCircuitCurrent,
    };
}

public sealed record PhaseLoad(string Power, string Current, string Unbalance);

/// <summary>Block KNF. Inserted only when <see cref="IsRequired"/> is set in Excel.</summary>
public sealed record PhaseBalance(bool IsRequired, IReadOnlyList<PhaseLoad> Phases)
{
    public IReadOnlyDictionary<string, string> ToAttributes()
    {
        var result = new Dictionary<string, string>();
        for (int i = 0; i < Phases.Count; i++)
        {
            result[$"L{i + 1}_PP"] = Phases[i].Power;
            result[$"L{i + 1}_I"] = Phases[i].Current;
            result[$"L{i + 1}_KNF"] = Phases[i].Unbalance;
        }
        return result;
    }
}

/// <summary>Block INCOMER.</summary>
public sealed record IncomerData(
    string IncomerType,
    IReadOnlyList<string> Breaker,
    IReadOnlyList<string> UpstreamBreaker,
    string Cable,
    string CableLength,
    IReadOnlyList<string> AdditionalDevice,
    string UpstreamBoardName)
{
    public IReadOnlyDictionary<string, string> ToAttributes()
    {
        var result = new Dictionary<string, string>
        {
            ["CABLE"] = Cable,
            ["CABLE_LENGHT"] = CableLength,
            ["MDB_NAME"] = UpstreamBoardName,
        };
        for (int i = 0; i < Breaker.Count; i++)
            result[$"DB-CB_{i + 1}"] = Breaker[i];
        for (int i = 0; i < UpstreamBreaker.Count; i++)
            result[$"MDB-CB_{i + 1}"] = UpstreamBreaker[i];
        for (int i = 0; i < AdditionalDevice.Count; i++)
            result[$"ADD_{i + 1}"] = AdditionalDevice[i];
        return result;
    }
}

/// <summary>One column of DB_EXPORT: blocks AUTOMAT + LINE.</summary>
public sealed record GroupData(int Index, IReadOnlyList<string> Rows)
{
    public string BreakerType => Rows[GroupRows.BreakerType];
    public string ConsumerType => Rows[GroupRows.Consumer];

    public IReadOnlyDictionary<string, string> ToAutomatAttributes() => Map(AttributeMap.Automat);
    public IReadOnlyDictionary<string, string> ToLineAttributes() => Map(AttributeMap.Line);

    private Dictionary<string, string> Map(IReadOnlyDictionary<string, int> map) =>
        map.ToDictionary(kv => kv.Key, kv => Rows[kv.Value]);
}
