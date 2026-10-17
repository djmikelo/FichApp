using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FichApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FichApp.Pages;

[Authorize]
public class ChangePasswordModel(
    UserManager<IdentityUser> userManager,
    ApplicationDbContext db) : PageModel
{
    [BindProperty]
    public PasswordInput Input { get; set; } = new();

    public List<string> Errors { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            CollectErrors();
            return Page();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("The signed-in user no longer exists.");
        var result = await userManager.ChangePasswordAsync(user, Input.CurrentPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            Errors.AddRange(result.Errors.Select(error => error.Description));
            return Page();
        }

        var profile = await GetProfileAsync();
        if (profile is not null)
        {
            profile.MustChangePassword = false;
            await db.SaveChangesAsync();
        }

        return RedirectToPage("/Index");
    }

    private Task<Models.EmployeeProfile?> GetProfileAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return db.EmployeeProfiles.SingleOrDefaultAsync(profile => profile.UserId == userId);
    }

    private void CollectErrors()
    {
        Errors.AddRange(ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Revisa los datos introducidos." : error.ErrorMessage));
    }

    public sealed class PasswordInput
    {
        [Required]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = "";

        [Required]
        [StringLength(100, MinimumLength = 12)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = "";
    }
}
