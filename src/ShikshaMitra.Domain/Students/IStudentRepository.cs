using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students;

public interface IStudentRepository : IRepository<Student, Guid>
{
	Task<Student?> GetStudentWithDetailsAsync(Guid studentId);
	Task<List<Student>> GetStudentsAsync(bool includeDetails = false);
}