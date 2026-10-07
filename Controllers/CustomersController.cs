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
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public CustomersController(
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
        string? assignedTo,
        string? sortOrder,
        int page = 1)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var query = _context.Customers
            .Include(c => c.AssignedUser)
            .AsQueryable();

        // Role-based data scoping
        if (isSalesExec)
        {
            query = query.Where(c => c.AssignedTo == currentUserId);
        }

        // Search by Name, Email, Phone, Company
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c =>
                c.CustomerName.ToLower().Contains(s) ||
                c.Email.ToLower().Contains(s) ||
                c.Phone.ToLower().Contains(s) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(s)) ||
                c.CustomerCode.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(assignedTo) && !isSalesExec)
        {
            query = query.Where(c => c.AssignedTo == assignedTo);
        }

        // Sorting
        query = sortOrder switch
        {
            "name_desc" => query.OrderByDescending(c => c.CustomerName),
            "date_asc" => query.OrderBy(c => c.CreatedDate),
            "company_asc" => query.OrderBy(c => c.CompanyName),
            "company_desc" => query.OrderByDescending(c => c.CompanyName),
            _ => query.OrderByDescending(c => c.CreatedDate)
        };

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var customers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.AssignedTo = assignedTo;
        ViewBag.SortOrder = sortOrder;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        ViewBag.UsersList = new SelectList(await _userManager.Users.Where(u => u.IsActive).ToListAsync(), "Id", "FullName");

        return View(customers);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var customer = await _context.Customers
            .Include(c => c.AssignedUser)
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(m => m.CustomerId == id);

        if (customer == null) return NotFound();

        if (User.IsInRole("Sales Executive") && customer.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        return View(customer);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateUsersDropdown();
        return View(new Customer
        {
            CustomerCode = $"CUST-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        // Business Validation: Unique Email and Phone
        if (await _context.Customers.AnyAsync(c => c.Email == customer.Email))
        {
            ModelState.AddModelError("Email", "A customer with this email address already exists.");
        }

        if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone))
        {
            ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
        }

        if (User.IsInRole("Sales Executive"))
        {
            customer.AssignedTo = _userManager.GetUserId(User);
        }

        customer.CreatedBy = User.Identity?.Name ?? "System";
        customer.CreatedDate = DateTime.UtcNow;

        if (ModelState.IsValid)
        {
            _context.Add(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "Customer", recordId: customer.CustomerId.ToString(),
                newValue: $"Created Customer {customer.CustomerName} ({customer.CustomerCode})");

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' was successfully created.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateUsersDropdown(customer.AssignedTo);
        return View(customer);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        if (User.IsInRole("Sales Executive") && customer.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        await PopulateUsersDropdown(customer.AssignedTo);
        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.CustomerId) return NotFound();

        var existing = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == id);
        if (existing == null) return NotFound();

        if (User.IsInRole("Sales Executive") && existing.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        // Duplicate checks excluding self
        if (await _context.Customers.AnyAsync(c => c.Email == customer.Email && c.CustomerId != id))
        {
            ModelState.AddModelError("Email", "Another customer already has this email address.");
        }

        if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone && c.CustomerId != id))
        {
            ModelState.AddModelError("Phone", "Another customer already has this phone number.");
        }

        if (User.IsInRole("Sales Executive"))
        {
            customer.AssignedTo = existing.AssignedTo;
        }

        customer.CreatedBy = existing.CreatedBy;
        customer.CreatedDate = existing.CreatedDate;

        if (ModelState.IsValid)
        {
            _context.Update(customer);
            await _context.SaveChangesAsync();

            var oldSummary = $"Name: {existing.CustomerName}, Email: {existing.Email}, Phone: {existing.Phone}, Status: {existing.Status}";
            var newSummary = $"Name: {customer.CustomerName}, Email: {customer.Email}, Phone: {customer.Phone}, Status: {customer.Status}";
            await _auditService.LogAsync("Update", "Customer", recordId: customer.CustomerId.ToString(),
                oldValue: oldSummary, newValue: newSummary);

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' was updated.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateUsersDropdown(customer.AssignedTo);
        return View(customer);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var customer = await _context.Customers
            .Include(c => c.AssignedUser)
            .FirstOrDefaultAsync(m => m.CustomerId == id);

        if (customer == null) return NotFound();

        return View(customer);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var customer = await _context.Customers
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer != null)
        {
            // Nullify or handle child relationships if any exist to prevent FK errors
            if (customer.Opportunities.Any() || customer.FollowUps.Any() || customer.Activities.Any())
            {
                TempData["ErrorMessage"] = $"Cannot delete customer '{customer.CustomerName}' because linked opportunities, follow-ups, or activities exist. Archive or reassign them first.";
                return RedirectToAction(nameof(Index));
            }

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Delete", "Customer", recordId: customer.CustomerId.ToString(),
                oldValue: $"Deleted Customer {customer.CustomerName} ({customer.CustomerCode})");

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' was deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateUsersDropdown(string? selectedId = null)
    {
        var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        ViewBag.AssignedTo = new SelectList(users, "Id", "FullName", selectedId);
    }
}
