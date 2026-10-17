namespace FichApp.Models;

public enum AttendanceEventType
{
    WorkStarted,
    BreakStarted,
    BreakEnded,
    WorkEnded
}

public sealed class AttendanceEvent
{
    public long Id { get; set; }
    public required string EmployeeId { get; set; }
    public required string EmployeeEmail { get; set; }
    public required string EmployeeName { get; set; }
    public AttendanceEventType Type { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public required string TimeZoneId { get; set; }
}

public sealed class EmployeeProfile
{
    public required string UserId { get; set; }
    public required string DisplayName { get; set; }
    public bool MustChangePassword { get; set; }
}
