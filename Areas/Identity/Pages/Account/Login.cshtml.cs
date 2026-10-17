using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FichApp.Areas.Identity.Pages.Account;

public class LoginModel(
    SignInManager<IdentityUser> signInManager,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(
            Input.Email.Trim(),
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            logger.LogInformation("User signed in.");
            return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Content("~/"));
        }

        if (result.IsLockedOut)
        {
            ErrorMessage = "La cuenta se ha bloqueado temporalmente por varios intentos fallidos. Prueba más tarde.";
            logger.LogWarning("User account locked out after failed sign-in attempts.");
            return Page();
        }

        ErrorMessage = result.RequiresTwoFactor
            ? "Esta cuenta requiere una configuración de autenticación en dos pasos que no está disponible en esta aplicación."
            : "No se ha podido iniciar sesión. Comprueba el correo y la contraseña.";
        return Page();
    }

    public sealed class LoginInput
    {
        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Introduce un correo electrónico válido.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        public bool RememberMe { get; set; }
    }
}
