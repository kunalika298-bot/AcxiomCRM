using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.ViewModels;

namespace AcxiomCRM.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string dateRange = "all")
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        // Base queries
        var custQuery = _context.Customers.AsQueryable();
        var leadQuery = _context.Leads.AsQueryable();
        var oppQuery = _context.Opportunities.AsQueryable();
        var fuQuery = _context.FollowUps.AsQueryable();

        // Server-side role authorization
        if (isSalesExec)
        {
            custQuery = custQuery.Where(c => c.AssignedTo == currentUserId);
            leadQuery = leadQuery.Where(l => l.AssignedTo == currentUserId);
            oppQuery = oppQuery.Where(o => o.AssignedTo == currentUserId);
            fuQuery = fuQuery.Where(f => f.AssignedTo == currentUserId);
        }

        // Date range filtering
        DateTime? startDate = dateRange switch
        {
            "month" => new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
            "quarter" => DateTime.UtcNow.AddMonths(-3),
            "year" => new DateTime(DateTime.UtcNow.Year, 1, 1),
            _ => null
        };

        if (startDate.HasValue)
        {
            custQuery = custQuery.Where(c => c.CreatedDate >= startDate.Value);
            leadQuery = leadQuery.Where(l => l.CreatedDate >= startDate.Value);
            oppQuery = oppQuery.Where(o => o.CreatedDate >= startDate.Value);
        }

        var model = new DashboardViewModel
        {
            DateRange = dateRange,
            TotalCustomers = await custQuery.CountAsync(),
            TotalLeads = await leadQuery.CountAsync(),
            OpenLeads = await leadQuery.CountAsync(l => l.Status != "Converted" && l.Status != "Lost"),
            TotalOpportunities = await oppQuery.CountAsync(),
            OpenOpportunities = await oppQuery.CountAsync(o => o.Status == "Open"),
            WonOpportunities = await oppQuery.CountAsync(o => o.Status == "Won"),
            LostOpportunities = await oppQuery.CountAsync(o => o.Status == "Lost"),
            TotalPipelineValue = await oppQuery.Where(o => o.Status == "Open").SumAsync(o => (decimal?)o.Amount) ?? 0m,
            WeightedPipelineValue = await oppQuery.Where(o => o.Status == "Open").SumAsync(o => (decimal?)(o.Amount * o.Probability / 100m)) ?? 0m,
            TotalWonRevenue = await oppQuery.Where(o => o.Status == "Won").SumAsync(o => (decimal?)o.Amount) ?? 0m
        };

        // Chart 1: Leads by Status
        var leadStatusGroups = await leadQuery
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        model.LeadStatusLabels = leadStatusGroups.Select(g => g.Status).ToList();
        model.LeadStatusCounts = leadStatusGroups.Select(g => g.Count).ToList();

        // Chart 2: Pipeline by Stage
        var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
        var oppStageGroups = await oppQuery
            .GroupBy(o => o.Stage)
            .Select(g => new { Stage = g.Key, Sum = g.Sum(o => o.Amount) })
            .ToListAsync();

        foreach (var stage in stages)
        {
            model.OpportunityStageLabels.Add(stage);
            var match = oppStageGroups.FirstOrDefault(g => g.Stage == stage);
            model.OpportunityStageValues.Add(match?.Sum ?? 0m);
        }

        // Chart 3: Monthly Sales / Won Revenue (Past 6 months)
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-5);
        var wonOpps = await _context.Opportunities
            .Where(o => o.Status == "Won" && o.CreatedDate >= sixMonthsAgo)
            .Where(o => !isSalesExec || o.AssignedTo == currentUserId)
            .ToListAsync();

        for (int i = 5; i >= 0; i--)
        {
            var targetMonth = DateTime.UtcNow.AddMonths(-i);
            var monthLabel = targetMonth.ToString("MMM yyyy");
            var monthWonTotal = wonOpps
                .Where(o => o.CreatedDate.Year == targetMonth.Year && o.CreatedDate.Month == targetMonth.Month)
                .Sum(o => o.Amount);

            model.MonthlySalesLabels.Add(monthLabel);
            model.MonthlySalesValues.Add(monthWonTotal);
        }

        // Urgent follow-ups (due today or overdue)
        model.UrgentFollowUps = await fuQuery
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Where(f => f.Status == "Planned" && f.FollowUpDate <= DateTime.UtcNow.AddDays(2))
            .OrderBy(f => f.FollowUpDate)
            .Take(5)
            .ToListAsync();

        // Top open deals
        model.HighValueOpportunities = await oppQuery
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Where(o => o.Status == "Open")
            .OrderByDescending(o => o.Amount)
            .Take(5)
            .ToListAsync();

        return View(model);
    }
}
