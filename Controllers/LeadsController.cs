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
public class LeadsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public LeadsController(
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
        string? status,
        string? priority,
        string? assignedTo,
        int page = 1)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var query = _context.Leads
            .Include(l => l.AssignedUser)
            .Include(l => l.ConvertedCustomer)
            .AsQueryable();

        if (isSalesExec)
        {
            query = query.Where(l => l.AssignedTo == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(l =>
                l.LeadName.ToLower().Contains(s) ||
                (l.CompanyName != null && l.CompanyName.ToLower().Contains(s)) ||
                l.Email.ToLower().Contains(s) ||
                l.Phone.ToLower().Contains(s) ||
                l.LeadCode.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(l => l.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(assignedTo) && !isSalesExec)
        {
            query = query.Where(l => l.AssignedTo == assignedTo);
        }

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var leads = await query
            .OrderByDescending(l => l.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.Priority = priority;
        ViewBag.AssignedTo = assignedTo;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        ViewBag.UsersList = new SelectList(await _userManager.Users.Where(u => u.IsActive).ToListAsync(), "Id", "FullName");

        return View(leads);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var lead = await _context.Leads
            .Include(l => l.AssignedUser)
            .Include(l => l.ConvertedCustomer)
            .Include(l => l.Opportunities)
            .Include(l => l.FollowUps)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(m => m.LeadId == id);

        if (lead == null) return NotFound();

        if (User.IsInRole("Sales Executive") && lead.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        return View(lead);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateUsersDropdown();
        return View(new Lead
        {
            LeadCode = $"LEAD-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}",
            ExpectedValue = 1000m
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead lead)
    {
        if (User.IsInRole("Sales Executive"))
        {
            lead.AssignedTo = _userManager.GetUserId(User);
        }

        lead.CreatedDate = DateTime.UtcNow;

        if (ModelState.IsValid)
        {
            _context.Add(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "Lead", recordId: lead.LeadId.ToString(),
                newValue: $"Created Lead {lead.LeadName} ({lead.LeadCode}) - Expected ${lead.ExpectedValue:N0}");

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateUsersDropdown(lead.AssignedTo);
        return View(lead);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();

        if (User.IsInRole("Sales Executive") && lead.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        await PopulateUsersDropdown(lead.AssignedTo);
        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lead lead)
    {
        if (id != lead.LeadId) return NotFound();

        var existing = await _context.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.LeadId == id);
        if (existing == null) return NotFound();

        if (User.IsInRole("Sales Executive") && existing.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        if (User.IsInRole("Sales Executive"))
        {
            lead.AssignedTo = existing.AssignedTo;
        }

        lead.CreatedDate = existing.CreatedDate;
        lead.ConvertedCustomerId = existing.ConvertedCustomerId;

        if (ModelState.IsValid)
        {
            _context.Update(lead);
            await _context.SaveChangesAsync();

            var oldSummary = $"Status: {existing.Status}, Priority: {existing.Priority}, Assigned: {existing.AssignedTo}";
            var newSummary = $"Status: {lead.Status}, Priority: {lead.Priority}, Assigned: {lead.AssignedTo}";
            await _auditService.LogAsync("Update", "Lead", recordId: lead.LeadId.ToString(),
                oldValue: oldSummary, newValue: newSummary);

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateUsersDropdown(lead.AssignedTo);
        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();

        if (User.IsInRole("Sales Executive") && lead.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        var oldStatus = lead.Status;
        lead.Status = status;
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Status Change", "Lead", recordId: lead.LeadId.ToString(),
            oldValue: $"Status: {oldStatus}", newValue: $"Status: {status}");

        TempData["SuccessMessage"] = $"Lead status updated to '{status}'.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Lead Conversion Action
    [HttpGet]
    public async Task<IActionResult> Convert(int? id)
    {
        if (id == null) return NotFound();

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();

        if (lead.Status == "Converted")
        {
            TempData["ErrorMessage"] = "This lead has already been converted to a customer.";
            return RedirectToAction(nameof(Details), new { id = lead.LeadId });
        }

        if (User.IsInRole("Sales Executive") && lead.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        ViewBag.Lead = lead;
        var newCustomer = new Customer
        {
            CustomerCode = $"CUST-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}",
            CustomerName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Status = "Active",
            AssignedTo = lead.AssignedTo
        };

        return View(newCustomer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(int leadId, Customer customer)
    {
        var lead = await _context.Leads.FindAsync(leadId);
        if (lead == null) return NotFound();

        if (User.IsInRole("Sales Executive") && lead.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        // Unique validation
        if (await _context.Customers.AnyAsync(c => c.Email == customer.Email))
        {
            ModelState.AddModelError("Email", "A customer with this email address already exists.");
        }

        if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone))
        {
            ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
        }

        customer.CreatedBy = User.Identity?.Name ?? "Lead Conversion";
        customer.CreatedDate = DateTime.UtcNow;
        customer.Status = "Active";

        if (ModelState.IsValid)
        {
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            // Update lead
            lead.Status = "Converted";
            lead.ConvertedCustomerId = customer.CustomerId;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Lead Conversion", "Lead", recordId: lead.LeadId.ToString(),
                newValue: $"Converted Lead {lead.LeadName} ({lead.LeadCode}) to Customer {customer.CustomerName} (ID: {customer.CustomerId})");

            TempData["SuccessMessage"] = $"Lead successfully converted to Customer '{customer.CustomerName}'!";
            return RedirectToAction("Details", "Customers", new { id = customer.CustomerId });
        }

        ViewBag.Lead = lead;
        return View(customer);
    }

    private async Task PopulateUsersDropdown(string? selectedId = null)
    {
        var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        ViewBag.AssignedTo = new SelectList(users, "Id", "FullName", selectedId);
    }
}
