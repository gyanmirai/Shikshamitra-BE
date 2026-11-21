using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.Library.Reports;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReportExportAccess_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private const string View = ShikshaMitraSubmodulePermissions.Library.Reports.View;
	private const string Export = ShikshaMitraSubmodulePermissions.Library.Reports.Export;

	[Fact]
	public async Task Export_Captures_Deterministic_Authority_Provenance()
	{
		var fixture = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = Principal(fixture.ActorId);
		var proof = Services();
		var job = await CreateCompletedAsync(proof.Reports, fixture.YearId);
		job.ExtraProperties.ContainsKey("LibraryExportScopeSignature").ShouldBeTrue();
	}

	[Fact]
	public async Task Unchanged_Authority_Can_Download_The_Captured_File()
	{
		var fixture = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = Principal(fixture.ActorId);
		var proof = Services();
		var job = await CreateCompletedAsync(proof.Reports, fixture.YearId);
		(await proof.Reports.DownloadExportAsync(job.Id)).Bytes.ShouldBe(new byte[] { 1, 2, 3 });
	}

	[Fact]
	public async Task Completed_Export_Is_Denied_After_Scope_Revocation_Without_Deleting_Evidence()
	{
		var fixture = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = Principal(fixture.ActorId);
		var proof = Services();
		var job = await CreateCompletedAsync(proof.Reports, fixture.YearId);
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().DeleteAsync(x => x.UserId == fixture.ActorId, true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(fixture.ActorId);
		});
		await Should.ThrowAsync<BusinessException>(() => proof.Reports.DownloadExportAsync(job.Id));
		(await StoredAsync(job.Id)).Status.ShouldBe(HrReportExportStatus.Completed);
	}

	[Fact]
	public async Task Download_Should_Recheck_Authority_After_The_Awaited_Blob_Read()
	{
		var fixture = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = Principal(fixture.ActorId);
		var blobs = Substitute.For<IBlobContainer>();
		blobs.GetOrNullAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => RevokeAndReadAsync());
		var proof = Services(blobs);
		var job = await CreateCompletedAsync(proof.Reports, fixture.YearId);

		await Should.ThrowAsync<BusinessException>(() => proof.Reports.DownloadExportAsync(job.Id));
		await blobs.Received(1).GetOrNullAsync(job.BlobName!, Arg.Any<CancellationToken>());
		(await StoredAsync(job.Id)).Status.ShouldBe(HrReportExportStatus.Completed);

		async Task<Stream?> RevokeAndReadAsync()
		{
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().DeleteAsync(x => x.UserId == fixture.ActorId, true);
				await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(fixture.ActorId);
			});
			return new MemoryStream(new byte[] { 1, 2, 3 });
		}
	}

	[Fact]
	public async Task Completed_Export_Is_Denied_When_A_Student_Moves_Out_Of_Class_Without_Changing_Assignments()
	{
		var fixture = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = Principal(fixture.ActorId);
		var boardId = Guid.NewGuid();
		var mediumId = Guid.NewGuid();
		var stageId = Guid.NewGuid();
		var classId = Guid.NewGuid();
		var otherClassId = Guid.NewGuid();
		var studentId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(new EducationBoard(boardId, fixture.TenantId, EducationBoardType.CBSE, fixture.YearId), true);
			await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(new EducationMedium(mediumId, fixture.TenantId, "English", boardId), true);
			await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(new EducationMediumStage(stageId, fixture.TenantId, "Primary", null, mediumId), true);
			var standards = GetRequiredService<IRepository<Standard, Guid>>();
			await standards.InsertAsync(new Standard(classId, fixture.TenantId, "Class 5", 8, stageId, null), true);
			await standards.InsertAsync(new Standard(otherClassId, fixture.TenantId, "Class 6", 9, stageId, null), true);
			var student = Student.CreateDirectRegistration(studentId, fixture.TenantId, "Library", null, "Reader", fixture.YearId,
				boardId, mediumId, stageId, classId, null, new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Male, "9999999999");
			student.AdditionalDetail!.IsLibraryMember = true;
			await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(x => x.UserId == fixture.ActorId, true);
			await assignments.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), fixture.TenantId, userId: fixture.ActorId,
				academicYearId: fixture.YearId, educationBoardId: boardId, educationMediumId: mediumId,
				educationMediumStageId: stageId, standardId: classId), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(fixture.ActorId);
		});
		var proof = Services();
		var before = await proof.Scope.GetCurrentExecutionScopeAsync();
		(await GetRequiredService<ILibraryAccessService>().GetMemberIdsAsync(before.UserScope)).ShouldContain(studentId);
		var job = await CreateCompletedAsync(proof.Reports, fixture.YearId);
		await WithUnitOfWorkAsync(async () =>
		{
			var students = GetRequiredService<IRepository<Student, Guid>>();
			var student = await students.GetAsync(studentId);
			student.UpdateAcademicPlacement(fixture.YearId, boardId, mediumId, stageId, otherClassId, null);
			await students.UpdateAsync(student, true);
		});
		var after = await proof.Scope.GetCurrentExecutionScopeAsync();
		LibraryReportScopeService.CreateScopeSignature(after).ShouldBe(LibraryReportScopeService.CreateScopeSignature(before));
		(await GetRequiredService<ILibraryAccessService>().GetMemberIdsAsync(after.UserScope)).ShouldNotContain(studentId);
		await Should.ThrowAsync<BusinessException>(() => proof.Reports.DownloadExportAsync(job.Id));
	}

	[Fact]
	public async Task Completed_Export_Rechecks_Current_Persisted_View_Permission()
	{
		var fixture = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = Principal(fixture.ActorId);
		var proof = Services();
		var job = await CreateCompletedAsync(proof.Reports, fixture.YearId);
		await GrantAsync(fixture.ActorId, View, false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => proof.Reports.DownloadExportAsync(job.Id));
	}

	[Fact]
	public async Task Worker_Rechecks_Export_Permission_Instead_Of_Trusting_Queued_Snapshot()
	{
		var fixture = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = Principal(fixture.ActorId);
		var proof = Services();
		var request = Request(fixture.YearId);
		var scope = await proof.Scope.AuthorizeAndNormalizeAsync(request);
		var created = await proof.Reports.CreateExportAsync(request);
		await GrantAsync(fixture.ActorId, Export, false);
		await WithUnitOfWorkAsync(() => proof.Worker.ExecuteAsync(new LibraryReportExportJobArgs
		{
			JobId = created.JobId, TenantId = fixture.TenantId, UserId = fixture.ActorId,
			Request = request, ExecutionScope = scope
		}));
		var stored = await StoredAsync(created.JobId);
		stored.Status.ShouldBe(HrReportExportStatus.Failed);
		stored.BlobName.ShouldBeNull();
	}

	[Fact]
	public async Task Worker_Rebuilds_Authority_And_Uses_Stored_Request_Instead_Of_Queued_Arguments()
	{
		var fixture = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = Principal(fixture.ActorId);
		var proof = Services();
		var created = await WithUnitOfWorkAsync(() => proof.Reports.CreateExportAsync(Request(fixture.YearId)));
		await WithUnitOfWorkAsync(() => proof.Worker.ExecuteAsync(new LibraryReportExportJobArgs
		{
			JobId = created.JobId, TenantId = fixture.TenantId, UserId = fixture.ActorId,
			Request = new() { ReportType = "untrusted queued report" },
			ExecutionScope = new() { TenantId = Guid.NewGuid(), UserScope = new() { IsAdmin = true } }
		}));
		var stored = await StoredAsync(created.JobId);
		stored.Status.ShouldBe(HrReportExportStatus.Completed);
		stored.BlobName.ShouldNotBeNull();
	}

	[Fact]
	public async Task Legacy_Export_Without_Provenance_Requires_Regeneration()
	{
		var fixture = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = Principal(fixture.ActorId);
		var proof = Services();
		var request = Request(fixture.YearId);
		var job = new HrReportExportJob(Guid.NewGuid(), fixture.TenantId, fixture.ActorId, request.ReportType,
			LibraryReportFormat.Excel, "{}", JsonSerializer.Serialize(request), DateTime.UtcNow.AddDays(1));
		job.MarkCompleted("library-legacy-test/" + job.Id, DateTime.UtcNow, 1, 3);
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IHrReportExportJobRepository>().InsertAsync(job, true);
			await GetRequiredService<IBlobContainer>().SaveAsync(job.BlobName!, new byte[] { 1, 2, 3 }, true);
		});
		await Should.ThrowAsync<BusinessException>(() => proof.Reports.DownloadExportAsync(job.Id));
		(await StoredAsync(job.Id)).BlobName.ShouldBe(job.BlobName);
	}

	[Fact]
	public async Task Shared_Export_Does_Not_Grant_The_Owners_Broader_Scope()
	{
		var fixture = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		var recipientId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(recipientId, "library-reader-" + recipientId.ToString("N"), "reader@example.test", fixture.TenantId), true);
			await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), fixture.TenantId,
				userId: recipientId, academicYearId: fixture.YearId, standardId: Guid.NewGuid()), true);
		});
		await GrantAsync(recipientId, View, true);
		var proof = Services();
		HrReportExportJob job;
		using (Principal(fixture.ActorId)) job = await CreateCompletedAsync(proof.Reports, fixture.YearId);
		await WithUnitOfWorkAsync(async () =>
		{
			var row = await GetRequiredService<IHrReportExportJobRepository>().GetAsync(job.Id);
			row.ShareWith([recipientId]);
			await GetRequiredService<IHrReportExportJobRepository>().UpdateAsync(row, true);
		});
		using var recipient = Principal(recipientId);
		await Should.ThrowAsync<BusinessException>(() => proof.Reports.DownloadExportAsync(job.Id));
	}

	private async Task<(Guid TenantId, Guid ActorId, Guid YearId)> SeedAsync()
	{
		var tenantId = Guid.NewGuid();
		var actorId = Guid.NewGuid();
		var yearId = Guid.NewGuid();
		using var school = GetRequiredService<ICurrentTenant>().Change(tenantId);
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(actorId, "library-export-" + actorId.ToString("N"), "export@example.test", tenantId), true);
			await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(new AcademicYear(yearId, tenantId,
				"Library export year", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
			await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: actorId), true);
		});
		await GrantAsync(actorId, View, true);
		await GrantAsync(actorId, Export, true);
		return (tenantId, actorId, yearId);
	}

	private Task GrantAsync(Guid actorId, string permission, bool granted) => WithUnitOfWorkAsync(() =>
		GetRequiredService<IPermissionManager>().SetAsync(permission, "U", actorId.ToString(), granted));

	private IDisposable Principal(Guid actorId) => GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
		[new Claim(AbpClaimTypes.UserId, actorId.ToString()), new Claim(AbpClaimTypes.TenantId, GetRequiredService<ICurrentTenant>().Id!.Value.ToString())], "LibraryExportTest")));

	private static LibraryReportRequestDto Request(Guid yearId) => new()
	{
		ReportType = LibraryReportTypes.BookMaster, Format = LibraryReportFormat.Excel,
		Filters = new() { AcademicYearId = yearId }
	};

	private async Task<HrReportExportJob> CreateCompletedAsync(LibraryReportAppService reports, Guid yearId)
	{
		var created = await WithUnitOfWorkAsync(() => reports.CreateExportAsync(Request(yearId)));
		await WithUnitOfWorkAsync(async () =>
		{
			var job = await GetRequiredService<IHrReportExportJobRepository>().GetAsync(created.JobId);
			job.MarkCompleted("library-export-access/" + job.Id, DateTime.UtcNow, 1, 3);
			await GetRequiredService<IHrReportExportJobRepository>().UpdateAsync(job, true);
			await GetRequiredService<IBlobContainer>().SaveAsync(job.BlobName!, new byte[] { 1, 2, 3 }, true);
		});
		return await StoredAsync(created.JobId);
	}

	private Task<HrReportExportJob> StoredAsync(Guid id) => WithUnitOfWorkAsync(() => GetRequiredService<IHrReportExportJobRepository>().GetAsync(id));

	private (LibraryReportAppService Reports, LibraryReportScopeService Scope, LibraryReportExportBackgroundJob Worker) Services(IBlobContainer? downloadBlobs = null)
	{
		var checker = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(ServiceProvider);
		var provider = new ReportProvider(ServiceProvider, checker);
		var scope = ActivatorUtilities.CreateInstance<LibraryReportScopeService>(provider);
		provider.Scope = scope;
		var reports = new LibraryReportAppService(GetRequiredService<LibraryReportGenerator>(), scope, GetRequiredService<ILibraryAiReportPlanner>(),
			GetRequiredService<IHrReportExportJobRepository>(), GetRequiredService<IBackgroundJobManager>(), downloadBlobs ?? GetRequiredService<IBlobContainer>())
			{ LazyServiceProvider = new AbpLazyServiceProvider(provider) };
		return (reports, scope, ActivatorUtilities.CreateInstance<LibraryReportExportBackgroundJob>(provider));
	}

	private sealed class ReportProvider(IServiceProvider inner, IPermissionChecker checker) : IServiceProvider
	{
		public LibraryReportScopeService? Scope { get; set; }
		public object? GetService(Type type) => type == typeof(IPermissionChecker) ? checker :
			type == typeof(LibraryReportScopeService) && Scope != null ? Scope : inner.GetService(type);
	}
}
