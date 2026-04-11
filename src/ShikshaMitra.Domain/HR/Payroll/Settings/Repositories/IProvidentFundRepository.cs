using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Settings;

public interface IProvidentFundRepository : IRepository<ProvidentFund, Guid>
{
	Task<bool> IsCodeExistsAsync(string code, Guid? excludeId = null);
	Task<bool> IsNameExistsAsync(string name, Guid? excludeId = null);
}
