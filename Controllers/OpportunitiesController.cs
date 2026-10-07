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
public class OpportunitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public OpportunitiesController(
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
        string? stage,
        string? status,
        string? assignedTo,
        int page = 1)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var query = _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedUser)
            .AsQueryable();

        if (isSalesExec)
        {
            query = query.Where(o => o.AssignedTo == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(o =>
                o.OpportunityName.ToLower().Contains(s) ||
                (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(s)) ||
                (o.Lead != null && o.Lead.LeadName.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(assignedTo) && !isSalesExec)
        {
            query = query.Where(o => o.AssignedTo == assignedTo);
        }

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var opportunities = await query
            .OrderByDescending(o => o.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Stage = stage;
        ViewBag.Status = status;
        ViewBag.AssignedTo = assignedTo;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        ViewBag.TotalValue = await query.SumAsync(o => o.Amount);
        ViewBag.WeightedValue = await query.SumAsync(o => (o.Amount * o.Probability) / 100m);

        ViewBag.UsersList = new SelectList(await _userManager.Users.Where(u => u.IsActive).ToListAsync(), "Id", "FullName");

        return View(opportunities);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var opportunity = await _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedUser)
            .Include(o => o.FollowUps)
            .FirstOrDefaultAsync(m => m.OpportunityId == id);

        if (opportunity == null) return NotFound();

        if (User.IsInRole("Sales Executive") && opportunity.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        return View(opportunity);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
    {
        await PopulateDropdowns(customerId, leadId);
        return View(new Opportunity
        {
            CustomerId = customerId,
            LeadId = leadId,
            ExpectedCloseDate = DateTime.Today.AddDays(30),
            Probability = 25m,
            Amount = 1000m,
            Stage = "Qualification",
            Status = "Open"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity opportunity)
    {
        // Business Rules Validation
        if (opportunity.Status != "Lost" && opportunity.Amount <= 0)
        {
            ModelState.AddModelError("Amount", "Opportunity amount must be greater than 0 for active opportunities.");
        }

        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100%.");
        }

        if (opportunity.Status == "Open" && opportunity.ExpectedCloseDate.Date < DateTime.Today)
        {
            ModelState.AddModelError("ExpectedCloseDate", "Expected close date cannot be in the past for active opportunities.");
        }

        if (User.IsInRole("Sales Executive"))
        {
            opportunity.AssignedTo = _userManager.GetUserId(User);
        }

        // Align status with stage
        if (opportunity.Stage == "Won") opportunity.Status = "Won";
        else if (opportunity.Stage == "Lost") opportunity.Status = "Lost";
        else opportunity.Status = "Open";

        opportunity.CreatedDate = DateTime.UtcNow;

        if (ModelState.IsValid)
        {
            _context.Add(opportunity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "Opportunity", recordId: opportunity.OpportunityId.ToString(),
                newValue: $"Created Opportunity {opportunity.OpportunityName} - ${opportunity.Amount:N2} ({opportunity.Stage})");

            TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
        return View(opportunity);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var opportunity = await _context.Opportunities.FindAsync(id);
        if (opportunity == null) return NotFound();

        if (User.IsInRole("Sales Executive") && opportunity.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        await PopulateDropdowns(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
        return View(opportunity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Opportunity opportunity)
    {
        if (id != opportunity.OpportunityId) return NotFound();

        var existing = await _context.Opportunities.AsNoTracking().FirstOrDefaultAsync(o => o.OpportunityId == id);
        if (existing == null) return NotFound();

        if (User.IsInRole("Sales Executive") && existing.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        // Business Rules Validation
        if (opportunity.Status != "Lost" && opportunity.Amount <= 0)
        {
            ModelState.AddModelError("Amount", "Opportunity amount must be greater than 0 for active opportunities.");
        }

        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100%.");
        }

        if (opportunity.Status == "Open" && opportunity.ExpectedCloseDate.Date < DateTime.Today)
        {
            ModelState.AddModelError("ExpectedCloseDate", "Expected close date cannot be in the past for active opportunities.");
        }

        if (User.IsInRole("Sales Executive"))
        {
            opportunity.AssignedTo = existing.AssignedTo;
        }

        // Align status with stage
        if (opportunity.Stage == "Won") opportunity.Status = "Won";
        else if (opportunity.Stage == "Lost") opportunity.Status = "Lost";
        else opportunity.Status = "Open";

        opportunity.CreatedDate = existing.CreatedDate;

        if (ModelState.IsValid)
        {
            _context.Update(opportunity);
            await _context.SaveChangesAsync();

            var oldSummary = $"Stage: {existing.Stage}, Amount: {existing.Amount}, Prob: {existing.Probability}%";
            var newSummary = $"Stage: {opportunity.Stage}, Amount: {opportunity.Amount}, Prob: {opportunity.Probability}%";
            await _auditService.LogAsync("Update", "Opportunity", recordId: opportunity.OpportunityId.ToString(),
                oldValue: oldSummary, newValue: newSummary);

            TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdowns(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
        return View(opportunity);
    }

    private async Task PopulateDropdowns(int? customerId = null, int? leadId = null, string? assignedTo = null)
    {
        var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
        var leads = await _context.Leads.Where(l => l.Status != "Converted").OrderBy(l => l.LeadName).ToListAsync();
        var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();

        ViewBag.CustomerId = new SelectList(customers, "CustomerId", "CustomerName", customerId);
        ViewBag.LeadId = new SelectList(leads, "LeadId", "LeadName", leadId);
        ViewBag.AssignedTo = new SelectList(users, "Id", "FullName", assignedTo);
    }
}
