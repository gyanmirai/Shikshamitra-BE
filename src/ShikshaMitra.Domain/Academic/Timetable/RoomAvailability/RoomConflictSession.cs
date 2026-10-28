using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public sealed record RoomConflictWindow(Guid RoomId, DateTime Date, TimeSpan StartTime, TimeSpan EndTime,
    Guid? CurrentReservationId = null, Guid? TeacherId = null, Guid? StandardId = null, Guid? DivisionId = null);

public sealed class RoomConflictSession
{
    private readonly HashSet<RoomConflictWindow> _windows;
    private readonly Func<RoomConflictWindow, Task<ConflictCheckResult>> _check;

    internal RoomConflictSession(IEnumerable<RoomConflictWindow> windows, Func<RoomConflictWindow, Task<ConflictCheckResult>> check)
    {
        _windows = new HashSet<RoomConflictWindow>(windows);
        _check = check;
    }

    public Task<ConflictCheckResult> CheckAsync(RoomConflictWindow window)
    {
        if (!_windows.Contains(window)) throw new ArgumentException("The requested window was not loaded in this conflict session.", nameof(window));
        return _check(window);
    }
}
