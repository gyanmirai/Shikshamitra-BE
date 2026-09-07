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
    /// <summary>GET /api/academics/exams/{examId}/seating-plan/v2/workspace — exam plan-group workspace</summary>
    [HttpGet("{examId}/seating-plan/v2/workspace")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.View)]
    public async Task<SeatingPlanWorkspaceDto> GetSeatingPlanWorkspaceAsync(
        Guid examId,
        [FromQuery] Guid? seatingPlanId = null)
    {
        return await seatAllocationAppService.GetSeatingPlanWorkspaceAsync(examId, seatingPlanId);
    }

    /// <summary>POST /api/academics/exams/{examId}/seating-plan/v2/plans — prepare one shared or individual subject plan</summary>
    [HttpPost("{examId}/seating-plan/v2/plans")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Add)]
    public async Task<SeatingPlanWorkspaceDto> PrepareSeatingPlanAsync(
        Guid examId,
        PrepareSeatingPlanInputDto input)
    {
        return await seatAllocationAppService.PrepareSeatingPlanAsync(examId, input);
    }

    /// <summary>Compatibility endpoint for older session-first clients.</summary>
    [HttpPost("{examId}/seating-plan/v2/session")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Add)]
    public async Task<SeatingPlanWorkspaceDto> PrepareSessionSeatingPlanAsync(
        Guid examId,
        PrepareSessionSeatingPlanInputDto input)
    {
        return await seatAllocationAppService.PrepareSessionSeatingPlanAsync(examId, input);
    }

    /// <summary>POST /api/academics/exams/{examId}/seating-plan/v2/validate-examination — validate every plan and subject before the single publish action</summary>
    [HttpPost("{examId}/seating-plan/v2/validate-examination")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.View)]
    public async Task<ExaminationSeatingReadinessDto> ValidateExaminationSeatingAsync(Guid examId)
    {
        return await seatAllocationAppService.ValidateExaminationSeatingAsync(examId);
    }

    /// <summary>POST /api/academics/exams/{examId}/seating-plan/v2/reset-draft — remove every draft plan and seat so the examination can be rebuilt</summary>
    [HttpPost("{examId}/seating-plan/v2/reset-draft")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Delete)]
    public async Task<SeatingPlanWorkspaceDto> ResetExaminationSeatingDraftAsync(
        Guid examId,
        ResetExaminationSeatingDraftInputDto input)
    {
        return await seatAllocationAppService.ResetExaminationSeatingDraftAsync(examId, input);
    }

    /// <summary>POST /api/academics/exams/{examId}/seating-plan/v2/publish — validate and publish the complete examination seating arrangement atomically</summary>
    [HttpPost("{examId}/seating-plan/v2/publish")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Publish)]
    public async Task<SeatingPlanWorkspaceDto> PublishExaminationSeatingAsync(
        Guid examId,
        PublishSeatingPlanInputDto input)
    {
        return await seatAllocationAppService.PublishExaminationSeatingAsync(examId, input);
    }

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

    /// <summary>GET /api/academics/exams/{examId}/seating-plan/scopes — subject-group seating plans</summary>
    [HttpGet("{examId}/seating-plan/scopes")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.View)]
    public async Task<SeatingPlanScopeListResponseDto> GetSeatingPlanScopesAsync(Guid examId)
    {
        return await seatAllocationAppService.GetSeatingPlanScopesAsync(examId);
    }

    /// <summary>POST /api/academics/exams/{examId}/seating-plan/scopes — create/update a subject-group seating plan</summary>
    [HttpPost("{examId}/seating-plan/scopes")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Add)]
    public async Task<SeatingPlanScopeDto> SaveSeatingPlanScopeAsync(Guid examId, SaveSeatingPlanScopeInputDto input)
    {
        return await seatAllocationAppService.SaveSeatingPlanScopeAsync(examId, input);
    }

    /// <summary>DELETE /api/academics/exams/{examId}/seating-plan/scopes/{seatingPlanId} — delete an unlocked empty subject-group seating plan</summary>
    [HttpDelete("{examId}/seating-plan/scopes/{seatingPlanId}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Delete)]
    public async Task DeleteSeatingPlanScopeAsync(Guid examId, Guid seatingPlanId)
    {
        await seatAllocationAppService.DeleteSeatingPlanScopeAsync(examId, seatingPlanId);
    }

    /// <summary>GET /api/academics/exams/{examId}/seating-plan — plan-level draft state (rooms in scope, rules, status, counts)</summary>
    [HttpGet("{examId}/seating-plan")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.View)]
    public async Task<SeatingPlanDraftResponseDto> GetSeatingPlanDraftAsync(Guid examId, [FromQuery] Guid? seatingPlanId = null)
    {
        return await seatAllocationAppService.GetSeatingPlanDraftAsync(examId, seatingPlanId);
    }

    /// <summary>PUT /api/academics/exams/{examId}/seating-plan — save plan-level draft state</summary>
    [HttpPut("{examId}/seating-plan")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Add)]
    public async Task<SeatingPlanDraftResponseDto> SaveSeatingPlanDraftAsync(
        Guid examId,
        SaveSeatingPlanDraftInputDto input)
    {
        return await seatAllocationAppService.SaveSeatingPlanDraftAsync(examId, input);
    }

    /// <summary>PUT /api/academics/exams/{examId}/seating-plan/bulk-save — atomic draft and loaded-room save</summary>
    [HttpPut("{examId}/seating-plan/bulk-save")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Add)]
    public async Task<SeatingPlanDraftResponseDto> SaveSeatingPlanBulkAsync(
        Guid examId,
        SaveSeatingPlanBulkInputDto input)
    {
        return await seatAllocationAppService.SaveSeatingPlanBulkAsync(examId, input);
    }

    /// <summary>POST /api/academics/exams/{examId}/seating-plan/validate — capacity/conflict validation</summary>
    [HttpPost("{examId}/seating-plan/validate")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.View)]
    public async Task<SeatingValidationResponseDto> ValidateSeatingPlanAsync(Guid examId, [FromQuery] Guid? seatingPlanId = null)
    {
        return await seatAllocationAppService.ValidateSeatingPlanAsync(examId, seatingPlanId);
    }

    /// <summary>GET /api/academics/exams/{examId}/seating-plan/room-summary — multi-room utilization summary</summary>
    [HttpGet("{examId}/seating-plan/room-summary")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.View)]
    public async Task<SeatingRoomSummaryResponseDto> GetSeatingRoomSummaryAsync(Guid examId, [FromQuery] Guid? seatingPlanId = null)
    {
        return await seatAllocationAppService.GetSeatingRoomSummaryAsync(examId, seatingPlanId);
    }

    /// <summary>POST /api/academics/exams/{examId}/seating-plan/finalize — lock the plan after validation passes</summary>
    [HttpPost("{examId}/seating-plan/finalize")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Finalize)]
    public async Task<SeatingPlanDraftResponseDto> FinalizeSeatingPlanAsync(Guid examId, [FromQuery] Guid? seatingPlanId = null)
    {
        return await seatAllocationAppService.FinalizeSeatingPlanAsync(examId, seatingPlanId);
    }

    /// <summary>POST /api/academics/exams/{examId}/seating-plan/publish — mark a finalized plan published</summary>
    [HttpPost("{examId}/seating-plan/publish")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Publish)]
    public async Task<SeatingPlanDraftResponseDto> PublishSeatingPlanAsync(Guid examId, PublishSeatingPlanInputDto input, [FromQuery] Guid? seatingPlanId = null)
    {
        return await seatAllocationAppService.PublishSeatingPlanAsync(examId, input, seatingPlanId);
    }

    /// <summary>POST /api/academics/exams/{examId}/seating-plan/unlock — reopen a finalized plan for editing (requires a reason)</summary>
    [HttpPost("{examId}/seating-plan/unlock")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Unlock)]
    public async Task<SeatingPlanDraftResponseDto> UnlockSeatingPlanAsync(Guid examId, UnlockSeatingPlanInputDto input, [FromQuery] Guid? seatingPlanId = null)
    {
        return await seatAllocationAppService.UnlockSeatingPlanAsync(examId, input, seatingPlanId);
    }

    /// <summary>GET /api/academics/exams/{examId}/seating-plan/audit-log — plan lifecycle history</summary>
    [HttpGet("{examId}/seating-plan/audit-log")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.View)]
    public async Task<SeatingPlanAuditLogResponseDto> GetSeatingPlanAuditLogAsync(Guid examId, [FromQuery] Guid? seatingPlanId = null)
    {
        return await seatAllocationAppService.GetSeatingPlanAuditLogAsync(examId, seatingPlanId);
    }

    /// <summary>GET /api/academics/exams/{examId}/seating-plan/print-data — flat per-seat rows for print views</summary>
    [HttpGet("{examId}/seating-plan/print-data")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Print)]
    public async Task<SeatingPrintDataResponseDto> GetSeatingPrintDataAsync(Guid examId, [FromQuery] Guid? seatingPlanId = null)
    {
        return await seatAllocationAppService.GetSeatingPrintDataAsync(examId, seatingPlanId);
    }

    /// <summary>GET /api/academics/exams/{examId}/seating-plan/print-data/pdf — master seating list PDF</summary>
    [HttpGet("{examId}/seating-plan/print-data/pdf")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Print)]
    public async Task<FileContentResult> GenerateSeatingPrintPdfAsync(Guid examId, [FromQuery] Guid? seatingPlanId = null)
    {
        var bytes = await seatAllocationAppService.GenerateSeatingPrintPdfAsync(examId, seatingPlanId);
        return new FileContentResult(bytes, "application/pdf") { FileDownloadName = "seating-plan.pdf" };
    }

    /// <summary>GET /api/academics/exams/{examId}/seating-plan/print-data/excel — master seating list Excel</summary>
    [HttpGet("{examId}/seating-plan/print-data/excel")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.Print)]
    public async Task<FileContentResult> GenerateSeatingPrintExcelAsync(Guid examId, [FromQuery] Guid? seatingPlanId = null)
    {
        var bytes = await seatAllocationAppService.GenerateSeatingPrintExcelAsync(examId, seatingPlanId);
        return new FileContentResult(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        {
            FileDownloadName = "seating-plan.xlsx"
        };
    }

    /// <summary>POST /api/academics/exams/{examId}/seating-plan/auto-allocate — preview-only proposed allocation</summary>
    [HttpPost("{examId}/seating-plan/auto-allocate")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.AutoAllocate)]
    public async Task<AutoAllocateResponseDto> AutoAllocatePreviewAsync(Guid examId, AutoAllocateRequestDto input)
    {
        return await seatAllocationAppService.AutoAllocatePreviewAsync(examId, input);
    }

    /// <summary>POST /api/academics/exams/{examId}/seating-plan/v2/apply-preview — merge a reviewed proposal without replacing existing seats</summary>
    [HttpPost("{examId}/seating-plan/v2/apply-preview")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_SeatAllocation.AutoAllocate)]
    public async Task<SeatingPlanDraftResponseDto> ApplyAutoAllocatePreviewAsync(
        Guid examId,
        ApplyAutoAllocatePreviewInputDto input)
    {
        return await seatAllocationAppService.ApplyAutoAllocatePreviewAsync(examId, input);
    }
}
