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
[Route("api/customers")]
[Authorize]
public class CustomersApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public CustomersApiController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<CustomerDto>))]
    public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers([FromQuery] string? search)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isSalesExec = User.IsInRole("Sales Executive");

        var query = _context.Customers.Include(c => c.AssignedUser).AsQueryable();

        if (isSalesExec)
        {
            query = query.Where(c => c.AssignedTo == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c => c.CustomerName.ToLower().Contains(s) || c.Email.ToLower().Contains(s));
        }

        var customers = await query.OrderByDescending(c => c.CreatedDate)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Status = c.Status,
                CreatedDate = c.CreatedDate,
                AssignedToUserId = c.AssignedTo,
                AssignedToName = c.AssignedUser != null ? c.AssignedUser.FullName : null
            })
            .ToListAsync();

        return Ok(customers);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomerDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CustomerDto>> GetCustomer(int id)
    {
        var c = await _context.Customers.Include(x => x.AssignedUser).FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c == null) return NotFound(new { message = $"Customer with ID {id} was not found." });

        if (User.IsInRole("Sales Executive") && c.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        return Ok(new CustomerDto
        {
            CustomerId = c.CustomerId,
            CustomerCode = c.CustomerCode,
            CustomerName = c.CustomerName,
            Email = c.Email,
            Phone = c.Phone,
            CompanyName = c.CompanyName,
            Address = c.Address,
            City = c.City,
            State = c.State,
            Status = c.Status,
            CreatedDate = c.CreatedDate,
            AssignedToUserId = c.AssignedTo,
            AssignedToName = c.AssignedUser?.FullName
        });
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CustomerDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (await _context.Customers.AnyAsync(c => c.Email == dto.Email))
        {
            return BadRequest(new { error = "Email address is already in use by another customer." });
        }

        if (await _context.Customers.AnyAsync(c => c.Phone == dto.Phone))
        {
            return BadRequest(new { error = "Phone number is already in use by another customer." });
        }

        var assignedId = User.IsInRole("Sales Executive") ? _userManager.GetUserId(User) : dto.AssignedToUserId;

        var customer = new Customer
        {
            CustomerCode = string.IsNullOrEmpty(dto.CustomerCode) ? $"CUST-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(100, 999)}" : dto.CustomerCode,
            CustomerName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Status = string.IsNullOrEmpty(dto.Status) ? "Active" : dto.Status,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "API",
            AssignedTo = assignedId
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Create via API", "Customer", recordId: customer.CustomerId.ToString(),
            newValue: $"Created Customer {customer.CustomerName} via REST API");

        dto.CustomerId = customer.CustomerId;
        dto.CustomerCode = customer.CustomerCode;
        dto.CreatedDate = customer.CreatedDate;
        dto.AssignedToUserId = customer.AssignedTo;

        return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, dto);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] CustomerDto dto)
    {
        if (id != dto.CustomerId)
        {
            return BadRequest(new { error = "ID in URL does not match ID in payload." });
        }

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound(new { message = $"Customer with ID {id} was not found." });

        if (User.IsInRole("Sales Executive") && customer.AssignedTo != _userManager.GetUserId(User))
        {
            return Forbid();
        }

        if (await _context.Customers.AnyAsync(c => c.Email == dto.Email && c.CustomerId != id))
        {
            return BadRequest(new { error = "Email address is already in use by another customer." });
        }

        customer.CustomerName = dto.CustomerName;
        customer.Email = dto.Email;
        customer.Phone = dto.Phone;
        customer.CompanyName = dto.CompanyName;
        customer.Address = dto.Address;
        customer.City = dto.City;
        customer.State = dto.State;
        customer.Status = dto.Status;

        if (!User.IsInRole("Sales Executive"))
        {
            customer.AssignedTo = dto.AssignedToUserId;
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Update via API", "Customer", recordId: id.ToString(), newValue: $"Updated customer {customer.CustomerName}");

        return Ok(new { message = "Customer updated successfully." });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await _context.Customers
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null) return NotFound(new { message = $"Customer with ID {id} was not found." });

        if (customer.Opportunities.Any() || customer.FollowUps.Any())
        {
            return BadRequest(new { error = "Cannot delete customer with existing opportunities or follow-ups." });
        }

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Delete via API", "Customer", recordId: id.ToString(), oldValue: $"Deleted {customer.CustomerName}");

        return Ok(new { message = "Customer deleted successfully." });
    }
}
