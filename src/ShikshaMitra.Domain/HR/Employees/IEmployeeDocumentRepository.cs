using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees;

public interface IEmployeeDocumentRepository : IRepository<EmployeeDocument, Guid>
{
}
