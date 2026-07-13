using System;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public interface IExamSeatAllocationAppService
{
    Task<SeatLayoutResponseDto> GetSeatLayoutAsync(Guid examId, GetSeatLayoutInput input);
    Task<SeatStudentPagedResponseDto> GetStudentsForDivisionAsync(Guid examId, Guid divisionId, GetSeatStudentsInput input);
    Task<SaveSeatAllocationResponseDto> SaveSeatAllocationAsync(Guid examId, SaveSeatAllocationRequestDto input);
    Task<SeatAllocationDivisionsResponseDto> GetDivisionsForExamAsync(Guid examId, GetExamDivisionsInput input);
    Task<SeatAllocationDashboardResponseDto> GetSeatAllocationTableAsync(GetSeatAllocationTableInput input);
    Task<ExamRoomPagedResponseDto> GetRoomsForExamAndDivisionAsync(Guid examId, Guid divisionId, GetExamRoomsInput input);
    Task<SeatingPlanScopeListResponseDto> GetSeatingPlanScopesAsync(Guid examId);
    Task<SeatingPlanScopeDto> SaveSeatingPlanScopeAsync(Guid examId, SaveSeatingPlanScopeInputDto input);
    Task DeleteSeatingPlanScopeAsync(Guid examId, Guid seatingPlanId);
    Task<SeatingPlanDraftResponseDto> GetSeatingPlanDraftAsync(Guid examId, Guid? seatingPlanId = null);
    Task<SeatingPlanDraftResponseDto> SaveSeatingPlanDraftAsync(Guid examId, SaveSeatingPlanDraftInputDto input);
    Task<SeatingValidationResponseDto> ValidateSeatingPlanAsync(Guid examId, Guid? seatingPlanId = null);
    Task<SeatingRoomSummaryResponseDto> GetSeatingRoomSummaryAsync(Guid examId, Guid? seatingPlanId = null);
    Task<SeatingPlanDraftResponseDto> FinalizeSeatingPlanAsync(Guid examId, Guid? seatingPlanId = null);
    Task<SeatingPlanDraftResponseDto> PublishSeatingPlanAsync(Guid examId, PublishSeatingPlanInputDto input, Guid? seatingPlanId = null);
    Task<SeatingPlanDraftResponseDto> UnlockSeatingPlanAsync(Guid examId, UnlockSeatingPlanInputDto input, Guid? seatingPlanId = null);
    Task<SeatingPlanAuditLogResponseDto> GetSeatingPlanAuditLogAsync(Guid examId, Guid? seatingPlanId = null);
    Task<SeatingPrintDataResponseDto> GetSeatingPrintDataAsync(Guid examId, Guid? seatingPlanId = null);
    Task<byte[]> GenerateSeatingPrintPdfAsync(Guid examId, Guid? seatingPlanId = null);
    Task<byte[]> GenerateSeatingPrintExcelAsync(Guid examId, Guid? seatingPlanId = null);

    /// <summary>Computes a proposed allocation without persisting it (spec §12.3 preview-before-apply).</summary>
    Task<AutoAllocateResponseDto> AutoAllocatePreviewAsync(Guid examId, AutoAllocateRequestDto input);
}
