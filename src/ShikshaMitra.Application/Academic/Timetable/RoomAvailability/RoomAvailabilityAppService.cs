using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using ShikshaMitra.HR.Employees;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

[Authorize]
public class RoomAvailabilityAppService : ApplicationService, IRoomAvailabilityAppService
{
    private readonly IRepository<Room, Guid> _roomRepository;
    private readonly IRepository<RoomReservation, Guid> _reservationRepository;
    private readonly IRepository<RoomRecurringConfig, Guid> _recurringConfigRepository;
    private readonly IRepository<RoomMaintenanceBlock, Guid> _maintenanceRepository;
    private readonly IRepository<RoomAuditLog, Guid> _auditLogRepository;
    private readonly IRepository<TenantSettings.RoomsManagement.Buildings.Building, Guid> _buildingRepository;
    private readonly IRepository<TenantSettings.RoomsManagement.Buildings.Floor, Guid> _floorRepository;
    private readonly IRepository<TenantSettings.RoomsManagement.RoomTypes.RoomType, Guid> _roomTypeRepository;
    private readonly RoomConflictService _conflictService;
    private readonly RoomRecommendationService _recommendationService;
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public RoomAvailabilityAppService(
        IRepository<Room, Guid> roomRepository,
        IRepository<RoomReservation, Guid> reservationRepository,
        IRepository<RoomRecurringConfig, Guid> recurringConfigRepository,
        IRepository<RoomMaintenanceBlock, Guid> maintenanceRepository,
        IRepository<RoomAuditLog, Guid> auditLogRepository,
        IRepository<TenantSettings.RoomsManagement.Buildings.Building, Guid> buildingRepository,
        IRepository<TenantSettings.RoomsManagement.Buildings.Floor, Guid> floorRepository,
        IRepository<TenantSettings.RoomsManagement.RoomTypes.RoomType, Guid> roomTypeRepository,
        RoomConflictService conflictService,
        RoomRecommendationService recommendationService,
        IRepository<Employee, Guid> employeeRepository)
    {
        _roomRepository = roomRepository;
        _reservationRepository = reservationRepository;
        _recurringConfigRepository = recurringConfigRepository;
        _maintenanceRepository = maintenanceRepository;
        _auditLogRepository = auditLogRepository;
        _buildingRepository = buildingRepository;
        _floorRepository = floorRepository;
        _roomTypeRepository = roomTypeRepository;
        _conflictService = conflictService;
        _recommendationService = recommendationService;
        _employeeRepository = employeeRepository;
    }

    public async Task<List<RoomAvailabilityDto>> GetAvailabilityAsync(RoomAvailabilityFilterDto input)
    {
        var rooms = await _roomRepository.GetListAsync();
        var buildings = await _buildingRepository.GetListAsync();
        var floors = await _floorRepository.GetListAsync();
        var roomTypes = await _roomTypeRepository.GetListAsync();

        var buildingLookup = buildings.ToDictionary(b => b.Id, b => b.BuildingName);
        var floorLookup = floors.ToDictionary(f => f.Id, f => f.FloorName);
        var roomTypeLookup = roomTypes.ToDictionary(t => t.Id, t => t.RoomTypeName);

        rooms = ApplyRoomMasterFilters(rooms, input, buildingLookup, floorLookup, roomTypeLookup)
            .OrderByDescending(r => r.CreationTime)
            .ThenBy(r => r.RoomName)
            .ToList();

        var result = new List<RoomAvailabilityDto>();

        var timeStart = input.StartTime ?? new TimeSpan(8, 0, 0);
        var timeEnd = input.EndTime ?? new TimeSpan(16, 0, 0);

        foreach (var room in rooms)
        {
            var conflictCheck = await _conflictService.CheckConflictsAsync(
                room.Id,
                input.Date,
                timeStart,
                timeEnd,
                standardId: input.StandardId,
                divisionId: input.DivisionId
            );

            var status = "available";
            string? occupiedBy = null;
            string? reason = null;

            if (conflictCheck.OverallSeverity == "critical")
            {
                var block = conflictCheck.Conflicts.FirstOrDefault();
                reason = block?.Message;
                status = block?.Type == "maintenanceCheck" ? "maintenance" : "occupied";
                occupiedBy = block?.Message;
            }

            var buildingName = buildingLookup.TryGetValue(room.BuildingId, out var bName) ? bName : "Main Building";
            var floorName = floorLookup.TryGetValue(room.FloorId, out var fName) ? fName : "First Floor";
            var typeName = roomTypeLookup.TryGetValue(room.RoomTypeId, out var tName) ? tName : "Classroom";

            var dto = new RoomAvailabilityDto
            {
                RoomId = room.Id,
                RoomName = room.RoomName,
                BuildingName = buildingName,
                FloorName = floorName,
                Type = typeName,
                Capacity = room.Capacity,
                Facilities = room.Features ?? new List<string>(),
                Status = status,
                OccupiedBy = occupiedBy,
                ConflictReason = reason,
                ConflictSeverity = conflictCheck.OverallSeverity,
                Source = ResolveAvailabilitySource(conflictCheck.Conflicts.FirstOrDefault()),
                NextAvailableAt = ResolveNextAvailableAt(conflictCheck.Conflicts.FirstOrDefault()),
                UtilizationPercentage = new Random().Next(40, 85)
            };

            result.Add(dto);
        }

        return input.IncludeOccupied
            ? result
            : result.Where(r => r.Status == "available").ToList();
    }

