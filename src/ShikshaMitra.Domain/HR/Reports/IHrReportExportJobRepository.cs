using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Reports;

public interface IHrReportExportJobRepository : IRepository<HrReportExportJob, Guid>
{
	Task<List<HrReportExportJob>> GetRecentAuthorizedJobsAsync(
		Guid userId,
		int maxResultCount,
		DateTime? requestedFrom = null,
		DateTime? requestedTo = null,
		CancellationToken cancellationToken = default);
}
