namespace SalesCRM.Core.Entities;

public class Board : BaseEntity
{
    /// Full board name shown in suggestions, e.g. "Central Board of Secondary Education".
    public string Name { get; set; } = string.Empty;

    /// Short code shown in compact lists, e.g. "CBSE", "ICSE", "GSEB".
    public string? ShortCode { get; set; }

    /// "Central", "State", "International" — used for grouping in the UI.
    public string? Category { get; set; }
}
