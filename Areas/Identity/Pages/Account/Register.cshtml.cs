using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FichApp.Areas.Identity.Pages.Account;

public class RegisterDisabledModel : PageModel
{
    public IActionResult OnGet() => NotFound();
    public IActionResult OnPost() => NotFound();
}
