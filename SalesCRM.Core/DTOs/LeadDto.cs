namespace SalesCRM.Core.DTOs;

public class LeadDto
{
    public int Id { get; set; }
    public string School { get; set; } = string.Empty;
    public string Board { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Students { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public int Score { get; set; }
    public decimal Value { get; set; }
    public DateTime? CloseDate { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? LossReason { get; set; }
    public int FoId { get; set; }
    public string FoName { get; set; } = string.Empty;
    public ContactDto Contact { get; set; } = null!;
    public List<ActivityDto> Activities { get; set; } = new();
}

public class ContactDto
{
    public string Name { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class LeadListDto
{
    public int Id { get; set; }
    public string School { get; set; } = string.Empty;
    public string Board { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public int Score { get; set; }
    public decimal Value { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public int FoId { get; set; }
    public string FoName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
}

public class CreateLeadRequest
{
    public string School { get; set; } = string.Empty;
    public string Board { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Students { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime? CloseDate { get; set; }
    public string? Notes { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactDesignation { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
}

public class UpdateLeadRequest
{
    public string? School { get; set; }
    public string? Board { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public int? Students { get; set; }
    public string? Type { get; set; }
    public string? Stage { get; set; }
    public decimal? Value { get; set; }
    public DateTime? CloseDate { get; set; }
    public string? Notes { get; set; }
    public string? LossReason { get; set; }
    public string? ContactName { get; set; }
    public string? ContactDesignation { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
}
