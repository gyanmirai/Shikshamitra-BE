using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.Reports;

public interface IInstituteReportJobRepository : IRepository<InstituteReportJob, Guid>
{
	Task<bool> TryAcquireAsync(Guid id, Guid lease, DateTime now);
	Task<bool> RenewAsync(Guid id, Guid lease, DateTime now);
	Task<bool> CompleteAsync(Guid id, Guid lease, string blob, DateTime now);
	Task FailAsync(Guid id, Guid lease, DateTime now);
	Task<bool> RequeueAsync(Guid id, DateTime now);
	Task<bool> ExpireAsync(Guid id, DateTime now);
}
