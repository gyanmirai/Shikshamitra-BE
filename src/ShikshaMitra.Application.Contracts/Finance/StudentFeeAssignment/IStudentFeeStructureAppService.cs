using System;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Finance.StudentFeeAssignment.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.Finance.StudentFeeAssignment;

public interface IStudentFeeStructureAppService : IApplicationService
{
	Task<StudentFeeStructureResponseDto> AssignAsync(AssignStudentFeeStructureDto input);

	Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId);
}
