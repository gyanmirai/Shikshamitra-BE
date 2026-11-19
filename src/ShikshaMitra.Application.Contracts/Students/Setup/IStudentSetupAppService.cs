using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.Setup;

public interface IStudentSetupAppService
{
	Task<StudentSetupVisitStatusDto> GetVisitStatusAsync();
	Task<StudentSetupVisitDto> RecordVisitAsync(StudentSetupVisitInput input);
	Task<StudentSetupStateDto> GetAsync(Guid academicYearId, StudentSetupContextInput input);
	Task<StudentSetupStateDto> SaveProgressAsync(Guid academicYearId, StudentSetupProgressInput input);
}
