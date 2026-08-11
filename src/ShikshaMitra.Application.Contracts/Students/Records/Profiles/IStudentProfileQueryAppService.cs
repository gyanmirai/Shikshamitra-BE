using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records.Profiles;

public interface IStudentProfileQueryAppService : IApplicationService
{
	Task<List<StudentSearchResultDto>> SearchStudentsByNameAsync(string searchTerm, int maxResultCount = 20);
}
