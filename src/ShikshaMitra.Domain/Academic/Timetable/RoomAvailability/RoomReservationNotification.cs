using System;
using System.Text.Json;
using Volo.Abp.Data;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public sealed record RoomReservationNotification(Guid? TenantId, Guid ReservationId, string EventKey, string Revision);

public sealed record RoomReservationNoticeRevision(string EventKey, string Revision, Guid? PreviousTeacherId)
{
    private const string Property = "AcademicRoomReservationNotice";

    public static RoomReservationNoticeRevision? Read(RoomReservation reservation)
    {
        var json = reservation.GetProperty<string>(Property);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<RoomReservationNoticeRevision>(json); }
        catch (JsonException) { return null; }
    }

    internal static RoomReservationNoticeRevision Record(RoomReservation reservation, string eventKey, Guid? previousTeacherId)
    {
        var revision = new RoomReservationNoticeRevision(eventKey, Guid.NewGuid().ToString("N"), previousTeacherId);
        reservation.SetProperty(Property, JsonSerializer.Serialize(revision));
        return revision;
    }

    internal static void Forget(RoomReservation reservation) => reservation.RemoveProperty(Property);
}
