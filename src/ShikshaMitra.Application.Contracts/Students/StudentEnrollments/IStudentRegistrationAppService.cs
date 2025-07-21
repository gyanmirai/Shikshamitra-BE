using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments;
public interface IStudentRegistrationAppService
{
	Task<PagedResultDto<StudentRegistrationFormDto>> GetRegistrationFormsByMediumAsync(
		Guid academicMediumId, 
		int skipCount = 0, 
		int maxResultCount = 10, 
		string? searchTerm = null);
}
