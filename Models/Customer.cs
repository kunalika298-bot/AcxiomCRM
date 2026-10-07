using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Customer
{
    [Key]
    public int CustomerId { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Customer Code")]
    public string CustomerCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer Name is required")]
    [StringLength(100, ErrorMessage = "Customer Name cannot exceed 100 characters")]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

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

    [StringLength(200)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Active"; // Active, Inactive, LeadConverted

    [Display(Name = "Created Date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    [Display(Name = "Created By")]
    public string? CreatedBy { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    [ForeignKey(nameof(AssignedTo))]
    public virtual ApplicationUser? AssignedUser { get; set; }

    // Navigation properties
    public virtual ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