    private static IEnumerable<Room> ApplyRoomMasterFilters(
        IEnumerable<Room> rooms,
        RoomAvailabilityFilterDto input,
        IReadOnlyDictionary<Guid, string> buildingLookup,
        IReadOnlyDictionary<Guid, string> floorLookup,
        IReadOnlyDictionary<Guid, string> roomTypeLookup)
    {
        var query = rooms;

        if (input.BuildingId.HasValue)
        {
            query = query.Where(r => r.BuildingId == input.BuildingId.Value);
        }

        if (input.FloorId.HasValue)
        {
            query = query.Where(r => r.FloorId == input.FloorId.Value);
        }

        if (input.RoomTypeId.HasValue)
        {
            query = query.Where(r => r.RoomTypeId == input.RoomTypeId.Value);
        }

        if (input.MinimumCapacity.HasValue && input.MinimumCapacity.Value > 0)
        {
            query = query.Where(r => r.Capacity >= input.MinimumCapacity.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.Trim();
            query = query.Where(r =>
                ContainsIgnoreCase(r.RoomName, search) ||
                ContainsIgnoreCase(BuildingName(r, buildingLookup), search) ||
                ContainsIgnoreCase(FloorName(r, floorLookup), search) ||
                ContainsIgnoreCase(RoomTypeName(r, roomTypeLookup), search) ||
                r.Features?.Any(feature => ContainsIgnoreCase(feature, search)) == true);
        }

        return query;
    }

    private static string BuildingName(Room room, IReadOnlyDictionary<Guid, string> lookup) =>
        lookup.TryGetValue(room.BuildingId, out var name) ? name : string.Empty;

    private static string FloorName(Room room, IReadOnlyDictionary<Guid, string> lookup) =>
        lookup.TryGetValue(room.FloorId, out var name) ? name : string.Empty;

    private static string RoomTypeName(Room room, IReadOnlyDictionary<Guid, string> lookup) =>
        lookup.TryGetValue(room.RoomTypeId, out var name) ? name : string.Empty;

    private static bool ContainsIgnoreCase(string? value, string search) =>
        value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

    public async Task<ConflictCheckResultDto> CheckConflictsAsync(CheckConflictRequestDto input)
    {
        var result = await _conflictService.CheckConflictsAsync(
            input.RoomId,
            input.Date,
            input.StartTime,
            input.EndTime,
            null,
            input.TeacherId,
            input.StandardId,
            input.DivisionId
        );

        return ObjectMapper.Map<ConflictCheckResult, ConflictCheckResultDto>(result);
    }

    public async Task<List<SuggestedRoomDto>> SuggestRoomsAsync(SuggestedRoomRequestDto input)
    {
        var result = await _recommendationService.SuggestRoomsAsync(
            input.Date,
            input.StartTime,
            input.EndTime,
            input.RequiredCapacity,
            input.RequiredFacilities,
            input.PreferredBuildingId,
            input.StandardId,
            input.Page,
            input.Limit
        );

        return ObjectMapper.Map<List<SuggestedRoom>, List<SuggestedRoomDto>>(result);
    }

    public async Task<RoomReservationDto> CreateReservationAsync(CreateRoomReservationDto input)
    {
        var conflicts = await _conflictService.CheckConflictsAsync(
            input.RoomId, input.Date, input.StartTime, input.EndTime, null,
            input.TeacherId, input.StandardId, input.DivisionId
        );

        EnsureNoCriticalConflicts(conflicts, "Cannot complete booking");

        var isAdmin = CurrentUser.Roles.Any(role => string.Equals(role, "administrator", StringComparison.OrdinalIgnoreCase));
        var approverId = CurrentUser.Id ?? Guid.Empty;

        Guid? configId = null;

        if (input.IsRecurring && input.RecurringConfig != null)
        {
            var config = new RoomRecurringConfig(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                input.RecurringConfig.RepeatType,
                input.RecurringConfig.UntilDate,
                input.RecurringConfig.RepeatDays != null ? JsonSerializer.Serialize(input.RecurringConfig.RepeatDays) : null,
                null,
                input.RecurringConfig.ExcludeHolidays,
                false,
                input.RecurringConfig.SkipConflictDates
            );
            await _recurringConfigRepository.InsertAsync(config);
            configId = config.Id;

            var dates = GenerateRecurringDates(input.Date, input.RecurringConfig);
            foreach (var date in dates)
            {
                var stepConflict = await _conflictService.CheckConflictsAsync(
                    input.RoomId, date, input.StartTime, input.EndTime, null,
                    input.TeacherId, input.StandardId, input.DivisionId
                );

                if (stepConflict.OverallSeverity == "critical" && input.RecurringConfig.SkipConflictDates)
                {
                    continue;
                }

                EnsureNoCriticalConflicts(stepConflict, $"Cannot create recurring booking for {date:yyyy-MM-dd}");

                var subReservation = new RoomReservation(
                    GuidGenerator.Create(), CurrentTenant.Id, input.RoomId, date,
                    input.StartTime, input.EndTime, input.Purpose, input.ReservationType,
                    input.TeacherId, input.StandardId, input.DivisionId, input.SubjectId,
                    input.ReservedFor, input.Remarks, false, configId
                );

                if (isAdmin)
                {
                    subReservation.Approve(approverId);
                }

                await _reservationRepository.InsertAsync(subReservation);
            }
        }

        var mainReservation = new RoomReservation(
            GuidGenerator.Create(), CurrentTenant.Id, input.RoomId, input.Date,
            input.StartTime, input.EndTime, input.Purpose, input.ReservationType,
            input.TeacherId, input.StandardId, input.DivisionId, input.SubjectId,
            input.ReservedFor, input.Remarks, input.IsRecurring, configId
        );

        if (isAdmin)
        {
            mainReservation.Approve(approverId);
        }

        await _reservationRepository.InsertAsync(mainReservation);

        var creationAuditLog = new RoomAuditLog(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            input.RoomId,
            mainReservation.Id,
            "Room Reserved",
            CurrentUser.Id ?? Guid.Empty,
            CurrentUser.UserName ?? "System",
            null,
            null,
            $"Room reserved for '{input.Purpose}'."
        );
        await _auditLogRepository.InsertAsync(creationAuditLog);

        return ObjectMapper.Map<RoomReservation, RoomReservationDto>(mainReservation);
    }

    private List<DateTime> GenerateRecurringDates(DateTime start, RecurringConfigDto config)
    {
        var dates = new List<DateTime>();
        var current = start;

        if (config.RepeatType == "daily")
        {
            while (current.AddDays(1) <= config.UntilDate)
            {
                current = current.AddDays(1);
                dates.Add(current);
            }
        }
        else if (config.RepeatType == "weekly" && config.RepeatDays != null)
        {
            while (current.AddDays(1) <= config.UntilDate)
            {
                current = current.AddDays(1);
                var abpDayOfWeekIndex = (int) current.DayOfWeek;
                if (config.RepeatDays.Contains(abpDayOfWeekIndex))
                {
                    dates.Add(current);
                }
            }
        }

        return dates;
    }

    public async Task<List<RoomReservationDto>> GetReservationsAsync(GetReservationsInputDto input)
    {
        var reservations = await _reservationRepository.GetListAsync(r =>
            !r.RecurringConfigId.HasValue || r.IsRecurring);

        if (input.CreatedDate.HasValue)
        {
            var date = input.CreatedDate.Value.Date;
            reservations = reservations.Where(r => r.CreationTime.Date == date).ToList();
        }

        var currentUserId = CurrentUser.GetId();
        var isAdmin = CurrentUser.Roles.Any(role => string.Equals(role, "administrator", StringComparison.OrdinalIgnoreCase));

        if (!isAdmin)
        {
            if (input.IsApprovalView)
            {
                var currentEmployee = await _employeeRepository.FirstOrDefaultAsync(x => x.UserId == currentUserId);
                if (currentEmployee != null)
                {
                    var reportingEmployeeIds = await GetReportingEmployeesAsync(currentEmployee.Id);
                    reservations = reservations.Where(r => r.TeacherId.HasValue && reportingEmployeeIds.Contains(r.TeacherId.Value)).ToList();
                }
                else
                {
                    reservations = new List<RoomReservation>();
                }
            }
            else
            {
                reservations = reservations.Where(r => r.CreatorId == currentUserId).ToList();
            }
        }

        var dtos = ObjectMapper.Map<List<RoomReservation>, List<RoomReservationDto>>(reservations);

        // Enrich with room names and building names
        var roomIds = dtos.Select(d => d.RoomId).Distinct().ToList();
        if (roomIds.Count > 0)
        {
            var rooms = await _roomRepository.GetListAsync(r => roomIds.Contains(r.Id));
            var buildings = await _buildingRepository.GetListAsync();
            var buildingLookup = buildings.ToDictionary(b => b.Id, b => b.BuildingName);
            var roomLookup = rooms.ToDictionary(r => r.Id, r => (r.RoomName, r.BuildingId));
            foreach (var dto in dtos)
            {
                if (roomLookup.TryGetValue(dto.RoomId, out var roomInfo))
                {
                    dto.RoomName = roomInfo.RoomName;
                    dto.BuildingName = buildingLookup.TryGetValue(roomInfo.BuildingId, out var bName)
                        ? bName
                        : string.Empty;
                }
            }
        }

        // Enrich with requester name from creation audit logs
        var reservationIds = dtos.Select(d => d.Id).ToList();
        if (reservationIds.Count > 0)
        {
            var createLogs = await _auditLogRepository.GetListAsync(l =>
                l.ReservationId.HasValue &&
                reservationIds.Contains(l.ReservationId.Value) &&
                l.Action == "Room Reserved");
            var creatorByReservationId = createLogs
                .GroupBy(l => l.ReservationId!.Value)
                .ToDictionary(g => g.Key, g => g.First().UserName);
            foreach (var dto in dtos)
            {
                if (creatorByReservationId.TryGetValue(dto.Id, out var creatorName))
                {
                    dto.RequestedBy = creatorName;
                }
            }
        }

        var configIds = dtos.Where(d => d.RecurringConfigId.HasValue).Select(d => d.RecurringConfigId.Value).Distinct().ToList();
        if (configIds.Any())
        {
            var configs = await _recurringConfigRepository.GetListAsync(c => configIds.Contains(c.Id));
            var configDict = configs.ToDictionary(c => c.Id, c => c.UntilDate);
            foreach (var dto in dtos)
            {
                if (dto.RecurringConfigId.HasValue && configDict.TryGetValue(dto.RecurringConfigId.Value, out var untilDate))
                {
                    dto.RecurringUntilDate = untilDate;
                }
            }
        }

        return dtos;
    }

    public async Task<RoomReservationDto> UpdateReservationAsync(Guid id, CreateRoomReservationDto input)
    {
        var reservation = await _reservationRepository.GetAsync(id);

        var conflicts = await _conflictService.CheckConflictsAsync(
            input.RoomId, input.Date, input.StartTime, input.EndTime, id,
            input.TeacherId, input.StandardId, input.DivisionId
        );

        if (conflicts.OverallSeverity == "critical")
        {
            throw new Volo.Abp.UserFriendlyException("Cannot update booking: Critical conflicts detected.");
        }

        var oldValJson = JsonSerializer.Serialize(reservation);

        reservation.Update(
            input.RoomId,
            input.Date,
            input.StartTime,
            input.EndTime,
            input.Purpose,
            input.ReservationType,
            input.TeacherId,
            input.StandardId,
            input.DivisionId,
            input.SubjectId,
            input.ReservedFor,
            input.Remarks
        );

        await _reservationRepository.UpdateAsync(reservation);

        var newValJson = JsonSerializer.Serialize(reservation);

        // Log audit log
        var auditLog = new RoomAuditLog(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            input.RoomId,
            id,
            "Updated",
            CurrentUser.Id ?? Guid.Empty,
            CurrentUser.UserName ?? "System",
            oldValJson,
            newValJson,
            "Reservation updated via App Service."
        );
        await _auditLogRepository.InsertAsync(auditLog);

        return ObjectMapper.Map<RoomReservation, RoomReservationDto>(reservation);
    }

    public async Task CancelReservationAsync(Guid id, string reason)
    {
        var reservation = await _reservationRepository.GetAsync(id);
        var oldValJson = JsonSerializer.Serialize(reservation);

        reservation.Cancel(CurrentUser.Id ?? Guid.Empty, reason);
        await _reservationRepository.UpdateAsync(reservation);

        if (reservation.IsRecurring && reservation.RecurringConfigId.HasValue)
        {
            var children = await _reservationRepository.GetListAsync(r =>
                r.RecurringConfigId == reservation.RecurringConfigId.Value &&
                !r.IsRecurring);
            foreach (var child in children)
            {
                child.Cancel(CurrentUser.Id ?? Guid.Empty, reason);
                await _reservationRepository.UpdateAsync(child);
            }
        }

        var newValJson = JsonSerializer.Serialize(reservation);

        var auditLog = new RoomAuditLog(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            reservation.RoomId,
            id,
            "Cancelled",
            CurrentUser.Id ?? Guid.Empty,
            CurrentUser.UserName ?? "System",
            oldValJson,
            newValJson,
            $"Reservation cancelled: {reason}"
        );
        await _auditLogRepository.InsertAsync(auditLog);
    }

    private void EnsureUserIsAuthorizedApprover(RoomReservation reservation)
    {
        var isAuthorized = CurrentUser.IsInRole("admin") ||
                           CurrentUser.IsInRole("Administrator") ||
                           CurrentUser.IsInRole("coordinator") ||
                           CurrentUser.IsInRole("Coordinator") ||
                           CurrentUser.IsInRole("HOD") ||
                           CurrentUser.IsInRole("hod") ||
                           CurrentUser.IsInRole("Principal") ||
                           CurrentUser.IsInRole("principal") ||
                           CurrentUser.IsInRole("Reporting Manager");

        if (!isAuthorized)
        {
            throw new Volo.Abp.Authorization.AbpAuthorizationException("You are not authorized to approve or reject room reservations.");
        }

        if (reservation.CreatorId.HasValue && reservation.CreatorId.Value == CurrentUser.Id)
        {
            throw new Volo.Abp.UserFriendlyException("Self-approval is not allowed. You cannot approve or reject your own room reservation requests.");
        }
    }

    public async Task ApproveReservationAsync(Guid id)
    {
        var reservation = await _reservationRepository.GetAsync(id);
        EnsureUserIsAuthorizedApprover(reservation);
        var oldValJson = JsonSerializer.Serialize(reservation);

        await EnsureReservationCanBeApprovedAsync(reservation);

        reservation.Approve(CurrentUser.Id ?? Guid.Empty);
        await _reservationRepository.UpdateAsync(reservation);

        if (reservation.IsRecurring && reservation.RecurringConfigId.HasValue)
        {
            var children = await _reservationRepository.GetListAsync(r =>
                r.RecurringConfigId == reservation.RecurringConfigId.Value &&
                !r.IsRecurring);
            foreach (var child in children)
            {
                await EnsureReservationCanBeApprovedAsync(child);
                child.Approve(CurrentUser.Id ?? Guid.Empty);
                await _reservationRepository.UpdateAsync(child);
            }
        }

        var newValJson = JsonSerializer.Serialize(reservation);

        var auditLog = new RoomAuditLog(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            reservation.RoomId,
            id,
            "Approved",
            CurrentUser.Id ?? Guid.Empty,
            CurrentUser.UserName ?? "System",
            oldValJson,
            newValJson,
            "Reservation approved."
        );
        await _auditLogRepository.InsertAsync(auditLog);
    }

    private async Task EnsureReservationCanBeApprovedAsync(RoomReservation reservation)
    {
        var conflicts = await _conflictService.CheckConflictsAsync(
            reservation.RoomId,
            reservation.Date,
            reservation.StartTime,
            reservation.EndTime,
            reservation.Id,
            reservation.TeacherId,
            reservation.StandardId,
            reservation.DivisionId
        );

        EnsureNoCriticalConflicts(conflicts, "Cannot approve booking");
    }

    private static void EnsureNoCriticalConflicts(ConflictCheckResult conflicts, string action)
    {
        if (conflicts.OverallSeverity != "critical")
        {
            return;
        }

        var criticalMessages = conflicts.Conflicts
            .Where(conflict => conflict.Severity == "critical")
            .Select(conflict => conflict.Message)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct()
            .ToList();

        var detail = criticalMessages.Count > 0
            ? string.Join(" ", criticalMessages)
            : "Critical conflicts detected.";

        throw new Volo.Abp.UserFriendlyException($"{action}: {detail}");
    }

    public async Task UpdateReservationStatusAsync(Guid id, UpdateRoomReservationStatusDto input)
    {
        var requestedStatus = (input.ApprovalStatus ?? input.Status)?.Trim();

        if (requestedStatus == null)
        {
            throw new Volo.Abp.UserFriendlyException("Reservation status is required.");
        }

        if (requestedStatus.Equals("rejected", StringComparison.OrdinalIgnoreCase) ||
            requestedStatus.Equals("reject", StringComparison.OrdinalIgnoreCase))
        {
            await RejectReservationAsync(id, input.Remarks ?? input.Reason ?? string.Empty);
            return;
        }

        if (requestedStatus.Equals("approved", StringComparison.OrdinalIgnoreCase) ||
            requestedStatus.Equals("approve", StringComparison.OrdinalIgnoreCase))
        {
            await ApproveReservationAsync(id);
            return;
        }

        throw new Volo.Abp.UserFriendlyException($"Unsupported reservation status '{requestedStatus}'.");
    }

    public async Task RejectReservationAsync(Guid id, string reason)
    {
        var normalizedReason = NormalizeRequiredReason(reason, "Rejection reason is required.");
        var reservation = await _reservationRepository.GetAsync(id);
        EnsureUserIsAuthorizedApprover(reservation);
        var oldValJson = JsonSerializer.Serialize(reservation);

        reservation.Reject(CurrentUser.Id ?? Guid.Empty, normalizedReason);
        await _reservationRepository.UpdateAsync(reservation);

        if (reservation.IsRecurring && reservation.RecurringConfigId.HasValue)
        {
            var children = await _reservationRepository.GetListAsync(r =>
                r.RecurringConfigId == reservation.RecurringConfigId.Value &&
                !r.IsRecurring);
            foreach (var child in children)
            {
                child.Reject(CurrentUser.Id ?? Guid.Empty, normalizedReason);
                await _reservationRepository.UpdateAsync(child);
            }
        }

        var newValJson = JsonSerializer.Serialize(reservation);

        var auditLog = new RoomAuditLog(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            reservation.RoomId,
            id,
            "Rejected",
            CurrentUser.Id ?? Guid.Empty,
            CurrentUser.UserName ?? "System",
            oldValJson,
            newValJson,
            $"Reservation rejected: {normalizedReason}"
        );
        await _auditLogRepository.InsertAsync(auditLog);
    }

    private static string NormalizeRequiredReason(string? reason, string errorMessage)
    {
        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            throw new Volo.Abp.UserFriendlyException(errorMessage);
        }

        return normalizedReason.Length <= 500
            ? normalizedReason
            : normalizedReason[..500];
    }

