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
[Route("api/opportunities")]
[Authorize]
public class OpportunitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public OpportunitiesApiController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OpportunityDto>>> GetOpportunities([FromQuery] string? stage)
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

        if (!string.IsNullOrEmpty(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        var opps = await query.OrderByDescending(o => o.CreatedDate)
            .Select(o => new OpportunityDto
            {
                OpportunityId = o.OpportunityId,
                OpportunityName = o.OpportunityName,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : null,
                LeadId = o.LeadId,
                LeadName = o.Lead != null ? o.Lead.LeadName : null,
                Amount = o.Amount,
                Stage = o.Stage,
                Probability = o.Probability,
                WeightedPipeline = (o.Amount * o.Probability) / 100m,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Status = o.Status,
                CreatedDate = o.CreatedDate,
                AssignedToUserId = o.AssignedTo,
                AssignedToName = o.AssignedUser != null ? o.AssignedUser.FullName : null,
                Source = o.Source,
                Notes = o.Notes
            })
            .ToListAsync();

        return Ok(opps);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OpportunityDto>> GetOpportunity(int id)
    {
        var o = await _context.Opportunities
            .Include(x => x.Customer)
            .Include(x => x.Lead)
            .Include(x => x.AssignedUser)
            .FirstOrDefaultAsync(x => x.OpportunityId == id);

        if (o == null) return NotFound(new { message = $"Opportunity with ID {id} was not found." });

        if (User.IsInRole("Sales Executive") && o.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        return Ok(new OpportunityDto
        {
            OpportunityId = o.OpportunityId,
            OpportunityName = o.OpportunityName,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer?.CustomerName,
            LeadId = o.LeadId,
            LeadName = o.Lead?.LeadName,
            Amount = o.Amount,
            Stage = o.Stage,
            Probability = o.Probability,
            WeightedPipeline = (o.Amount * o.Probability) / 100m,
            ExpectedCloseDate = o.ExpectedCloseDate,
            Status = o.Status,
            CreatedDate = o.CreatedDate,
            AssignedToUserId = o.AssignedTo,
            AssignedToName = o.AssignedUser?.FullName,
            Source = o.Source,
            Notes = o.Notes
        });
    }

    [HttpPost]
    public async Task<ActionResult<OpportunityDto>> CreateOpportunity([FromBody] OpportunityDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (dto.Amount <= 0)
        {
            return BadRequest(new { error = "Amount must be greater than 0." });
        }

        if (dto.Probability < 0 || dto.Probability > 100)
        {
            return BadRequest(new { error = "Probability must be between 0 and 100%." });
        }

        var assignedId = User.IsInRole("Sales Executive") ? _userManager.GetUserId(User) : dto.AssignedToUserId;

        var opp = new Opportunity
        {
            OpportunityName = dto.OpportunityName,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Amount = dto.Amount,
            Stage = string.IsNullOrEmpty(dto.Stage) ? "Qualification" : dto.Stage,
            Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            Status = string.IsNullOrEmpty(dto.Status) ? "Open" : dto.Status,
            CreatedDate = DateTime.UtcNow,
            AssignedTo = assignedId,
            Source = dto.Source,
            Notes = dto.Notes
        };

        _context.Opportunities.Add(opp);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Create via API", "Opportunity", recordId: opp.OpportunityId.ToString(), newValue: $"Created Opportunity {opp.OpportunityName}");

        dto.OpportunityId = opp.OpportunityId;
        dto.CreatedDate = opp.CreatedDate;
        dto.AssignedToUserId = opp.AssignedTo;
        dto.WeightedPipeline = (opp.Amount * opp.Probability) / 100m;

        return CreatedAtAction(nameof(GetOpportunity), new { id = opp.OpportunityId }, dto);
    }
}
