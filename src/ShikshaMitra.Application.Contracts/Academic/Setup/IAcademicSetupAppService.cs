using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Setup;

public interface IAcademicSetupAppService
{
	Task<AcademicSetupVisitStatusDto> GetVisitStatusAsync();
	Task<AcademicSetupVisitDto> RecordVisitAsync(AcademicSetupVisitInput input);
	Task<AcademicSetupStateDto> GetAsync(Guid academicYearId, AcademicSetupContextInput input);
	Task<AcademicSetupStateDto> SaveProgressAsync(Guid academicYearId, AcademicSetupProgressInput input);
}
