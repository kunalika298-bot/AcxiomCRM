using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class AuditLog
{
    [Key]
    public int AuditLogId { get; set; }

    [StringLength(450)]
    [Display(Name = "User ID")]
    public string? UserId { get; set; }

    [StringLength(256)]
    [Display(Name = "User Email")]
    public string? UserEmail { get; set; }

    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty; // Create, Update, Delete, Login, Logout, Convert, etc.

    [Required]
    [StringLength(100)]
    [Display(Name = "Entity Name")]
    public string EntityName { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Record ID")]
    public string? RecordId { get; set; }

    [Display(Name = "Old Value")]
    public string? OldValue { get; set; }

    [Display(Name = "New Value")]
    public string? NewValue { get; set; }

    [Display(Name = "Date & Time")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [StringLength(50)]
    [Display(Name = "IP Address")]
    public string? IpAddress { get; set; }
}
