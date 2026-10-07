using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers;

[Authorize]
public class FollowUpsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public FollowUpsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(
        string? filterType, // "upcoming", "overdue", "all"
        string? status,
        string? type,
        string? assignedTo,
        int page = 1)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var query = _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.Opportunity)
            .Include(f => f.AssignedUser)
            .AsQueryable();

        if (isSalesExec)
        {
            query = query.Where(f => f.AssignedTo == currentUserId);
        }

        var now = DateTime.UtcNow;

        if (filterType == "upcoming")
        {
            query = query.Where(f => f.FollowUpDate >= now && f.Status == "Planned");
        }
        else if (filterType == "overdue")
        {
            query = query.Where(f => f.FollowUpDate < now && f.Status == "Planned");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(f => f.FollowUpType == type);
        }

        if (!string.IsNullOrWhiteSpace(assignedTo) && !isSalesExec)
        {
            query = query.Where(f => f.AssignedTo == assignedTo);
        }

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var followUps = await query
            .OrderBy(f => f.FollowUpDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.FilterType = filterType ?? "all";
        ViewBag.Status = status;
        ViewBag.Type = type;
        ViewBag.AssignedTo = assignedTo;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        ViewBag.UsersList = new SelectList(await _userManager.Users.Where(u => u.IsActive).ToListAsync(), "Id", "FullName");

        return View(followUps);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null, int? opportunityId = null)
    {
        await PopulateDropdowns(customerId, leadId, opportunityId);
        return View(new FollowUp
        {
            CustomerId = customerId,
            LeadId = leadId,
            OpportunityId = opportunityId,
            FollowUpDate = DateTime.Today.AddDays(1).AddHours(10),
            FollowUpType = "Call",
            Status = "Planned"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUp followUp)
    {
        // Business Rule: Follow-up date cannot be in the past for new planned follow-up
        if (followUp.Status == "Planned" && followUp.FollowUpDate < DateTime.Today)
        {
            ModelState.AddModelError("FollowUpDate", "Follow-up date and time cannot be before today.");
        }

        if (User.IsInRole("Sales Executive"))
        {
            followUp.AssignedTo = _userManager.GetUserId(User);
        }

        if (ModelState.IsValid)
        {
            _context.Add(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "FollowUp", recordId: followUp.FollowUpId.ToString(),
                newValue: $"Created FollowUp ({followUp.FollowUpType}) on {followUp.FollowUpDate:yyyy-MM-dd HH:mm}");

            TempData["SuccessMessage"] = "Follow-up scheduled successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedTo);
        return View(followUp);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();

        if (User.IsInRole("Sales Executive") && followUp.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        await PopulateDropdowns(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedTo);
        return View(followUp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FollowUp followUp)
    {
        if (id != followUp.FollowUpId) return NotFound();

        var existing = await _context.FollowUps.AsNoTracking().FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (existing == null) return NotFound();

        if (User.IsInRole("Sales Executive") && existing.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        if (User.IsInRole("Sales Executive"))
        {
            followUp.AssignedTo = existing.AssignedTo;
        }

        if (ModelState.IsValid)
        {
            _context.Update(followUp);
            await _context.SaveChangesAsync();

            var oldSummary = $"Date: {existing.FollowUpDate:yyyy-MM-dd HH:mm}, Status: {existing.Status}";
            var newSummary = $"Date: {followUp.FollowUpDate:yyyy-MM-dd HH:mm}, Status: {followUp.Status}";
            await _auditService.LogAsync("Update", "FollowUp", recordId: followUp.FollowUpId.ToString(),
                oldValue: oldSummary, newValue: newSummary);

            TempData["SuccessMessage"] = "Follow-up updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedTo);
        return View(followUp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? remarks)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();

        if (User.IsInRole("Sales Executive") && followUp.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        var oldStatus = followUp.Status;
        followUp.Status = "Completed";
        if (!string.IsNullOrEmpty(remarks))
        {
            followUp.Remarks = (followUp.Remarks + " | Completed: " + remarks).Trim();
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Status Change", "FollowUp", recordId: followUp.FollowUpId.ToString(),
            oldValue: $"Status: {oldStatus}", newValue: "Status: Completed");

        TempData["SuccessMessage"] = "Follow-up marked as Completed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int id, DateTime newDate, string? reason)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();

        if (User.IsInRole("Sales Executive") && followUp.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        if (newDate < DateTime.Today)
        {
            TempData["ErrorMessage"] = "New follow-up date cannot be in the past.";
            return RedirectToAction(nameof(Index));
        }

        var oldDate = followUp.FollowUpDate;
        followUp.FollowUpDate = newDate;
        followUp.Status = "Planned";
        if (!string.IsNullOrEmpty(reason))
        {
            followUp.Remarks = (followUp.Remarks + " | Rescheduled: " + reason).Trim();
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Reschedule", "FollowUp", recordId: followUp.FollowUpId.ToString(),
            oldValue: $"Date: {oldDate:yyyy-MM-dd HH:mm}", newValue: $"Date: {newDate:yyyy-MM-dd HH:mm} (Reason: {reason})");

        TempData["SuccessMessage"] = $"Follow-up successfully rescheduled to {newDate:yyyy-MM-dd HH:mm}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelFollowUp(int id)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();

        if (User.IsInRole("Sales Executive") && followUp.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        followUp.Status = "Cancelled";
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Status Change", "FollowUp", recordId: followUp.FollowUpId.ToString(),
            oldValue: "Status: Planned", newValue: "Status: Cancelled");

        TempData["SuccessMessage"] = "Follow-up marked as Cancelled.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdowns(int? customerId = null, int? leadId = null, int? opportunityId = null, string? assignedTo = null)
    {
        var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
        var leads = await _context.Leads.Where(l => l.Status != "Converted").OrderBy(l => l.LeadName).ToListAsync();
        var opportunities = await _context.Opportunities.OrderBy(o => o.OpportunityName).ToListAsync();
        var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();

        ViewBag.CustomerId = new SelectList(customers, "CustomerId", "CustomerName", customerId);
        ViewBag.LeadId = new SelectList(leads, "LeadId", "LeadName", leadId);
        ViewBag.OpportunityId = new SelectList(opportunities, "OpportunityId", "OpportunityName", opportunityId);
        ViewBag.AssignedTo = new SelectList(users, "Id", "FullName", assignedTo);
    }
}