    public async Task<RoomMaintenanceBlockDto> CreateMaintenanceBlockAsync(CreateMaintenanceBlockDto input)
    {
        var block = new RoomMaintenanceBlock(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            input.RoomId,
            input.Title,
            input.Reason,
            input.Severity,
            input.StartDateTime,
            input.EndDateTime,
            input.AssignedStaff,
            input.Remarks
        );

        await _maintenanceRepository.InsertAsync(block);

        var room = await _roomRepository.GetAsync(input.RoomId);
        var dto = ObjectMapper.Map<RoomMaintenanceBlock, RoomMaintenanceBlockDto>(block);
        dto.RoomName = room?.RoomName ?? "Unknown Room";
        return dto;
    }

    public async Task<List<RoomAuditLogDto>> GetAuditLogsAsync(Guid? roomId)
    {
        var logs = await _auditLogRepository.GetListAsync(l => !roomId.HasValue || l.RoomId == roomId.Value);
        var ordered = logs.OrderByDescending(l => l.Timestamp).ToList();
        var dtos = ObjectMapper.Map<List<RoomAuditLog>, List<RoomAuditLogDto>>(ordered);

        var roomIds = ordered.Select(l => l.RoomId).Distinct().ToList();
        var rooms = await _roomRepository.GetListAsync(r => roomIds.Contains(r.Id));
        var roomNamesById = rooms.ToDictionary(r => r.Id, r => r.RoomName);

        foreach (var dto in dtos)
        {
            dto.RoomName = roomNamesById.TryGetValue(dto.RoomId, out var name) ? name : "Unknown Room";
        }

        return dtos;
    }

