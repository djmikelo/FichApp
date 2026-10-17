using System.Globalization;
using System.Security.Claims;
using FichApp.Data;
using FichApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FichApp.Pages;

[Authorize]
public class RecordsModel(ApplicationDbContext db, TimeZoneInfo timeZone) : PageModel
{
    public DateOnly From { get; private set; }
    public DateOnly To { get; private set; }
    public string? ErrorMessage { get; private set; }
    public List<IndexModel.EventView> Events { get; } = [];

    public async Task<IActionResult> OnGetAsync(DateOnly? from, DateOnly? to)
    {
        var employeeId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var profile = await db.EmployeeProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == employeeId);
        if (profile?.MustChangePassword == true)
        {
            return RedirectToPage("/ChangePassword");
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
        From = from ?? today.AddDays(-30);
        To = to ?? today;

        if (From > To || To.DayNumber - From.DayNumber > 365)
        {
            ErrorMessage = "Selecciona un periodo válido de hasta 366 días.";
            return Page();
        }

        var startUtc = LocalMidnight(From);
        var endUtc = LocalMidnight(To.AddDays(1));
        var records = await db.AttendanceEvents.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId &&
                           item.RecordedAtUtc >= startUtc &&
                           item.RecordedAtUtc < endUtc)
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.Id)
            .ToListAsync();

        Events.AddRange(records.Select(ToEventView));
        return Page();
    }

    private DateTime LocalMidnight(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    private IndexModel.EventView ToEventView(AttendanceEvent item)
    {
        var eventZone = item.TimeZoneId == timeZone.Id ? timeZone : TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId);
        var utc = DateTime.SpecifyKind(item.RecordedAtUtc, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTime(new DateTimeOffset(utc), eventZone);
        return new IndexModel.EventView(
            IndexModel.GetEventLabel(item.Type),
            local.ToString("d 'de' MMMM", CultureInfo.GetCultureInfo("es-ES")),
            local.ToString("HH:mm:ss", CultureInfo.GetCultureInfo("es-ES")),
            local.ToString("O", CultureInfo.InvariantCulture),
            item.Type is AttendanceEventType.WorkStarted or AttendanceEventType.BreakEnded ? "↗" : "↘");
    }
}
