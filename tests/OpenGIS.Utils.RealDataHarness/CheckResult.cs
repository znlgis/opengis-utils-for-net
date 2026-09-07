namespace OpenGIS.Utils.RealDataHarness;

/// <summary>
///     单项检查结果
/// </summary>
public record CheckResult(
    string Dimension,
    string Target,
    string Check,
    CheckStatus Status,
    string Details)
{
    public override string ToString()
    {
        return $"[{Status,4}] {Dimension} | {Target} | {Check} | {Details}";
    }
}

/// <summary>
///     检查状态
/// </summary>
public enum CheckStatus
{
    Pass,
    Fail,
    Warn,
    Info
}
