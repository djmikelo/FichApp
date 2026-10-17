using System.ComponentModel.DataAnnotations;
using System.Data;
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
public class IndexModel(
    ApplicationDbContext db,
    TimeZoneInfo timeZone,
    ILogger<IndexModel> logger) : PageModel
{
    private static readonly IReadOnlyDictionary<AttendanceEventType, (string Label, string CssClass)> ActionDetails =
        new Dictionary<AttendanceEventType, (string, string)>
        {
            [AttendanceEventType.WorkStarted] = ("Iniciar jornada", "button button-primary"),
            [AttendanceEventType.BreakStarted] = ("Iniciar pausa", "button button-secondary"),
            [AttendanceEventType.BreakEnded] = ("Terminar pausa", "button button-primary"),
            [AttendanceEventType.WorkEnded] = ("Finalizar jornada", "button button-danger")
        };

    public string EmployeeName { get; private set; } = "";
    public string TodayLabel { get; private set; } = "";
    public string TimeZoneLabel { get; private set; } = "";
    public string StateLabel { get; private set; } = "Sin jornada iniciada";
    public string StateDescription { get; private set; } = "Registra el inicio cuando empieces a trabajar.";
    public bool IsWorking { get; private set; }
    public string? StatusMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public List<AvailableAction> AvailableActions { get; } = [];
    public List<EventView> RecentEvents { get; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var employeeId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var employee = await db.Users
            .Where(user => user.Id == employeeId)
            .Select(user => new { user.Email })
            .SingleAsync();
        var profile = await db.EmployeeProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == employeeId);
        if (profile?.MustChangePassword == true)
        {
            return RedirectToPage("/ChangePassword");
        }

        EmployeeName = profile?.DisplayName ?? employee.Email ?? "Empleado";
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        TodayLabel = now.ToString("dddd, d 'de' MMMM", CultureInfo.GetCultureInfo("es-ES"));
        TimeZoneLabel = $"Hora local · {timeZone.Id}";

        var lastEvent = await db.AttendanceEvents.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync();

        SetState(lastEvent);
        SetAvailableActions(lastEvent?.Type);

        var recent = await db.AttendanceEvents.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(6)
            .ToListAsync();
        RecentEvents.AddRange(recent.Select(ToEventView));
        StatusMessage = TempData["StatusMessage"] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(AttendanceEventType type)
    {
        if (!ActionDetails.ContainsKey(type))
        {
            return BadRequest();
        }

        var employeeId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var lastEvent = await db.AttendanceEvents
            .Where(item => item.EmployeeId == employeeId)
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync();

        if (!IsValidNextAction(lastEvent?.Type, type))
        {
            ErrorMessage = "Ese fichaje no corresponde al estado actual de tu jornada. Recarga la página y vuelve a intentarlo.";
            await transaction.RollbackAsync();
            await OnGetAsync();
            ErrorMessage = "Ese fichaje no corresponde al estado actual de tu jornada. Recarga la página y vuelve a intentarlo.";
            return Page();
        }

        var user = await db.Users.SingleAsync(item => item.Id == employeeId);
        var profile = await db.EmployeeProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == employeeId);

        db.AttendanceEvents.Add(new AttendanceEvent
        {
            EmployeeId = employeeId,
            EmployeeEmail = user.Email ?? "",
            EmployeeName = profile?.DisplayName ?? user.Email ?? "Empleado",
            Type = type,
            RecordedAtUtc = DateTime.UtcNow,
            TimeZoneId = timeZone.Id
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation("Employee {EmployeeId} recorded attendance event {EventType}", employeeId, type);
        TempData["StatusMessage"] = $"{ActionDetails[type].Label}: fichaje guardado.";
        return RedirectToPage();
    }

    private static bool IsValidNextAction(AttendanceEventType? previous, AttendanceEventType next) =>
        (previous, next) switch
        {
            (null, AttendanceEventType.WorkStarted) => true,
            (AttendanceEventType.WorkEnded, AttendanceEventType.WorkStarted) => true,
            (AttendanceEventType.WorkStarted or AttendanceEventType.BreakEnded, AttendanceEventType.BreakStarted) => true,
            (AttendanceEventType.WorkStarted or AttendanceEventType.BreakEnded, AttendanceEventType.WorkEnded) => true,
            (AttendanceEventType.BreakStarted, AttendanceEventType.BreakEnded) => true,
            _ => false
        };

    private void SetState(AttendanceEvent? lastEvent)
    {
        if (lastEvent is null || lastEvent.Type == AttendanceEventType.WorkEnded)
        {
            StateLabel = "Fuera de jornada";
            StateDescription = "Registra el inicio cuando empieces a trabajar.";
            return;
        }

        if (lastEvent.Type == AttendanceEventType.BreakStarted)
        {
            StateLabel = "En pausa";
            StateDescription = $"Pausa iniciada a las {LocalTime(lastEvent):HH:mm}.";
            return;
        }

        IsWorking = true;
        StateLabel = "Jornada en curso";
        StateDescription = lastEvent.Type == AttendanceEventType.BreakEnded
            ? $"Pausa terminada a las {LocalTime(lastEvent):HH:mm}."
            : $"Jornada iniciada a las {LocalTime(lastEvent):HH:mm}.";
    }

    private void SetAvailableActions(AttendanceEventType? previous)
    {
        var allowed = previous switch
        {
            null or AttendanceEventType.WorkEnded => new[] { AttendanceEventType.WorkStarted },
            AttendanceEventType.WorkStarted or AttendanceEventType.BreakEnded =>
                new[] { AttendanceEventType.BreakStarted, AttendanceEventType.WorkEnded },
            AttendanceEventType.BreakStarted => new[] { AttendanceEventType.BreakEnded },
            _ => Array.Empty<AttendanceEventType>()
        };

        foreach (var action in allowed)
        {
            var details = ActionDetails[action];
            AvailableActions.Add(new AvailableAction(action, details.Label, details.CssClass));
        }
    }

    private EventView ToEventView(AttendanceEvent item)
    {
        var localTime = LocalTime(item);
        return new EventView(
            GetEventLabel(item.Type),
            localTime.ToString("d 'de' MMMM", CultureInfo.GetCultureInfo("es-ES")),
            localTime.ToString("HH:mm:ss", CultureInfo.GetCultureInfo("es-ES")),
            localTime.ToString("O", CultureInfo.InvariantCulture),
            item.Type is AttendanceEventType.WorkStarted or AttendanceEventType.BreakEnded ? "↗" : "↘");
    }

    private DateTimeOffset LocalTime(AttendanceEvent item)
    {
        var eventTimeZone = item.TimeZoneId == timeZone.Id
            ? timeZone
            : TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId);
        var utc = DateTime.SpecifyKind(item.RecordedAtUtc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTime(new DateTimeOffset(utc), eventTimeZone);
    }

    internal static string GetEventLabel(AttendanceEventType type) => type switch
    {
        AttendanceEventType.WorkStarted => "Inicio de jornada",
        AttendanceEventType.BreakStarted => "Inicio de pausa",
        AttendanceEventType.BreakEnded => "Fin de pausa",
        AttendanceEventType.WorkEnded => "Fin de jornada",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public sealed record AvailableAction(AttendanceEventType Type, string Label, string CssClass);
    public sealed record EventView(string Label, string DateLabel, string TimeLabel, string IsoTime, string Marker);
}
