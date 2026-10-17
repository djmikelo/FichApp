using System.Globalization;
using System.Security.Claims;
using System.Text;
using FichApp.Data;
using FichApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FichApp.Pages.Admin;

[Authorize(Roles = "Admin")]
public class ReportModel(ApplicationDbContext db, TimeZoneInfo timeZone) : PageModel
{
    public DateOnly From { get; private set; }
    public DateOnly To { get; private set; }
    public string? ErrorMessage { get; private set; }
    public List<ReportEventView> Events { get; } = [];

    public async Task OnGetAsync(DateOnly? from, DateOnly? to)
    {
        SetRange(from, to);
        if (ErrorMessage is not null)
        {
            return;
        }

        var records = await LoadRecordsAsync();
        Events.AddRange(records.Select(ToView));
    }

    public async Task<IActionResult> OnGetExportAsync(DateOnly? from, DateOnly? to)
    {
        SetRange(from, to);
        if (ErrorMessage is not null)
        {
            return BadRequest(ErrorMessage);
        }

        var records = await LoadRecordsAsync();
        var csv = new StringBuilder();
        csv.AppendLine("Empleado,Correo,Fecha local,Hora local,Acción,Zona horaria,Marca de tiempo UTC");
        foreach (var record in records)
        {
            var local = GetLocalTime(record);
            csv.AppendLine(string.Join(",",
                Csv(record.EmployeeName),
                Csv(record.EmployeeEmail),
                Csv(local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Csv(local.ToString("HH:mm:ss zzz", CultureInfo.InvariantCulture)),
                Csv(IndexModel.GetEventLabel(record.Type)),
                Csv(record.TimeZoneId),
                Csv(DateTime.SpecifyKind(record.RecordedAtUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture))));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"fichajes-{From:yyyyMMdd}-{To:yyyyMMdd}.csv");
    }

    private void SetRange(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
        From = from ?? today;
        To = to ?? today;
        if (From > To || To.DayNumber - From.DayNumber > 365)
        {
            ErrorMessage = "Selecciona un periodo válido de hasta 366 días.";
        }
    }

    private async Task<List<AttendanceEvent>> LoadRecordsAsync()
    {
        var startUtc = LocalMidnight(From);
        var endUtc = LocalMidnight(To.AddDays(1));
        return await db.AttendanceEvents.AsNoTracking()
            .Where(item => item.RecordedAtUtc >= startUtc && item.RecordedAtUtc < endUtc)
            .OrderBy(item => item.RecordedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync();
    }

    private DateTime LocalMidnight(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    private DateTimeOffset GetLocalTime(AttendanceEvent record)
    {
        var eventZone = record.TimeZoneId == timeZone.Id
            ? timeZone
            : TimeZoneInfo.FindSystemTimeZoneById(record.TimeZoneId);
        var utc = DateTime.SpecifyKind(record.RecordedAtUtc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTime(new DateTimeOffset(utc), eventZone);
    }

    private ReportEventView ToView(AttendanceEvent record)
    {
        var local = GetLocalTime(record);
        return new ReportEventView(
            record.EmployeeName,
            record.EmployeeEmail,
            local.ToString("d MMM yyyy", CultureInfo.GetCultureInfo("es-ES")),
            local.ToString("HH:mm:ss", CultureInfo.GetCultureInfo("es-ES")),
            local.ToString("O", CultureInfo.InvariantCulture),
            IndexModel.GetEventLabel(record.Type),
            record.TimeZoneId);
    }

    private static string Csv(string value)
    {
        var firstNonWhitespace = value.AsSpan().TrimStart();
        if (firstNonWhitespace.Length > 0 && firstNonWhitespace[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    public sealed record ReportEventView(
        string EmployeeName,
        string EmployeeEmail,
        string Date,
        string Time,
        string IsoTime,
        string Action,
        string TimeZoneId);
}
