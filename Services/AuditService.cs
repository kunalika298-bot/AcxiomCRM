using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AcxiomCRM.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        ApplicationDbContext _context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        this._context = _context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? recordId = null,
        string? oldValue = null,
        string? newValue = null,
        string? userId = null,
        string? userEmail = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var ip = httpContext?.Connection?.RemoteIpAddress?.ToString();

            if (string.IsNullOrEmpty(userId) && httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            }

            if (string.IsNullOrEmpty(userEmail) && httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                userEmail = httpContext.User.FindFirstValue(ClaimTypes.Email) ?? httpContext.User.Identity?.Name;
            }

            var audit = new AuditLog
            {
                Action = action,
                EntityName = entityName,
                RecordId = recordId,
                OldValue = oldValue,
                NewValue = newValue,
                UserId = userId,
                UserEmail = userEmail,
                CreatedDate = DateTime.UtcNow,
                IpAddress = ip
            };

            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Do not fail main transaction if audit logging fails
            _logger.LogError(ex, "Failed to write audit log for {Action} on {Entity}", action, entityName);
        }
    }
}
