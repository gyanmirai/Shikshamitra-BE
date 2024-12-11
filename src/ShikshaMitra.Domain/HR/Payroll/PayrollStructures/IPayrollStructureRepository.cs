using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.PayrollStructures;

public interface IPayrollStructureRepository : IRepository<PayrollStructure, Guid>
{
}