    public async Task<List<RoomUsageReportDto>> GetUsageReportAsync()
    {
        var rooms = await _roomRepository.GetListAsync();
        var reservations = await _reservationRepository.GetListAsync();
        var maintenance = await _maintenanceRepository.GetListAsync();

        var reports = new List<RoomUsageReportDto>();

        foreach (var room in rooms)
        {
            var resCount = reservations.Count(r => r.RoomId == room.Id);
            var maintCount = maintenance.Count(m => m.RoomId == room.Id);
            var cancelCount = reservations.Count(r => r.RoomId == room.Id && r.Status == RoomReservationStatus.Cancelled);

            var totalPeriods = 40;
            var occupiedPeriods = resCount * 2 + maintCount * 4;
            occupiedPeriods = Math.Clamp(occupiedPeriods, 0, totalPeriods);
            var freePeriods = totalPeriods - occupiedPeriods;

            reports.Add(new RoomUsageReportDto
            {
                RoomId = room.Id,
                RoomName = room.RoomName,
                BuildingName = "Main Building",
                Type = "Classroom",
                Capacity = room.Capacity,
                TotalPeriods = totalPeriods,
                OccupiedPeriods = occupiedPeriods,
                FreePeriods = freePeriods,
                UtilizationPercentage = (occupiedPeriods * 100) / totalPeriods,
                ReservationCount = resCount,
                MaintenanceCount = maintCount,
                ConflictCount = new Random().Next(0, 3),
                CancellationCount = cancelCount,
                MainUsageType = resCount > maintCount ? "Academic Lectures" : "Maintenance / Repairs"
            });
        }

        return reports;
    }

