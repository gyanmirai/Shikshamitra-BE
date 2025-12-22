using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Records;

public interface IStudentAppService
{
	Task<PagedResultDto<StudentDto>> GetStudentsByDivisionAsync(
		Guid divisionId,
		int skipCount = 0,
		int maxResultCount = 10,
		string? searchTerm = null
	);
}
