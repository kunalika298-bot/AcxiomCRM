using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class FollowUp
{
    [Key]
    public int FollowUpId { get; set; }

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public virtual Customer? Customer { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [ForeignKey(nameof(LeadId))]
    public virtual Lead? Lead { get; set; }

    [Display(Name = "Opportunity")]
    public int? OpportunityId { get; set; }

    [ForeignKey(nameof(OpportunityId))]
    public virtual Opportunity? Opportunity { get; set; }

    [Required(ErrorMessage = "Follow-up Date is required")]
    [Display(Name = "Follow-up Date & Time")]
    public DateTime FollowUpDate { get; set; }

    [Required(ErrorMessage = "Follow-up Type is required")]
    [StringLength(50)]
    [Display(Name = "Type")]
    public string FollowUpType { get; set; } = "Call"; // Call, Email, Meeting, Demo, Task

    [StringLength(1000)]
    [Display(Name = "Remarks / Notes")]
    public string? Remarks { get; set; }

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Planned"; // Planned, Completed, Missed, Cancelled

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    [ForeignKey(nameof(AssignedTo))]
    public virtual ApplicationUser? AssignedUser { get; set; }
}
