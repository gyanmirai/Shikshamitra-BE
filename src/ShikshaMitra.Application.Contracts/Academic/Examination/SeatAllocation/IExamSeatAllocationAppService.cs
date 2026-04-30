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
}
