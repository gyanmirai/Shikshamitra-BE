using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public interface IRoomAvailabilityAppService : IApplicationService
{
    Task<List<RoomAvailabilityDto>> GetAvailabilityAsync(RoomAvailabilityFilterDto input);
    Task<ConflictCheckResultDto> CheckConflictsAsync(CheckConflictRequestDto input);
    Task<List<SuggestedRoomDto>> SuggestRoomsAsync(SuggestedRoomRequestDto input);
    Task<RoomReservationDto> CreateReservationAsync(CreateRoomReservationDto input);
    Task<List<RoomReservationDto>> GetReservationsAsync();
    Task<RoomReservationDto> UpdateReservationAsync(Guid id, CreateRoomReservationDto input);
    Task UpdateReservationStatusAsync(Guid id, UpdateRoomReservationStatusDto input);
    Task CancelReservationAsync(Guid id, string reason);
    Task ApproveReservationAsync(Guid id);
    Task RejectReservationAsync(Guid id, string reason);
    Task<RoomMaintenanceBlockDto> CreateMaintenanceBlockAsync(CreateMaintenanceBlockDto input);
    Task<List<RoomAuditLogDto>> GetAuditLogsAsync(Guid? roomId);
    Task<List<RoomUsageReportDto>> GetUsageReportAsync();
    Task<RoomScheduleDto> GetRoomScheduleAsync(Guid roomId, DateTime date);
}
