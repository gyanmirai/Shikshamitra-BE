using ShikshaMitra.HR.Employees;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Curriculum;

public interface ISubjectRepository : IRepository<Subject, Guid>
{
	public Task<Subject?> GetSubjectsByClassAsync(Guid classId);
}
