using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models.DTOs;

public class CustomerDto
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    public string? CompanyName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedDate { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
}

public class LeadDto
{
    public int LeadId { get; set; }
    public string LeadCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LeadName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    public string? CompanyName { get; set; }
    public string? Source { get; set; }
    public string Status { get; set; } = "New";

    [Range(0, 9999999999.99)]
    public decimal ExpectedValue { get; set; }

    public string Priority { get; set; } = "Medium";
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public int? ConvertedCustomerId { get; set; }
}

public class OpportunityDto
{
    public int OpportunityId { get; set; }

    [Required]
    [StringLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }

    [Required]
    [Range(0.01, 9999999999.99)]
    public decimal Amount { get; set; }

    public string Stage { get; set; } = "Qualification";

    [Range(0, 100)]
    public decimal Probability { get; set; }

    public decimal WeightedPipeline { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedDate { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
}
