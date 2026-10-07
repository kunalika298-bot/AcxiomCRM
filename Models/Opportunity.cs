using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Opportunity
{
    [Key]
    public int OpportunityId { get; set; }

    [Required(ErrorMessage = "Opportunity Name is required")]
    [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters")]
    [Display(Name = "Opportunity Name")]
    public string OpportunityName { get; set; } = string.Empty;

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public virtual Customer? Customer { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [ForeignKey(nameof(LeadId))]
    public virtual Lead? Lead { get; set; }

    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, 9999999999.99, ErrorMessage = "Amount must be greater than 0")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Amount ($)")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(50)]
    public string Stage { get; set; } = "Qualification"; // Qualification, Proposal, Negotiation, Won, Lost

    [Required(ErrorMessage = "Probability is required")]
    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100%")]
    [Column(TypeName = "decimal(5,2)")]
    [Display(Name = "Probability (%)")]
    public decimal Probability { get; set; } = 20m;

    [Required(ErrorMessage = "Expected Close Date is required")]
    [DataType(DataType.Date)]
    [Display(Name = "Expected Close Date")]
    public DateTime ExpectedCloseDate { get; set; }

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Open"; // Open, Won, Lost

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    [ForeignKey(nameof(AssignedTo))]
    public virtual ApplicationUser? AssignedUser { get; set; }

    [StringLength(100)]
    public string? Source { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [NotMapped]
    [Display(Name = "Weighted Pipeline ($)")]
    public decimal WeightedPipeline => Math.Round(Amount * (Probability / 100m), 2);

    // Navigation properties
    public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
}