    public async Task<RoomScheduleDto> GetRoomScheduleAsync(Guid roomId, DateTime date)
    {
        var room = await _roomRepository.GetAsync(roomId);
        var reservations = await _reservationRepository.GetListAsync(r =>
            r.RoomId == roomId &&
            r.Date == date.Date &&
            (r.Status == RoomReservationStatus.Approved || r.Status == RoomReservationStatus.Active || r.Status == RoomReservationStatus.CheckedIn || r.Status == RoomReservationStatus.PendingApproval));

        var schedule = new RoomScheduleDto
        {
            RoomId = roomId,
            RoomName = room?.RoomName ?? "Unknown Room",
            Date = date
        };

        var slotsConfig = new[]
        {
            new { Period = "P1", Time = "06:00 AM - 07:00 AM", Start = new TimeSpan(6,0,0), End = new TimeSpan(7,0,0) },
            new { Period = "P2", Time = "07:00 AM - 08:00 AM", Start = new TimeSpan(7,0,0), End = new TimeSpan(8,0,0) },
            new { Period = "P3", Time = "08:00 AM - 09:00 AM", Start = new TimeSpan(8,0,0), End = new TimeSpan(9,0,0) },
            new { Period = "P4", Time = "09:00 AM - 10:00 AM", Start = new TimeSpan(9,0,0), End = new TimeSpan(10,0,0) },
            new { Period = "P5", Time = "10:00 AM - 11:00 AM", Start = new TimeSpan(10,0,0), End = new TimeSpan(11,0,0) },
            new { Period = "P6", Time = "11:00 AM - 12:00 PM", Start = new TimeSpan(11,0,0), End = new TimeSpan(12,0,0) },
            new { Period = "P7", Time = "12:00 PM - 01:00 PM", Start = new TimeSpan(12,0,0), End = new TimeSpan(13,0,0) },
            new { Period = "P8", Time = "01:00 PM - 02:00 PM", Start = new TimeSpan(13,0,0), End = new TimeSpan(14,0,0) },
            new { Period = "P9", Time = "02:00 PM - 03:00 PM", Start = new TimeSpan(14,0,0), End = new TimeSpan(15,0,0) },
            new { Period = "P10", Time = "03:00 PM - 04:00 PM", Start = new TimeSpan(15,0,0), End = new TimeSpan(16,0,0) },
            new { Period = "P11", Time = "04:00 PM - 05:00 PM", Start = new TimeSpan(16,0,0), End = new TimeSpan(17,0,0) },
            new { Period = "P12", Time = "05:00 PM - 06:00 PM", Start = new TimeSpan(17,0,0), End = new TimeSpan(18,0,0) },
            new { Period = "P13", Time = "06:00 PM - 07:00 PM", Start = new TimeSpan(18,0,0), End = new TimeSpan(19,0,0) },
            new { Period = "P14", Time = "07:00 PM - 08:00 PM", Start = new TimeSpan(19,0,0), End = new TimeSpan(20,0,0) },
            new { Period = "P15", Time = "08:00 PM - 09:00 PM", Start = new TimeSpan(20,0,0), End = new TimeSpan(21,0,0) },
            new { Period = "P16", Time = "09:00 PM - 10:00 PM", Start = new TimeSpan(21,0,0), End = new TimeSpan(22,0,0) },
            new { Period = "P17", Time = "10:00 PM - 11:00 PM", Start = new TimeSpan(22,0,0), End = new TimeSpan(23,0,0) },
            new { Period = "P18", Time = "11:00 PM - 12:00 AM", Start = new TimeSpan(23,0,0), End = new TimeSpan(23,59,59) }
        };

        foreach (var cfg in slotsConfig)
        {
            var res = reservations.FirstOrDefault(r => r.StartTime < cfg.End && r.EndTime > cfg.Start);
            if (res != null)
            {
                schedule.Slots.Add(new ScheduleSlotDto
                {
                    Period = cfg.Period,
                    Time = cfg.Time,
                    Status = res.ReservationType.ToString().ToLower(),
                    Details = res.Purpose,
                    OccupiedBy = res.ReservedFor ?? "Academics",
                    Teacher = "Assigned Faculty",
                    ReservationId = res.Id
                });
            }
            else
            {
                var conflict = await _conflictService.CheckConflictsAsync(roomId, date, cfg.Start, cfg.End);
                var block = conflict.Conflicts.FirstOrDefault(c => c.Severity == "critical");

                schedule.Slots.Add(new ScheduleSlotDto
                {
                    Period = cfg.Period,
                    Time = cfg.Time,
                    Status = ResolveScheduleStatus(block),
                    Details = block?.Message ?? "Available",
                    OccupiedBy = ResolveAvailabilitySource(block)
                });
            }
        }

        return schedule;
    }

