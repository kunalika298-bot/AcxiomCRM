using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> Customers(string? status)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var query = _context.Customers.Include(c => c.AssignedUser).AsQueryable();
        if (isSalesExec) query = query.Where(c => c.AssignedTo == currentUserId);
        if (!string.IsNullOrEmpty(status)) query = query.Where(c => c.Status == status);

        ViewBag.Status = status;
        var list = await query.OrderByDescending(c => c.CreatedDate).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> ExportCustomersCsv(string? status)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var query = _context.Customers.Include(c => c.AssignedUser).AsQueryable();
        if (isSalesExec) query = query.Where(c => c.AssignedTo == currentUserId);
        if (!string.IsNullOrEmpty(status)) query = query.Where(c => c.Status == status);

        var list = await query.ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("CustomerId,CustomerCode,CustomerName,Email,Phone,Company,City,State,Status,AssignedTo,CreatedDate");

        foreach (var c in list)
        {
            sb.AppendLine($"{c.CustomerId},\"{c.CustomerCode}\",\"{c.CustomerName}\",\"{c.Email}\",\"{c.Phone}\",\"{c.CompanyName}\",\"{c.City}\",\"{c.State}\",\"{c.Status}\",\"{c.AssignedUser?.FullName}\",{c.CreatedDate:yyyy-MM-dd}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"Customers_Report_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    public async Task<IActionResult> Leads(string? status, string? priority)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var query = _context.Leads.Include(l => l.AssignedUser).AsQueryable();
        if (isSalesExec) query = query.Where(l => l.AssignedTo == currentUserId);
        if (!string.IsNullOrEmpty(status)) query = query.Where(l => l.Status == status);
        if (!string.IsNullOrEmpty(priority)) query = query.Where(l => l.Priority == priority);

        ViewBag.Status = status;
        ViewBag.Priority = priority;
        var list = await query.OrderByDescending(l => l.CreatedDate).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> ExportLeadsCsv(string? status, string? priority)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var query = _context.Leads.Include(l => l.AssignedUser).AsQueryable();
        if (isSalesExec) query = query.Where(l => l.AssignedTo == currentUserId);
        if (!string.IsNullOrEmpty(status)) query = query.Where(l => l.Status == status);
        if (!string.IsNullOrEmpty(priority)) query = query.Where(l => l.Priority == priority);

        var list = await query.ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("LeadId,LeadCode,LeadName,Email,Phone,Company,Source,Status,Priority,ExpectedValue,AssignedTo,CreatedDate");

        foreach (var l in list)
        {
            sb.AppendLine($"{l.LeadId},\"{l.LeadCode}\",\"{l.LeadName}\",\"{l.Email}\",\"{l.Phone}\",\"{l.CompanyName}\",\"{l.Source}\",\"{l.Status}\",\"{l.Priority}\",{l.ExpectedValue},\"{l.AssignedUser?.FullName}\",{l.CreatedDate:yyyy-MM-dd}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"Leads_Report_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    public async Task<IActionResult> Opportunities(string? stage, string? status)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var query = _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedUser)
            .AsQueryable();

        if (isSalesExec) query = query.Where(o => o.AssignedTo == currentUserId);
        if (!string.IsNullOrEmpty(stage)) query = query.Where(o => o.Stage == stage);
        if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);

        ViewBag.Stage = stage;
        ViewBag.Status = status;
        var list = await query.OrderByDescending(o => o.Amount).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> FollowUps(string? status, string? type)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var query = _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.AssignedUser)
            .AsQueryable();

        if (isSalesExec) query = query.Where(f => f.AssignedTo == currentUserId);
        if (!string.IsNullOrEmpty(status)) query = query.Where(f => f.Status == status);
        if (!string.IsNullOrEmpty(type)) query = query.Where(f => f.FollowUpType == type);

        ViewBag.Status = status;
        ViewBag.Type = type;
        var list = await query.OrderByDescending(f => f.FollowUpDate).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> SalesConversion()
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var leadsQuery = _context.Leads.Include(l => l.ConvertedCustomer).Include(l => l.AssignedUser).AsQueryable();
        var oppsQuery = _context.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser).AsQueryable();

        if (isSalesExec)
        {
            leadsQuery = leadsQuery.Where(l => l.AssignedTo == currentUserId);
            oppsQuery = oppsQuery.Where(o => o.AssignedTo == currentUserId);
        }

        ViewBag.ConvertedLeads = await leadsQuery.Where(l => l.Status == "Converted").ToListAsync();
        ViewBag.WonDeals = await oppsQuery.Where(o => o.Status == "Won").ToListAsync();

        return View();
    }
}
