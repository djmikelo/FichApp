using System.ComponentModel.DataAnnotations;
using FichApp.Data;
using FichApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FichApp.Pages.Admin;

[Authorize(Roles = "Admin")]
public class EmployeesModel(
    UserManager<IdentityUser> userManager,
    ApplicationDbContext db,
    ILogger<EmployeesModel> logger) : PageModel
{
    [BindProperty]
    public NewEmployeeInput NewEmployee { get; set; } = new();

    public List<EmployeeView> Employees { get; private set; } = [];
    public List<string> Errors { get; private set; } = [];
    public string? StatusMessage { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadEmployeesAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            CollectModelErrors();
            await LoadEmployeesAsync();
            return Page();
        }

        var email = NewEmployee.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            Errors.Add("Ya existe una cuenta con ese correo electrónico.");
            await LoadEmployeesAsync();
            return Page();
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await using var transaction = await db.Database.BeginTransactionAsync();
        var createResult = await userManager.CreateAsync(user, NewEmployee.Password);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync();
            Errors.AddRange(createResult.Errors.Select(error => error.Description));
            await LoadEmployeesAsync();
            return Page();
        }

        var roleResult = await userManager.AddToRoleAsync(user, "Employee");
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync();
            Errors.AddRange(roleResult.Errors.Select(error => error.Description));
            await LoadEmployeesAsync();
            return Page();
        }

        db.EmployeeProfiles.Add(new EmployeeProfile
        {
            UserId = user.Id,
            DisplayName = NewEmployee.DisplayName.Trim(),
            MustChangePassword = true
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation("Administrator created an employee account {EmployeeId}", user.Id);
        TempData["StatusMessage"] = "Cuenta creada. Comunica la contraseña inicial al empleado por un canal seguro.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            Errors.Add("La nueva contraseña es obligatoria.");
            await LoadEmployeesAsync();
            return Page();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !await userManager.IsInRoleAsync(user, "Employee"))
        {
            return NotFound();
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, newPassword);
        if (!result.Succeeded)
        {
            Errors.AddRange(result.Errors.Select(error => error.Description));
            await LoadEmployeesAsync();
            return Page();
        }

        var profile = await db.EmployeeProfiles.SingleAsync(item => item.UserId == userId);
        profile.MustChangePassword = true;
        await db.SaveChangesAsync();

        logger.LogInformation("Administrator reset an employee password for {EmployeeId}", user.Id);
        TempData["StatusMessage"] = "Contraseña restablecida. Comunícala al empleado por un canal seguro; deberá cambiarla al entrar.";
        return RedirectToPage();
    }

    private async Task LoadEmployeesAsync()
    {
        var employees = await userManager.GetUsersInRoleAsync("Employee");
        var employeeIds = employees.Select(user => user.Id).ToArray();
        var profiles = await db.EmployeeProfiles.AsNoTracking()
            .Where(profile => employeeIds.Contains(profile.UserId))
            .ToDictionaryAsync(profile => profile.UserId);

        Employees = employees
            .OrderBy(user => user.Email)
            .Select(user =>
            {
                var displayName = profiles.TryGetValue(user.Id, out var profile)
                    ? profile.DisplayName
                    : user.Email ?? "Empleado";
                var initials = string.Join("", displayName
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Take(2)
                    .Select(part => char.ToUpperInvariant(part[0])));
                return new EmployeeView(user.Id, displayName, user.Email ?? "", initials);
            })
            .ToList();

        StatusMessage = TempData["StatusMessage"] as string;
    }

    private void CollectModelErrors()
    {
        Errors.AddRange(ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Revisa los datos introducidos." : error.ErrorMessage));
    }

    public sealed class NewEmployeeInput
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(200)]
        public string DisplayName { get; set; } = "";

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Introduce un correo electrónico válido.")]
        [StringLength(256)]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "La contraseña inicial es obligatoria.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

    public sealed record EmployeeView(string UserId, string DisplayName, string Email, string Initials);
}