    private static string? ResolveAvailabilitySource(ConflictItem? conflict)
    {
        return conflict?.Type switch
        {
            "classAvailability" => "Institute setup class shift",
            "maintenanceCheck" => "Maintenance",
            "holidayCalendar" => "Academic calendar",
            "roomAvailability" => "Timetable / reservation",
            _ => null
        };
    }

    private static string? ResolveNextAvailableAt(ConflictItem? conflict)
    {
        if (conflict?.Type != "classAvailability")
        {
            return null;
        }

        var marker = " to ";
        var index = conflict.Message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var remainder = conflict.Message[(index + marker.Length)..];
        var endIndex = remainder.IndexOf(" based on", StringComparison.OrdinalIgnoreCase);
        return endIndex > 0 ? remainder[..endIndex] : null;
    }

    private static string ResolveScheduleStatus(ConflictItem? conflict)
    {
        return conflict?.Type switch
        {
            "classAvailability" => "class",
            "maintenanceCheck" => "maintenance",
            "holidayCalendar" => "event",
            "roomAvailability" => "reserved",
            _ => "free"
        };
    }
    private async Task<List<Guid>> GetReportingEmployeesAsync(Guid managerEmployeeId)
    {
        var allEmployees = await _employeeRepository.GetListAsync();
        var result = new List<Guid>();
        var visited = new HashSet<Guid>();
        
        void Traverse(Guid parentId)
        {
            if (!visited.Add(parentId)) return;
            
            var children = allEmployees.Where(x => x.ReportingManagerId == parentId).ToList();
            foreach (var child in children)
            {
                if (!result.Contains(child.Id))
                {
                    result.Add(child.Id);
                    Traverse(child.Id);
                }
            }
        }
        
        Traverse(managerEmployeeId);
        return result;
    }
}
