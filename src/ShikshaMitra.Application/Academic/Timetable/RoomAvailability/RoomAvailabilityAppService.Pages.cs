using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomAvailabilityAppService
{
    public async Task<RoomReservationPageResultDto> GetReservationsPageAsync(GetRoomReservationsPageInputDto input)
    {
        var (cohort, visibleUsers) = await ReservationCohortAsync(input.CreatedDate);
        var counts = await ReservationCountsAsync(cohort);
        var rooms = await _roomRepository.GetQueryableAsync();
        var buildings = await _buildingRepository.GetQueryableAsync();
        var audits = await _auditLogRepository.GetQueryableAsync();
        var configs = await _recurringConfigRepository.GetQueryableAsync();
        var query = from reservation in cohort
                    join room in rooms on reservation.RoomId equals room.Id into roomRows
                    from room in roomRows.DefaultIfEmpty()
                    join building in buildings on room.BuildingId equals building.Id into buildingRows
                    from building in buildingRows.DefaultIfEmpty()
                    select new ReservationPageRow
                    {
                        Reservation = reservation,
                        RoomName = room == null ? "" : room.RoomName,
                        BuildingName = building == null ? "" : building.BuildingName,
                        RequestedBy = audits.Where(a => a.ReservationId == reservation.Id && a.Action == "Room Reserved")
                            .Select(a => a.UserName).FirstOrDefault() ?? "",
                        UntilDate = configs.Where(c => c.Id == reservation.RecurringConfigId).Select(c => (DateTime?)c.UntilDate).FirstOrDefault()
                    };
        if (input.RoomId.HasValue) query = query.Where(row => row.Reservation.RoomId == input.RoomId.Value);
        if (!string.IsNullOrWhiteSpace(input.StatusGroup) && input.StatusGroup != "all")
        {
            var statuses = Enum.GetValues<RoomReservationStatus>().Where(status => ReservationStatusKey(status) == input.StatusGroup).ToArray();
            query = query.Where(row => Enumerable.Contains(statuses, row.Reservation.Status));
        }
        query = ApplyReservationTableFilters(query, input);
        var total = await AsyncExecuter.LongCountAsync(query);
        var rows = await AsyncExecuter.ToListAsync(OrderReservations(query, input.Sorting)
            .Skip(input.SkipCount).Take(input.MaxResultCount));
        var actor = CurrentUser.GetId();
        var privileged = IsPrivilegedRoomOperator();
        return new RoomReservationPageResultDto
        {
            TotalCount = total,
            StatusCounts = counts,
            Items = rows.Select(row =>
            {
                var dto = ObjectMapper.Map<RoomReservation, RoomReservationDto>(row.Reservation);
                dto.RoomName = row.RoomName;
                dto.BuildingName = row.BuildingName;
                dto.RequestedBy = row.RequestedBy;
                dto.RecurringUntilDate = row.UntilDate;
                dto.CanReview = dto.Status == RoomReservationStatus.PendingApproval && dto.CreatorId.HasValue &&
                    CanReviewReservation(dto.CreatorId.Value, actor, privileged, visibleUsers);
                return dto;
            }).ToList()
        };
    }

    public async Task<RoomReservationSummaryDto> GetReservationSummaryAsync(GetReservationsInputDto input)
    {
        var (query, _) = await ReservationCohortAsync(input.CreatedDate);
        return new RoomReservationSummaryDto { StatusCounts = await ReservationCountsAsync(query) };
    }

    private async Task<(IQueryable<RoomReservation> Query, HashSet<Guid> VisibleUsers)> ReservationCohortAsync(DateTime? createdDate)
    {
        var query = (await _reservationRepository.GetQueryableAsync()).Where(row => !row.RecurringConfigId.HasValue || row.IsRecurring);
        var scope = await _userScopeAccessService.GetCurrentUserScopeAsync();
        if (!scope.IsAdmin && !scope.HasWildcardAcademicYear)
        {
            var standards = scope.StandardIds.ToArray();
            var divisions = scope.DivisionIds.ToArray();
            var wildcardStandards = scope.WildcardDivisionStandardIds.ToArray();
            var instituteWide = IsPrivilegedRoomOperator() || scope.HasAnyAccess;
            query = query.Where(row => (!row.StandardId.HasValue && !row.DivisionId.HasValue && instituteWide) ||
                (row.StandardId.HasValue && Enumerable.Contains(standards, row.StandardId.Value)) ||
                (row.DivisionId.HasValue && (Enumerable.Contains(divisions, row.DivisionId.Value) ||
                    (scope.HasWildcardDivision && row.StandardId.HasValue && Enumerable.Contains(wildcardStandards, row.StandardId.Value)))));
        }
        if (createdDate.HasValue)
        {
            var date = createdDate.Value.Date;
            query = query.Where(row => row.CreationTime.Date == date);
        }
        var users = await GetVisibleReservationUserIdsAsync(CurrentUser.GetId());
        if (!IsAdministrator() && !await HasClassTeacherAccessAsync())
        {
            var ids = users.ToArray();
            query = query.Where(row => row.CreatorId.HasValue && Enumerable.Contains(ids, row.CreatorId.Value));
        }
        return (query, users);
    }

    private async Task<Dictionary<string, int>> ReservationCountsAsync(IQueryable<RoomReservation> query)
    {
        var grouped = await AsyncExecuter.ToListAsync(query.GroupBy(row => row.Status).Select(group => new { Status = group.Key, Count = group.Count() }));
        var counts = new Dictionary<string, int>();
        foreach (var key in new[] { "all", "draft", "active", "pendingApproval", "approved", "completed", "cancelled", "rejected", "checkedIn", "noShow", "expired" }) counts[key] = 0;
        foreach (var group in grouped)
        {
            counts["all"] += group.Count;
            counts[ReservationStatusKey(group.Status)] += group.Count;
        }
        return counts;
    }

    private static string ReservationStatusKey(RoomReservationStatus status) => status switch
    {
        RoomReservationStatus.Draft => "draft",
        RoomReservationStatus.PendingApproval => "pendingApproval",
        RoomReservationStatus.Cancelled => "cancelled",
        RoomReservationStatus.Rejected => "rejected",
        _ => "active"
    };

    public async Task<PagedResultDto<RoomAuditLogDto>> GetAuditLogsPageAsync(GetRoomAuditLogsInputDto input)
    {
        var query = await _auditLogRepository.GetQueryableAsync();
        if (input.RoomId.HasValue) query = query.Where(row => row.RoomId == input.RoomId.Value);
        if (input.FromDate.HasValue) query = query.Where(row => row.Timestamp >= input.FromDate.Value);
        if (input.ToDate.HasValue) query = query.Where(row => row.Timestamp <= input.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(input.Action)) query = query.Where(row => row.Action == input.Action);
        var rooms = await _roomRepository.GetQueryableAsync();
        var withNames = from log in query
                        join room in rooms on log.RoomId equals room.Id into roomRows
                        from room in roomRows.DefaultIfEmpty()
                        select new { Log = log, RoomName = room == null ? "Unknown Room" : room.RoomName };
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var search = input.SearchTerm.ToLowerInvariant();
            withNames = withNames.Where(row => row.Log.Action.ToLower().Contains(search) || row.Log.UserName.ToLower().Contains(search) ||
                row.RoomName.ToLower().Contains(search) || (row.Log.Remarks ?? "").ToLower().Contains(search) ||
                (row.Log.NewValue ?? "").ToLower().Contains(search));
        }
        var count = await AsyncExecuter.LongCountAsync(withNames);
        var page = await AsyncExecuter.ToListAsync(withNames.OrderByDescending(row => row.Log.Timestamp).ThenByDescending(row => row.Log.Id)
            .Skip(input.SkipCount).Take(input.MaxResultCount));
        var items = page.Select(row =>
        {
            var dto = ObjectMapper.Map<RoomAuditLog, RoomAuditLogDto>(row.Log);
            dto.RoomName = row.RoomName;
            return dto;
        }).ToList();
        return new PagedResultDto<RoomAuditLogDto>(count, items);
    }

    private sealed class ReservationPageRow
    {
        public RoomReservation Reservation { get; set; } = null!;
        public string RoomName { get; set; } = "";
        public string BuildingName { get; set; } = "";
        public string RequestedBy { get; set; } = "";
        public DateTime? UntilDate { get; set; }
    }
}
