using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
public class AttendanceImportLogAccessService(IRepository<AttendanceImportBatch, Guid> attendanceImportBatchRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<AttendanceImportBatch> GetOwnedBatchAsync(Guid importId)
	{
		return await attendanceImportBatchRepository.FindAsync(importId)
			?? throw new EntityNotFoundException(typeof(AttendanceImportBatch), importId);
	}
}
