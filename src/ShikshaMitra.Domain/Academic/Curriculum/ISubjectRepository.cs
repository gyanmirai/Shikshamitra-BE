using ShikshaMitra.HR.Employees;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Curriculum;

public interface ISubjectRepository : IRepository<Subject, Guid>
{
	public Task<PagedResultDto<Subject>> GetSubjectsByClassAsync(Guid classId, int skipCount, int maxResultCount);
}
