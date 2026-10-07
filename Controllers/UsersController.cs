using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;
using AcxiomCRM.Models.ViewModels;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditService _auditService;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditService auditService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search, string? role)
    {
        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(s) || (u.Email != null && u.Email.ToLower().Contains(s)));
        }

        var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
        var userItems = new List<UserItemViewModel>();

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var primaryRole = roles.FirstOrDefault() ?? "No Role";

            if (!string.IsNullOrEmpty(role) && !string.Equals(primaryRole, role, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            userItems.Add(new UserItemViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                Role = primaryRole,
                IsActive = u.IsActive,
                IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
                CreatedAt = u.CreatedAt
            });
        }

        ViewBag.Search = search;
        ViewBag.SelectedRole = role;
        return View(userItems);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "A user with this email already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            if (!await _roleManager.RoleExistsAsync(model.Role))
            {
                await _roleManager.CreateAsync(new IdentityRole(model.Role));
            }
            await _userManager.AddToRoleAsync(user, model.Role);

            await _auditService.LogAsync("Create User", "User", recordId: user.Id, newValue: $"Created user {user.Email} with role {model.Role}");
            TempData["SuccessMessage"] = $"User {user.Email} created successfully.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var err in result.Errors)
        {
            ModelState.AddModelError(string.Empty, err.Description);
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var model = new EditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = roles.FirstOrDefault() ?? "Sales Executive",
            IsActive = user.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
        {
            return NotFound();
        }

        var oldInfo = $"Name: {user.FullName}, Role: {(await _userManager.GetRolesAsync(user)).FirstOrDefault()}, Active: {user.IsActive}";

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.IsActive = model.IsActive;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var err in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }
            return View(model);
        }

        // Update role
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(model.Role))
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!await _roleManager.RoleExistsAsync(model.Role))
            {
                await _roleManager.CreateAsync(new IdentityRole(model.Role));
            }
            await _userManager.AddToRoleAsync(user, model.Role);
        }

        var newInfo = $"Name: {user.FullName}, Role: {model.Role}, Active: {user.IsActive}";
        await _auditService.LogAsync("Update User", "User", recordId: user.Id, oldValue: oldInfo, newValue: newInfo);

        TempData["SuccessMessage"] = $"User {user.Email} updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Prevent self-deactivation
        var currentAdminId = _userManager.GetUserId(User);
        if (user.Id == currentAdminId)
        {
            TempData["ErrorMessage"] = "You cannot deactivate your own administrative account.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);

        var action = user.IsActive ? "User Activated" : "User Deactivated";
        await _auditService.LogAsync(action, "User", recordId: user.Id, newValue: $"Active={user.IsActive}");

        TempData["SuccessMessage"] = $"User {user.Email} status updated to {(user.IsActive ? "Active" : "Inactive")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        await _auditService.LogAsync("User Unlocked", "User", recordId: user.Id, newValue: "Lockout reset by administrator");
        TempData["SuccessMessage"] = $"User {user.Email} unlocked successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        return View(new ResetPasswordViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
        {
            return NotFound();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

        if (result.Succeeded)
        {
            await _auditService.LogAsync("Reset Password", "User", recordId: user.Id, newValue: "Password reset by admin");
            TempData["SuccessMessage"] = $"Password for {user.Email} has been reset.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var err in result.Errors)
        {
            ModelState.AddModelError(string.Empty, err.Description);
        }
        return View(model);
    }
}
