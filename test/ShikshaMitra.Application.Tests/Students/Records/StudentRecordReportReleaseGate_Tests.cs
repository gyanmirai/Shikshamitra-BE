using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ShikshaMitra.Common.Excel;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private const string ReportProofView = ShikshaMitraSubmodulePermissions.Students.Records_Reports.View;
	private const string ReportProofAdd = ShikshaMitraSubmodulePermissions.Students.Records_Reports.Add;
	private const string ReportProofSource = ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View;
	private const string ReportProofIdentityRole = "RecordsReportIdentityReader";

	[Fact]
	public Task Record_report_release_completed_download_rechecks_persisted_read_grant_but_does_not_require_generation_grant() => RunCertificationScenarioAsync(async () =>
	{
		var proof = await CreateReportReleaseProofAsync();
		using var actor = _principalAccessor.Change(ProfilePrincipal(proof.ActorId));
		var job = await proof.Reports.CreateAsync(ReportProofInput(proof.StudentId));
		await proof.Runner.RunAsync(job.Id);
		var file = await proof.Reports.DownloadAsync(job.Id); file.Content.Length.ShouldBeGreaterThan(1000);
		await SetReportProofGrantAsync(proof.ActorId, ReportProofAdd, false);
		(await proof.Checker.IsGrantedAsync(ReportProofAdd)).ShouldBeFalse();
		(await proof.Reports.DownloadAsync(job.Id)).Content.SequenceEqual(file.Content).ShouldBeTrue();
		(await GetReportProofListAsync(proof)).Items.Single().CanDownload.ShouldBeTrue();
		await SetReportProofGrantAsync(proof.ActorId, ReportProofSource, false);
		(await proof.Checker.IsGrantedAsync(ReportProofSource)).ShouldBeFalse();
		(await proof.Checker.IsGrantedAsync(ReportProofView)).ShouldBeTrue();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<PermissionGrant, Guid>>().AnyAsync(g =>
			g.TenantId == TestTenantId && g.ProviderName == "U" && g.ProviderKey == proof.ActorId.ToString() && g.Name == ReportProofSource))).ShouldBeFalse();
		await Should.ThrowAsync<AbpAuthorizationException>(() => proof.Reports.DownloadAsync(job.Id));
		(await GetReportProofListAsync(proof)).Items.Single().CanDownload.ShouldBeFalse();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordReportJob, Guid>>().GetAsync(job.Id))).Status.ShouldBe("Completed");
	});

	[Fact]
	public Task Record_report_release_database_role_removal_invalidates_completed_file_despite_stale_role_claim() => RunCertificationScenarioAsync(async () =>
	{
		var proof = await CreateReportReleaseProofAsync(identityRole: true);
		var principal = ProfilePrincipal(proof.ActorId); ((ClaimsIdentity)principal.Identity!).AddClaim(new(AbpClaimTypes.Role, ReportProofIdentityRole));
		using var actor = _principalAccessor.Change(principal);
		var policy = StudentRecordsPolicy.Starter(); policy.Access.Masks = [new() { FieldGroup = "Identity", AllowedRoleNames = [ReportProofIdentityRole] }];
		await PublishReportProofPolicyAsync(policy);
		var input = ReportProofInput(proof.StudentId);
		(await proof.Reports.PreviewAsync(input)).Rows.Single()["Student"].ShouldBe("RecordChange Student");
		var job = await proof.Reports.CreateAsync(input); await proof.Runner.RunAsync(job.Id);
		ReadReportProofXml((await proof.Reports.DownloadAsync(job.Id)).Content).ShouldContain("RecordChange Student");
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IdentityUserManager>();
			(await users.RemoveFromRoleAsync((await users.FindByIdAsync(proof.ActorId.ToString()))!, ReportProofIdentityRole)).Succeeded.ShouldBeTrue();
		});
		principal.FindAll(AbpClaimTypes.Role).ShouldContain(c => c.Value == ReportProofIdentityRole);
		(await proof.Checker.IsGrantedAsync(ReportProofSource)).ShouldBeTrue();
		(await Should.ThrowAsync<BusinessException>(() => proof.Reports.DownloadAsync(job.Id))).Code.ShouldBe("Students:Records:ReportAuthorityChanged");
		(await GetReportProofListAsync(proof)).Items.Single().CanDownload.ShouldBeFalse();
		(await proof.Reports.PreviewAsync(input)).Rows.Single()["Student"].ShouldBe("Restricted student");
	});

	[Fact]
	public Task Record_report_release_stricter_mask_invalidates_completed_and_queued_files_and_new_workbook_contains_only_masked_identity() => RunCertificationScenarioAsync(async () =>
	{
		var proof = await CreateReportReleaseProofAsync();
		using var actor = _principalAccessor.Change(ProfilePrincipal(proof.ActorId));
		var completed = await proof.Reports.CreateAsync(ReportProofInput(proof.StudentId)); await proof.Runner.RunAsync(completed.Id);
		ReadReportProofXml((await proof.Reports.DownloadAsync(completed.Id)).Content).ShouldContain("RecordChange Student");
		var queued = await proof.Reports.CreateAsync(ReportProofInput(proof.StudentId));
		var policy = StudentRecordsPolicy.Starter(); policy.Access.Masks = [new() { FieldGroup = "Identity", AllowedRoleNames = [ReportProofIdentityRole] }];
		await PublishReportProofPolicyAsync(policy);
		(await Should.ThrowAsync<BusinessException>(() => proof.Reports.DownloadAsync(completed.Id))).Code.ShouldBe("Students:Records:ReportAuthorityChanged");
		(await GetReportProofListAsync(proof)).Items.Single(j => j.Id == completed.Id).CanDownload.ShouldBeFalse();
		await proof.Runner.RunAsync(queued.Id); await AssertReportProofFailureAsync(proof, queued.Id, "Students:Records:ReportAuthorityChanged");
		var fresh = await proof.Reports.CreateAsync(ReportProofInput(proof.StudentId)); await proof.Runner.RunAsync(fresh.Id);
		var xml = ReadReportProofXml((await proof.Reports.DownloadAsync(fresh.Id)).Content);
		xml.ShouldContain("Restricted student"); xml.ShouldNotContain("RecordChange Student");
	});

	[Fact]
	public Task Record_report_release_expired_completed_file_is_not_downloadable_and_expired_queued_job_is_not_started() => RunCertificationScenarioAsync(async () =>
	{
		var proof = await CreateReportReleaseProofAsync();
		using var actor = _principalAccessor.Change(ProfilePrincipal(proof.ActorId));
		var completed = await proof.Reports.CreateAsync(ReportProofInput(proof.StudentId)); await proof.Runner.RunAsync(completed.Id);
		(await proof.Reports.DownloadAsync(completed.Id)).Content.Length.ShouldBeGreaterThan(1000);
		await ExpireReportProofJobAsync(completed.Id);
		(await Should.ThrowAsync<BusinessException>(() => proof.Reports.DownloadAsync(completed.Id))).Code.ShouldBe("Students:Records:ReportNotReady");
		var expired = (await GetReportProofListAsync(proof)).Items.Single(); expired.Status.ShouldBe("Expired"); expired.CanDownload.ShouldBeFalse();
		var queued = await proof.Reports.CreateAsync(ReportProofInput(proof.StudentId)); await ExpireReportProofJobAsync(queued.Id);
		await proof.Runner.RunAsync(queued.Id);
		var stored = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordReportJob, Guid>>().GetAsync(queued.Id));
		stored.Status.ShouldBe("Queued"); stored.GenerationId.ShouldBeNull(); stored.BlobName.ShouldBeNull();
		(await GetReportProofListAsync(proof)).Items.Single(j => j.Id == queued.Id).Status.ShouldBe("Expired");
		(await Should.ThrowAsync<BusinessException>(() => proof.Reports.DownloadAsync(queued.Id))).Code.ShouldBe("Students:Records:ReportNotReady");
	});

	[Fact]
	public Task Record_report_release_async_runner_rechecks_persisted_generation_grant_before_reading_source() => RunCertificationScenarioAsync(async () =>
	{
		var proof = await CreateReportReleaseProofAsync();
		using var actor = _principalAccessor.Change(ProfilePrincipal(proof.ActorId));
		var queued = await proof.Reports.CreateAsync(ReportProofInput(proof.StudentId));
		await SetReportProofGrantAsync(proof.ActorId, ReportProofAdd, false);
		(await proof.Checker.IsGrantedAsync(ReportProofAdd)).ShouldBeFalse();
		await proof.Runner.RunAsync(queued.Id); await AssertReportProofFailureAsync(proof, queued.Id, "Students:Records:ReportFailed");
	});

	[Theory]
	[InlineData(ReportProofAdd)]
	[InlineData(ReportProofSource)]
	public Task Record_report_release_revocation_after_real_workbook_generation_prevents_completion_and_blob_publication(string permission) => RunCertificationScenarioAsync(async () =>
	{
		var proof = await CreateReportReleaseProofAsync();
		using var actor = _principalAccessor.Change(ProfilePrincipal(proof.ActorId));
		var queued = await proof.Reports.CreateAsync(ReportProofInput(proof.StudentId));
		var excel = new ReportProofAfterWorkbook(GetRequiredService<IExcelService>(), () => SetReportProofGrantAsync(proof.ActorId, permission, false));
		await CreateReportProofRunner(proof.Generator, proof.Authority, proof.Checker, excel).RunAsync(queued.Id);
		excel.Invoked.ShouldBeTrue(); excel.GeneratedBytes.ShouldBeGreaterThan(1000);
		(await proof.Checker.IsGrantedAsync(permission)).ShouldBeFalse();
		await AssertReportProofFailureAsync(proof, queued.Id, "Students:Records:ReportFailed");
	});

	[Fact]
	public Task Record_report_release_expiry_after_real_workbook_generation_prevents_completion_and_blob_publication() => RunCertificationScenarioAsync(async () =>
	{
		var proof = await CreateReportReleaseProofAsync();
		using var actor = _principalAccessor.Change(ProfilePrincipal(proof.ActorId));
		var queued = await proof.Reports.CreateAsync(ReportProofInput(proof.StudentId));
		var excel = new ReportProofAfterWorkbook(GetRequiredService<IExcelService>(), () => ExpireReportProofJobAsync(queued.Id));
		await CreateReportProofRunner(proof.Generator, proof.Authority, proof.Checker, excel).RunAsync(queued.Id);
		excel.Invoked.ShouldBeTrue(); excel.GeneratedBytes.ShouldBeGreaterThan(1000);
		await AssertReportProofFailureAsync(proof, queued.Id, "Students:Records:ReportNotReady");
		(await GetReportProofListAsync(proof)).Items.Single().Status.ShouldBe("Expired");
	});

	private async Task<ReportReleaseProof> CreateReportReleaseProofAsync(bool identityRole = false)
	{
		var fixture = await RecordContactFixtureAsync(); var actor = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(actor, $"records-report-proof-{actor:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: actor), true);
			var roles = GetRequiredService<IdentityRoleManager>();
			foreach (var role in new[] { "admin", ReportProofIdentityRole })
				if (!await roles.RoleExistsAsync(role)) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), role, TestTenantId))).Succeeded.ShouldBeTrue();
			if (identityRole)
			{
				var users = GetRequiredService<IdentityUserManager>();
				(await users.AddToRoleAsync((await users.FindByIdAsync(actor.ToString()))!, ReportProofIdentityRole)).Succeeded.ShouldBeTrue();
			}
		});
		foreach (var permission in new[] { ReportProofView, ReportProofAdd, ReportProofSource }) await SetReportProofGrantAsync(actor, permission, true);
		// Bypass the common AlwaysAllow checker only for this real report graph; actual ABP providers/store remain registered.
		var checker = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(ServiceProvider, new ReportProofProductionEnvironment());
		var lazy = new AbpLazyServiceProvider(ServiceProvider);
		var authority = new StudentRecordReportAuthority(GetRequiredService<IUserScopeAccessService>(), GetRequiredService<StudentRecordsAccessService>(), checker,
			GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>()) { LazyServiceProvider = lazy };
		var generator = new StudentRecordReportGenerator(GetRequiredService<IStudentDirectoryAppService>(), GetRequiredService<IStudentRecordChangeAppService>(),
			GetRequiredService<IStudentRecordTransitionAppService>(), GetRequiredService<IRepository<StudentRecordDocumentEvent, Guid>>(), _studentRepository,
			GetRequiredService<StudentRecordsEnrollmentScopeService>(), GetRequiredService<StudentRecordsAccessService>(), GetRequiredService<StudentRecordsPolicyResolver>(), authority) { LazyServiceProvider = lazy };
		var reports = new StudentRecordReportAppService(GetRequiredService<IRepository<StudentRecordReportJob, Guid>>(), generator, authority, GetRequiredService<IBlobContainer>()) { LazyServiceProvider = lazy };
		using (_principalAccessor.Change(ProfilePrincipal(actor)))
			foreach (var permission in new[] { ReportProofView, ReportProofAdd, ReportProofSource }) (await checker.IsGrantedAsync(permission)).ShouldBeTrue(permission);
		return new(fixture.Student.Id, actor, checker, authority, generator, reports, CreateReportProofRunner(generator, authority, checker, GetRequiredService<IExcelService>()));
	}
	private StudentRecordReportRunner CreateReportProofRunner(StudentRecordReportGenerator generator, StudentRecordReportAuthority authority, ShikshaMitraPermissionChecker checker, IExcelService excel) =>
		new(GetRequiredService<IRepository<StudentRecordReportJob, Guid>>(), generator, authority, GetRequiredService<IdentityUserManager>(), _principalAccessor, checker,
			excel, GetRequiredService<IBlobContainer>()) { LazyServiceProvider = new AbpLazyServiceProvider(ServiceProvider) };
	private Task SetReportProofGrantAsync(Guid actor, string permission, bool granted) => WithUnitOfWorkAsync(() =>
		GetRequiredService<IPermissionManager>().SetAsync(permission, "U", actor.ToString(), granted));
	private async Task PublishReportProofPolicyAsync(StudentRecordsPolicy policy)
	{
		var settings = GetRequiredService<IStudentRecordsSettingsAppService>(); var current = await settings.GetAsync(null);
		current = await settings.SaveDraftAsync(new() { ConcurrencyStamp = current.ConcurrencyStamp, Policy = policy });
		await settings.PublishAsync(new() { ConcurrencyStamp = current.ConcurrencyStamp! });
	}
	private Task ExpireReportProofJobAsync(Guid id) => WithUnitOfWorkAsync(async () =>
	{
		var job = await GetRequiredService<IRepository<StudentRecordReportJob, Guid>>().GetAsync(id);
		var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		// Move only the isolated fixture's persisted deadline; do not wait seven days or alter the production clock.
		db.Entry(job).Property(nameof(StudentRecordReportJob.ExpiresAt)).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-1);
		await db.SaveChangesAsync();
	});
	private async Task AssertReportProofFailureAsync(ReportReleaseProof proof, Guid id, string code)
	{
		var job = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordReportJob, Guid>>().GetAsync(id));
		job.Status.ShouldBe("Failed"); job.ErrorCode.ShouldBe(code); job.BlobName.ShouldBeNull(); job.RowCount.ShouldBeNull(); job.GenerationId.ShouldNotBeNull();
		(await GetRequiredService<IBlobContainer>().ExistsAsync($"student-record-reports/{TestTenantId}/{proof.ActorId}/{id}-{job.GenerationId}.xlsx")).ShouldBeFalse();
		(await GetReportProofListAsync(proof)).Items.Single(j => j.Id == id).CanDownload.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => proof.Reports.DownloadAsync(id))).Code.ShouldBe("Students:Records:ReportNotReady");
	}
	// The report graph is constructed directly to inject the real checker; supply the UOW normally created by ABP's application-service proxy.
	private Task<PagedResultDto<StudentRecordReportJobDto>> GetReportProofListAsync(ReportReleaseProof proof) => WithUnitOfWorkAsync(() => proof.Reports.GetListAsync(new()));
	private static StudentRecordReportInput ReportProofInput(Guid studentId) => new() { ReportType = "Directory", StudentId = studentId, ClientKey = Guid.NewGuid().ToString("N") };
	private static string ReadReportProofXml(byte[] bytes)
	{
		using var stream = new MemoryStream(bytes); using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
		return string.Join("\n", archive.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.Ordinal)).Select(e =>
		{
			using var reader = new StreamReader(e.Open()); return reader.ReadToEnd();
		}));
	}
	private sealed record ReportReleaseProof(Guid StudentId, Guid ActorId, ShikshaMitraPermissionChecker Checker, StudentRecordReportAuthority Authority,
		StudentRecordReportGenerator Generator, StudentRecordReportAppService Reports, StudentRecordReportRunner Runner);
	private sealed class ReportProofAfterWorkbook(IExcelService excel, Func<Task> after) : IExcelService
	{
		public bool Invoked { get; private set; }
		public int GeneratedBytes { get; private set; }
		public byte[] GenerateExcelAsBytes(ExcelDocument request, CancellationToken cancellationToken = default)
		{
			var bytes = excel.GenerateExcelAsBytes(request, cancellationToken); GeneratedBytes = bytes.Length;
			Task.Run(after, cancellationToken).GetAwaiter().GetResult(); Invoked = true; return bytes;
		}
	}
	private sealed class ReportProofProductionEnvironment : IHostEnvironment
	{
		public string EnvironmentName { get; set; } = Environments.Production;
		public string ApplicationName { get; set; } = nameof(ReportProofProductionEnvironment);
		public string ContentRootPath { get; set; } = string.Empty;
		public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
	}
}
