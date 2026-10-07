using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin")]
public class AuditLogsController : Controller
{
    private readonly ApplicationDbContext _context;

    public AuditLogsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(
        string? search,
        string? entity,
        string? actionType,
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1)
    {
        const int pageSize = 20;
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(a =>
                (a.UserEmail != null && a.UserEmail.ToLower().Contains(s)) ||
                a.Action.ToLower().Contains(s) ||
                a.EntityName.ToLower().Contains(s) ||
                (a.RecordId != null && a.RecordId.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(entity))
        {
            query = query.Where(a => a.EntityName == entity);
        }

        if (!string.IsNullOrWhiteSpace(actionType))
        {
            query = query.Where(a => a.Action.Contains(actionType));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.CreatedDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var logs = await query
            .OrderByDescending(a => a.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Entity = entity;
        ViewBag.ActionType = actionType;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        ViewBag.Entities = await _context.AuditLogs.Select(a => a.EntityName).Distinct().ToListAsync();

        return View(logs);
    }

    public async Task<IActionResult> Details(int id)
    {
        var log = await _context.AuditLogs.FindAsync(id);
        if (log == null)
        {
            return NotFound();
        }
        return View(log);
    }
}
