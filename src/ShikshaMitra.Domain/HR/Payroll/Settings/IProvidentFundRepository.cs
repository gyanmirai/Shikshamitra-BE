using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Settings;

public interface IProvidentFundRepository : IRepository<ProvidentFund, Guid> 
{
	
}