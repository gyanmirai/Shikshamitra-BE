using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;

namespace ShikshaMitra.Controllers.Academic.Examination.Exams;

[RemoteService(Name = "ShikshaMitra")]
[Route("api/academics/exams")]
[Authorize]
[ApiController]
public class SeatAllocationController(
    IExamSeatAllocationAppService seatAllocationAppService
) : ShikshaMitraController
{
    /// <summary>GET /api/academics/exams/seat-allocation — dashboard table</summary>
    [HttpGet("seat-allocation")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.View)]
    public async Task<SeatAllocationDashboardResponseDto> GetSeatAllocationTableAsync(
        [FromQuery] GetSeatAllocationTableInput input)
    {
        return await seatAllocationAppService.GetSeatAllocationTableAsync(input);
    }

    /// <summary>GET /api/academics/exams/{examId}/seat-layout — seat layout for a room</summary>
    [HttpGet("{examId}/seat-layout")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.View)]
    public async Task<SeatLayoutResponseDto> GetSeatLayoutAsync(
        Guid examId,
        [FromQuery] GetSeatLayoutInput input)
    {
        return await seatAllocationAppService.GetSeatLayoutAsync(examId, input);
    }

    /// <summary>GET /api/academics/exams/{examId}/seat-allocation/students — divisions for exam</summary>
    [HttpGet("{examId}/seat-allocation/students")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.View)]
    public async Task<SeatAllocationDivisionsResponseDto> GetDivisionsForExamAsync(
        Guid examId,
        [FromQuery] GetExamDivisionsInput input)
    {
        return await seatAllocationAppService.GetDivisionsForExamAsync(examId, input);
    }

    /// <summary>GET /api/academics/exams/{examId}/seat-allocation/divisions/{divisionId}/students — student list</summary>
    [HttpGet("{examId}/seat-allocation/divisions/{divisionId}/students")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.View)]
    public async Task<SeatStudentPagedResponseDto> GetStudentsForDivisionAsync(
        Guid examId,
        Guid divisionId,
        [FromQuery] GetSeatStudentsInput input)
    {
        return await seatAllocationAppService.GetStudentsForDivisionAsync(examId, divisionId, input);
    }

    /// <summary>POST /api/academics/exams/{examId}/seat-allocation/save — bulk save</summary>
    [HttpPost("{examId}/seat-allocation/save")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Add)]
    public async Task<SaveSeatAllocationResponseDto> SaveSeatAllocationAsync(
        Guid examId,
        SaveSeatAllocationRequestDto input)
    {
        return await seatAllocationAppService.SaveSeatAllocationAsync(examId, input);
    }

    /// <summary>GET /api/academics/exams/{examId}/divisions/{divisionId}/rooms — rooms for exam+division</summary>
    [HttpGet("{examId}/divisions/{divisionId}/rooms")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.View)]
    public async Task<ExamRoomPagedResponseDto> GetRoomsForExamAndDivisionAsync(
        Guid examId,
        Guid divisionId,
        [FromQuery] GetExamRoomsInput input)
    {
        return await seatAllocationAppService.GetRoomsForExamAndDivisionAsync(examId, divisionId, input);
    }
}
