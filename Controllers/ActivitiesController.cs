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
public class ActivitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public ActivitiesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(
        string? search,
        string? type,
        string? status,
        string? assignedTo,
        DateTime? date,
        int page = 1)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var query = _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.AssignedUser)
            .AsQueryable();

        if (isSalesExec)
        {
            query = query.Where(a => a.AssignedTo == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(a => a.Subject.ToLower().Contains(s) || (a.Description != null && a.Description.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(a => a.ActivityType == type);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (date.HasValue)
        {
            query = query.Where(a => a.ActivityDate.Date == date.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(assignedTo) && !isSalesExec)
        {
            query = query.Where(a => a.AssignedTo == assignedTo);
        }

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var activities = await query
            .OrderByDescending(a => a.ActivityDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Type = type;
        ViewBag.Status = status;
        ViewBag.AssignedTo = assignedTo;
        ViewBag.Date = date?.ToString("yyyy-MM-dd");
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        ViewBag.UsersList = new SelectList(await _userManager.Users.Where(u => u.IsActive).ToListAsync(), "Id", "FullName");

        return View(activities);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
    {
        await PopulateDropdowns(customerId, leadId);
        return View(new Activity
        {
            CustomerId = customerId,
            LeadId = leadId,
            ActivityDate = DateTime.Now,
            ActivityType = "Call",
            Status = "Completed"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Activity activity)
    {
        if (User.IsInRole("Sales Executive"))
        {
            activity.AssignedTo = _userManager.GetUserId(User);
        }

        if (ModelState.IsValid)
        {
            _context.Add(activity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "Activity", recordId: activity.ActivityId.ToString(),
                newValue: $"Logged Activity [{activity.ActivityType}]: {activity.Subject}");

            TempData["SuccessMessage"] = "Activity logged successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns(activity.CustomerId, activity.LeadId, activity.AssignedTo);
        return View(activity);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var activity = await _context.Activities.FindAsync(id);
        if (activity == null) return NotFound();

        if (User.IsInRole("Sales Executive") && activity.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        await PopulateDropdowns(activity.CustomerId, activity.LeadId, activity.AssignedTo);
        return View(activity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Activity activity)
    {
        if (id != activity.ActivityId) return NotFound();

        var existing = await _context.Activities.AsNoTracking().FirstOrDefaultAsync(a => a.ActivityId == id);
        if (existing == null) return NotFound();

        if (User.IsInRole("Sales Executive") && existing.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        if (User.IsInRole("Sales Executive"))
        {
            activity.AssignedTo = existing.AssignedTo;
        }

        if (ModelState.IsValid)
        {
            _context.Update(activity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Update", "Activity", recordId: activity.ActivityId.ToString(),
                oldValue: existing.Subject, newValue: activity.Subject);

            TempData["SuccessMessage"] = "Activity updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns(activity.CustomerId, activity.LeadId, activity.AssignedTo);
        return View(activity);
    }

    private async Task PopulateDropdowns(int? customerId = null, int? leadId = null, string? assignedTo = null)
    {
        var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
        var leads = await _context.Leads.OrderBy(l => l.LeadName).ToListAsync();
        var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();

        ViewBag.CustomerId = new SelectList(customers, "CustomerId", "CustomerName", customerId);
        ViewBag.LeadId = new SelectList(leads, "LeadId", "LeadName", leadId);
        ViewBag.AssignedTo = new SelectList(users, "Id", "FullName", assignedTo);
    }
}
