using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentRecords;

public interface IStudentRepository : IRepository<Student, Guid>
{
}
