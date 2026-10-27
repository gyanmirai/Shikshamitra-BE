using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.TenantSettings.Reports;

public class EfCoreInstituteReportJobRepository(IDbContextProvider<ShikshaMitraDbContext> provider)
	: EfCoreRepository<ShikshaMitraDbContext, InstituteReportJob, Guid>(provider), IInstituteReportJobRepository
{
	public Task<bool> TryAcquireAsync(Guid id, Guid lease, DateTime now) => AcquireAsync(id, lease, now, Guid.NewGuid().ToString("N"));
	private async Task<bool> AcquireAsync(Guid id, Guid lease, DateTime now, string stamp) =>
		await (await GetQueryableAsync()).Where(x => x.Id == id && x.TenantId == CurrentTenant.Id && x.ExpiresAt > now
			&& x.Attempts < 3 && (x.Status == "Queued" || (x.Status == "Processing" && x.LeaseExpiresAt <= now)))
			.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Processing").SetProperty(x => x.LeaseId, lease)
				.SetProperty(x => x.StartedAt, now).SetProperty(x => x.LeaseExpiresAt, now.AddMinutes(10))
				.SetProperty(x => x.Attempts, x => x.Attempts + 1).SetProperty(x => x.LastModificationTime, now)
				.SetProperty(x => x.ConcurrencyStamp, stamp)) == 1;
	public async Task<bool> RenewAsync(Guid id, Guid lease, DateTime now) =>
		await (await GetQueryableAsync()).Where(x => x.Id == id && x.TenantId == CurrentTenant.Id && x.Status == "Processing" && x.LeaseId == lease)
			.ExecuteUpdateAsync(update => update.SetProperty(x => x.LeaseExpiresAt, now.AddMinutes(10))) == 1;
	public Task<bool> CompleteAsync(Guid id, Guid lease, string blob, DateTime now) => CompleteCoreAsync(id, lease, blob, now, Guid.NewGuid().ToString("N"));
	private async Task<bool> CompleteCoreAsync(Guid id, Guid lease, string blob, DateTime now, string stamp) =>
		await (await GetQueryableAsync()).Where(x => x.Id == id && x.TenantId == CurrentTenant.Id && x.Status == "Processing" && x.LeaseId == lease)
			.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Completed").SetProperty(x => x.BlobName, blob)
				.SetProperty(x => x.CompletedAt, now).SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseExpiresAt, (DateTime?)null)
				.SetProperty(x => x.LastModificationTime, now)
				.SetProperty(x => x.ConcurrencyStamp, stamp)) == 1;
	public Task FailAsync(Guid id, Guid lease, DateTime now) => FailCoreAsync(id, lease, now, Guid.NewGuid().ToString("N"));
	private async Task FailCoreAsync(Guid id, Guid lease, DateTime now, string stamp) =>
		await (await GetQueryableAsync()).Where(x => x.Id == id && x.TenantId == CurrentTenant.Id && x.Status == "Processing" && x.LeaseId == lease)
			.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Failed").SetProperty(x => x.CompletedAt, now)
				.SetProperty(x => x.ErrorMessage, "Report generation failed. Generate the report again or contact your administrator.")
				.SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseExpiresAt, (DateTime?)null)
				.SetProperty(x => x.LastModificationTime, now)
				.SetProperty(x => x.ConcurrencyStamp, stamp));
	public Task<bool> RequeueAsync(Guid id, DateTime now) => RequeueCoreAsync(id, now, Guid.NewGuid().ToString("N"));
	private async Task<bool> RequeueCoreAsync(Guid id, DateTime now, string stamp) =>
		await (await GetQueryableAsync()).Where(x => x.Id == id && x.TenantId == CurrentTenant.Id && x.Status == "Processing" && x.ExpiresAt > now
			&& (x.LeaseExpiresAt <= now || (x.LeaseExpiresAt == null && x.CreationTime < now.AddHours(-1))))
			.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Queued").SetProperty(x => x.LeaseId, (Guid?)null)
				.SetProperty(x => x.LastModificationTime, now)
				.SetProperty(x => x.LeaseExpiresAt, (DateTime?)null).SetProperty(x => x.ConcurrencyStamp, stamp)) == 1;
	public Task<bool> ExpireAsync(Guid id, DateTime now) => ExpireCoreAsync(id, now, Guid.NewGuid().ToString("N"));
	private async Task<bool> ExpireCoreAsync(Guid id, DateTime now, string stamp) =>
		await (await GetQueryableAsync()).Where(x => x.Id == id && x.TenantId == CurrentTenant.Id
			&& ((x.Status == "Queued" && x.CreationTime < now.AddDays(-1))
				|| (x.Status == "Processing" && (x.Attempts >= 3 || x.ExpiresAt <= now)
					&& (x.LeaseExpiresAt <= now || (x.LeaseExpiresAt == null && x.CreationTime < now.AddHours(-1))))))
			.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Failed").SetProperty(x => x.CompletedAt, now)
				.SetProperty(x => x.ErrorMessage, "Report generation failed. Generate the report again or contact your administrator.")
				.SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseExpiresAt, (DateTime?)null)
				.SetProperty(x => x.LastModificationTime, now).SetProperty(x => x.ConcurrencyStamp, stamp)) == 1;
}
