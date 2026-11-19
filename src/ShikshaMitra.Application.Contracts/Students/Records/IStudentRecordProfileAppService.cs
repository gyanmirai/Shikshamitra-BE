using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public interface IStudentRecordProfileAppService : IApplicationService
{
	Task<StudentRecordProfileDto> GetAsync(Guid studentId, Guid? academicYearId = null);
}
