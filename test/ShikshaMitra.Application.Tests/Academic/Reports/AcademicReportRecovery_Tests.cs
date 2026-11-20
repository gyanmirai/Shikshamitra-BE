using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Reports;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public sealed class AcademicReportRecovery_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Expired_notice_requeues_the_owned_export_once_and_fences_old_worker_deliveries()
	{
		var schoolId = Guid.NewGuid();
		var owner = Guid.NewGuid();
		var jobId = Guid.NewGuid();
		using var school = GetRequiredService<ICurrentTenant>().Change(schoolId);
		var reports = GetRequiredService<IAcademicReportAppService>();
		var state = GetRequiredService<AcademicReportExportStateService>();
		var repository = GetRequiredService<IRepository<HrReportExportJob, Guid>>();
		var request = new AcademicReportRequestDto { ReportType = AcademicReportTypes.ChapterDescriptionSummary };

		await WithUnitOfWorkAsync(async () => await repository.InsertAsync(new(jobId, schoolId, owner, request.ReportType,
			request.Format, "{}", JsonSerializer.Serialize(request), DateTime.UtcNow.AddMinutes(-1)), autoSave: true));
		var originalRun = (await state.TryStartAsync(jobId)).ShouldNotBeNull();
		(await state.TryCompleteAsync(jobId, originalRun, "expired.xlsx", 3, 40)).ShouldBeTrue();

		using (AsUser(Guid.NewGuid()))
		{
			await Should.ThrowAsync<AbpAuthorizationException>(() => reports.GetExportRecoveryAsync(jobId));
			await Should.ThrowAsync<AbpAuthorizationException>(() => reports.RegenerateExportAsync(jobId));
		}

		using var principal = AsUser(owner);
		var expired = await reports.GetExportRecoveryAsync(jobId);
		expired.Status.ShouldBe("Expired");
		expired.CanRegenerate.ShouldBeTrue();
		var queued = await reports.RegenerateExportAsync(jobId);
		queued.JobId.ShouldBe(jobId);
		queued.Status.ShouldBe(HrReportExportStatus.Queued);
		var generation = await WithUnitOfWorkAsync(async () =>
			AcademicReportExportStateService.ReadGenerationId(await repository.GetAsync(jobId)));
		generation.ShouldNotBeNull();
		(await reports.RegenerateExportAsync(jobId)).JobId.ShouldBe(jobId);
		(await WithUnitOfWorkAsync(async () => AcademicReportExportStateService.ReadGenerationId(await repository.GetAsync(jobId))))
			.ShouldBe(generation);

		(await state.TryStartAsync(jobId)).ShouldBeNull();
		(await state.TryStartAsync(jobId, Guid.NewGuid())).ShouldBeNull();
		var jobs = GetRequiredService<IBackgroundJobStore>();
		var work = await WithUnitOfWorkAsync(async () => (await jobs.GetWaitingJobsAsync(100))
			.Where(x => x.JobArgs.Contains(jobId.ToString("D"), StringComparison.OrdinalIgnoreCase)).ShouldHaveSingleItem());
		var args = JsonSerializer.Deserialize<AcademicReportExportJobArgs>(work.JobArgs, new JsonSerializerOptions(JsonSerializerDefaults.Web))
			.ShouldNotBeNull();
		args.GenerationId.ShouldBe(generation);
		args.UserId.ShouldBe(owner);
		args.Request!.ReportType.ShouldBe(request.ReportType);
		await GetRequiredService<AcademicReportExportBackgroundJob>().ExecuteAsync(args);
		(await state.TryCompleteAsync(jobId, originalRun, "stale.xlsx", 1, 10)).ShouldBeFalse();
		var ready = await reports.GetExportRecoveryAsync(jobId);
		ready.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow);
		ready.Status.ShouldBe(HrReportExportStatus.Completed);
		ready.CanRegenerate.ShouldBeFalse();
		var download = await reports.DownloadExportAsync(jobId);
		download.Bytes.ShouldNotBeEmpty();

		IDisposable AsUser(Guid userId) => GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
			[new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.TenantId, schoolId.ToString())], "Test")));
	}

	[Fact]
	public void Dynamic_recovery_intersects_the_saved_and_current_scope()
	{
		var retained = Guid.NewGuid();
		var removed = Guid.NewGuid();
		var added = Guid.NewGuid();
		AcademicReportAppService.IntersectExportScope([retained, removed], [retained, added]).ShouldBe([retained]);
		AcademicReportAppService.IntersectExportScope([retained], null).ShouldBe([retained]);
		Should.Throw<BusinessException>(() => AcademicReportAppService.IntersectExportScope([removed], [added]));
	}
}