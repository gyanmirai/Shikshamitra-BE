using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomConflictService
{
    private async Task<List<RoomDivisionRead>> ReadRoomDivisionsAsync(IQueryable<Division> query) =>
        await AsyncExecuter.ToListAsync(query.Select(division => new RoomDivisionRead(
            division.Id, division.StandardId, division.Name, division.RoomNumber)));

    private async Task<Dictionary<Guid, RoomStandardRead>> ReadRoomStandardsAsync(IReadOnlyCollection<Guid> standardIds)
    {
        if (standardIds.Count == 0) return new();
        var rows = await AsyncExecuter.ToListAsync((await _standardRepository.GetQueryableAsync())
            .Where(standard => Enumerable.Contains(standardIds, standard.Id))
            .Select(standard => new RoomStandardRead(standard.Id, standard.Name,
                standard.Shift == null ? null : standard.Shift.ShiftName,
                standard.Shift == null ? null : (TimeOnly?)standard.Shift.StartTime,
                standard.Shift == null ? null : (TimeOnly?)standard.Shift.EndTime)));
        return rows.ToDictionary(standard => standard.Id);
    }

    private sealed record RoomDivisionRead(Guid Id, Guid StandardId, string Name, string? RoomNumber);
    private sealed record RoomStandardRead(Guid Id, string Name, string? ShiftName, TimeOnly? ShiftStart, TimeOnly? ShiftEnd);
}
