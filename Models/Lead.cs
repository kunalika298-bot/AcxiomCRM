using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Lead
{
    [Key]
    public int LeadId { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Lead Code")]
    public string LeadCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lead Name is required")]
    [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters")]
    [Display(Name = "Lead Name")]
    public string LeadName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid Email format")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required")]
    [Phone(ErrorMessage = "Invalid Phone number format")]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [StringLength(100)]
    public string? Source { get; set; } // Website, Referral, Cold Call, Advertisement, Partner, Other

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "New"; // New, Contacted, Qualified, Unqualified, Converted, Lost

    [Range(0, 9999999999.99, ErrorMessage = "Expected Value must be 0 or greater")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Expected Value ($)")]
    public decimal ExpectedValue { get; set; } = 0m;

    [Required]
    [StringLength(20)]
    public string Priority { get; set; } = "Medium"; // Low, Medium, High

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    [ForeignKey(nameof(AssignedTo))]
    public virtual ApplicationUser? AssignedUser { get; set; }

    [Display(Name = "Converted Customer")]
    public int? ConvertedCustomerId { get; set; }

    [ForeignKey(nameof(ConvertedCustomerId))]
    public virtual Customer? ConvertedCustomer { get; set; }

    // Navigation properties
    public virtual ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
