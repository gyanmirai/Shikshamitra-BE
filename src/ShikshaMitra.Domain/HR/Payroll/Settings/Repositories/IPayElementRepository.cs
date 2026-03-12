using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Settings;

public interface IPayElementRepository : IRepository<PayElement, Guid>
{
}