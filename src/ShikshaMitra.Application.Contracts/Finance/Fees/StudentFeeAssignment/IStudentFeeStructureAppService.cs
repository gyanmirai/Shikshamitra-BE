using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public interface IStudentFeeStructureAppService : IApplicationService
{
	Task AssignAsync(AssignStudentFeeStructureDto input);

	Task<StudentFeeStructureResponseDto> GetByStudentIdAsync(Guid studentId);
}
