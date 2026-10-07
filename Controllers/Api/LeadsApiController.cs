using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.DTOs;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/leads")]
[Authorize]
public class LeadsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public LeadsApiController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeadDto>>> GetLeads([FromQuery] string? status)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var query = _context.Leads.Include(l => l.AssignedUser).AsQueryable();

        if (isSalesExec)
        {
            query = query.Where(l => l.AssignedTo == currentUserId);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(l => l.Status == status);
        }

        var leads = await query.OrderByDescending(l => l.CreatedDate)
            .Select(l => new LeadDto
            {
                LeadId = l.LeadId,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                Email = l.Email,
                Phone = l.Phone,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Status = l.Status,
                ExpectedValue = l.ExpectedValue,
                Priority = l.Priority,
                Notes = l.Notes,
                CreatedDate = l.CreatedDate,
                AssignedToUserId = l.AssignedTo,
                AssignedToName = l.AssignedUser != null ? l.AssignedUser.FullName : null,
                ConvertedCustomerId = l.ConvertedCustomerId
            })
            .ToListAsync();

        return Ok(leads);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeadDto>> GetLead(int id)
    {
        var l = await _context.Leads.Include(x => x.AssignedUser).FirstOrDefaultAsync(x => x.LeadId == id);
        if (l == null) return NotFound(new { message = $"Lead with ID {id} was not found." });

        if (User.IsInRole("Sales Executive") && l.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        return Ok(new LeadDto
        {
            LeadId = l.LeadId,
            LeadCode = l.LeadCode,
            LeadName = l.LeadName,
            Email = l.Email,
            Phone = l.Phone,
            CompanyName = l.CompanyName,
            Source = l.Source,
            Status = l.Status,
            ExpectedValue = l.ExpectedValue,
            Priority = l.Priority,
            Notes = l.Notes,
            CreatedDate = l.CreatedDate,
            AssignedToUserId = l.AssignedTo,
            AssignedToName = l.AssignedUser?.FullName,
            ConvertedCustomerId = l.ConvertedCustomerId
        });
    }

    [HttpPost]
    public async Task<ActionResult<LeadDto>> CreateLead([FromBody] LeadDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var assignedId = User.IsInRole("Sales Executive") ? _userManager.GetUserId(User) : dto.AssignedToUserId;

        var lead = new Lead
        {
            LeadCode = string.IsNullOrEmpty(dto.LeadCode) ? $"LEAD-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}" : dto.LeadCode,
            LeadName = dto.LeadName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Source = dto.Source,
            Status = string.IsNullOrEmpty(dto.Status) ? "New" : dto.Status,
            ExpectedValue = dto.ExpectedValue,
            Priority = string.IsNullOrEmpty(dto.Priority) ? "Medium" : dto.Priority,
            Notes = dto.Notes,
            CreatedDate = DateTime.UtcNow,
            AssignedTo = assignedId
        };

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Create via API", "Lead", recordId: lead.LeadId.ToString(), newValue: $"Created Lead {lead.LeadName}");

        dto.LeadId = lead.LeadId;
        dto.LeadCode = lead.LeadCode;
        dto.CreatedDate = lead.CreatedDate;
        dto.AssignedToUserId = lead.AssignedTo;

        return CreatedAtAction(nameof(GetLead), new { id = lead.LeadId }, dto);
    }
}
