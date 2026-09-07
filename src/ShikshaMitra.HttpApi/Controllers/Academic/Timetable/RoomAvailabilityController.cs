using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Timetable;

[Area("timetable")]
[Route("api/academics/timetable")]
public class RoomAvailabilityController(IRoomAvailabilityAppService service) : ShikshaMitraController
{
    private readonly IRoomAvailabilityAppService _service = service;

    [HttpGet]
    [Route("room-availability")]
    public async Task<List<RoomAvailabilityDto>> GetAvailabilityAsync([FromQuery] RoomAvailabilityFilterDto input)
    {
        return await _service.GetAvailabilityAsync(input);
    }



    [HttpGet]
    [Route("room-reservations")]
    public async Task<List<RoomReservationDto>> GetReservationsAsync([FromQuery] GetReservationsInputDto input)
    {
        return await _service.GetReservationsAsync(input);
    }

    [HttpPost]
    [Route("room-reservations/check-conflicts")]
    public async Task<ConflictCheckResultDto> CheckConflictsAsync([FromBody] CheckConflictRequestDto input)
    {
        return await _service.CheckConflictsAsync(input);
    }

    [HttpPost]
    [Route("room-reservations/suggest-rooms")]
    public async Task<List<SuggestedRoomDto>> SuggestRoomsAsync([FromBody] SuggestedRoomRequestDto input)
    {
        return await _service.SuggestRoomsAsync(input);
    }

    [HttpPost]
    [Route("room-reservations")]
    public async Task<RoomReservationDto> CreateReservationAsync([FromBody] CreateRoomReservationDto input)
    {
        return await _service.CreateReservationAsync(input);
    }

    [HttpPut]
    [Route("room-reservations/{id}")]
    public async Task<RoomReservationDto> UpdateReservationAsync(Guid id, [FromBody] CreateRoomReservationDto input)
    {
        return await _service.UpdateReservationAsync(id, input);
    }

    [HttpPut]
    [HttpPatch]
    [Route("room-reservations/{id}/status")]
    public async Task UpdateReservationStatusAsync(Guid id, [FromBody] UpdateRoomReservationStatusDto input)
    {
        await _service.UpdateReservationStatusAsync(id, input);
    }

    [HttpPost]
    [Route("room-reservations/{id}/cancel")]
    public async Task CancelReservationAsync(Guid id, [FromBody] string reason)
    {
        await _service.CancelReservationAsync(id, reason);
    }

    [HttpPost]
    [Route("room-reservations/{id}/approve")]
    public async Task ApproveReservationAsync(Guid id)
    {
        await _service.ApproveReservationAsync(id);
    }

    [HttpPost]
    [Route("room-reservations/{id}/reject")]
    public async Task RejectReservationAsync(Guid id, [FromBody] JsonElement input)
    {
        await _service.RejectReservationAsync(id, ExtractReason(input));
    }

    [HttpPost]
    [Route("room-maintenance-blocks")]
    public async Task<RoomMaintenanceBlockDto> CreateMaintenanceBlockAsync([FromBody] CreateMaintenanceBlockDto input)
    {
        return await _service.CreateMaintenanceBlockAsync(input);
    }

    [HttpGet]
    [Route("room-availability/audit-logs")]
    public async Task<List<RoomAuditLogDto>> GetAuditLogsAsync([FromQuery] Guid? roomId)
    {
        return await _service.GetAuditLogsAsync(roomId);
    }

    [HttpGet]
    [Route("room-availability/usage-report")]
    public async Task<List<RoomUsageReportDto>> GetUsageReportAsync()
    {
        return await _service.GetUsageReportAsync();
    }

    [HttpGet]
    [Route("room-availability/schedule")]
    public async Task<RoomScheduleDto> GetRoomScheduleAsync([FromQuery] Guid roomId, [FromQuery] DateTime date)
    {
        return await _service.GetRoomScheduleAsync(roomId, date);
    }

    private static string ExtractReason(JsonElement input)
    {
        if (input.ValueKind == JsonValueKind.String)
        {
            return input.GetString() ?? string.Empty;
        }

        if (input.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (input.TryGetProperty("remarks", out var remarks) && remarks.ValueKind == JsonValueKind.String)
        {
            return remarks.GetString() ?? string.Empty;
        }

        if (input.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String)
        {
            return reason.GetString() ?? string.Empty;
        }

        if (input.TryGetProperty("rejectionReason", out var rejectionReason) &&
            rejectionReason.ValueKind == JsonValueKind.String)
        {
            return rejectionReason.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}
