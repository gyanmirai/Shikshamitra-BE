using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

[RemoteService(false)]
public class AttendanceImportBatchStateWorkflow(
	IRepository<AttendanceImportBatch, Guid> attendanceImportBatchRepository,
	AttendanceImportLogAccessService accessService)
	: ApplicationService, ITransientDependency
{
	public async Task LockBatchAsync(Guid importId)
	{
		var batch = await accessService.GetOwnedBatchAsync(importId);
		if (batch.IsBatchLocked)
		{
			return;
		}

		batch.SetBatchLocked(true);
		await attendanceImportBatchRepository.UpdateAsync(batch, autoSave: true);
	}

	public async Task UnlockBatchAsync(Guid importId)
	{
		var batch = await accessService.GetOwnedBatchAsync(importId);
		if (!batch.IsBatchLocked)
		{
			return;
		}

		batch.SetBatchLocked(false);
		await attendanceImportBatchRepository.UpdateAsync(batch, autoSave: true);
	}
}
