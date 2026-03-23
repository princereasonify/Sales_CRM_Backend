namespace SalesCRM.Core.Entities;

public class VisitFieldConfig : BaseEntity
{
    public string FieldName { get; set; } = string.Empty;
    public string FieldType { get; set; } = "Dropdown"; // Dropdown, MultiSelect, Text, Number, Date
    public string? Options { get; set; }    // JSON array
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public int CreatedById { get; set; }

    public User CreatedBy { get; set; } = null!;
}
