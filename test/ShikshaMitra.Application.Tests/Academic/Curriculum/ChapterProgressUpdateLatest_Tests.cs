using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ShikshaMitra.Academic.ClassManagement.ChapterProgress;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp;
using Xunit;
using ProgressEntity = ShikshaMitra.Academic.Syllabus.ChapterProgress;

namespace ShikshaMitra.Academic.Curriculum;

public class ChapterProgressUpdateLatest_Tests : LessonPlanProgressTestBase
{
	[Fact]
	public Task Update_Must_Change_The_Latest_Read_Snapshot_And_Preserve_Older_History() => WithUnitOfWorkAsync(async () =>
	{
		using var tenant = Tenant.Change(TenantId);
		await SeedContextAsync();
		var progress = GetRequiredService<IRepository<ProgressEntity, Guid>>();
		var old = new ProgressEntity(Guid.Parse("00000000-0000-0000-0000-000000000001"), ChapterId, DivisionId);
		var latest = new ProgressEntity(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), ChapterId, DivisionId); latest.UpdateProgress(40);
		typeof(ProgressEntity).GetProperty(nameof(ProgressEntity.CreationTime))!.SetValue(old, DateTime.UtcNow.AddDays(-2));
		typeof(ProgressEntity).GetProperty(nameof(ProgressEntity.CreationTime))!.SetValue(latest, DateTime.UtcNow.AddDays(-1));
		await progress.InsertAsync(old, autoSave: true); await progress.InsertAsync(latest, autoSave: true);
		var scopes = Substitute.For<IUserScopeService>();
		scopes.GetUserScopeAsync(Arg.Any<Guid>(), Arg.Any<System.Threading.CancellationToken>()).Returns(new UserScopeContext { TenantId = TenantId, IsAdmin = true });
		var service = new ChapterProgressAppService(GetRequiredService<IRepository<AssignedSubject, Guid>>(),
			GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>(), progress, GetRequiredService<IRepository<ChapterProgressLog, Guid>>(),
			GetRequiredService<IRepository<Subject, Guid>>(), GetRequiredService<IRepository<TeacherSubjectAllocation, Guid>>(),
			GetRequiredService<IRepository<Standard, Guid>>(), GetRequiredService<IRepository<Division, Guid>>(), scopes,
			GetRequiredService<IGuidGenerator>(), GetRequiredService<ILogger<ChapterProgressAppService>>(), GetRequiredService<IRepository<Employee, Guid>>(),
			GetRequiredService<IReportingManagerAccessService>()) { LazyServiceProvider = new AbpLazyServiceProvider(ServiceProvider) };
		await service.UpdateChapterProgressAsync(ChapterId, new() { DivisionId = DivisionId, Progress = 70, Description = "LOCAL QA latest snapshot regression" });
		(await progress.GetAsync(old.Id)).Progress.ShouldBe(0);
		(await progress.GetAsync(latest.Id)).Progress.ShouldBe(70);
		var visible = await service.GetChapterProgressListAsync(AcademicMediumId, StandardId, DivisionId, SubjectId);
		visible.Records.Single(x => x.ChapterId == ChapterId).Progress.ShouldBe(70);
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateChapterProgressAsync(ChapterId,
			new() { DivisionId = DivisionId, Progress = 70 }))).Code.ShouldBe("ClassManagement:ProgressNoChange");
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateChapterProgressAsync(ChapterId,
			new() { DivisionId = DivisionId, Progress = 60 }))).Code.ShouldBe("ClassManagement:ProgressDecreaseReasonRequired");
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateChapterProgressAsync(ChapterId,
			new() { DivisionId = DivisionId, Progress = 100 }))).Code.ShouldBe("ClassManagement:CompletionEvidenceRequired");
		(await progress.GetAsync(old.Id)).Progress.ShouldBe(0);
		(await progress.GetAsync(latest.Id)).Progress.ShouldBe(70);
	});
}
