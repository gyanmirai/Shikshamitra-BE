using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public sealed class TeacherDiaryFollowUpNotification_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public async Task A_submitted_diary_supports_scoped_manual_follow_up_without_an_automatic_send()
	{
		using var actor = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
			[new Claim(AbpClaimTypes.UserId, ActorId.ToString()), new Claim(AbpClaimTypes.Role, "admin"),
				new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "Test")));
		using var school = Tenant.Change(TenantId, "Synthetic school");
		var diaryId = Guid.NewGuid();
		Guid owner = default;
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			owner = (await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(OldTeacher)).UserId!.Value;
			var plan = new LessonPlan(Guid.NewGuid(), TenantId, board.AcademicYearId, OldTeacher, StandardId, DivisionId,
				SubjectId, ChapterId, DateTime.UtcNow.Date, "Lesson covered in diary", 30);
			plan.Submit();
			plan.Approve(ActorId, "Approved");
			await Plans.InsertAsync(plan, autoSave: true);
			var diary = new TeacherDiary(diaryId, TenantId, plan.Id, OldTeacher, DateTime.UtcNow.Date);
			diary.Update(DateTime.UtcNow.Date, true, "Synthetic topic", null, 25, null, null, null, null, false,
				null, null, null, "Private learner notes", null, null, null, null, null, false, null, "Private diary remarks");
			await GetRequiredService<IRepository<TeacherDiary, Guid>>().InsertAsync(diary, autoSave: true);
		});

		var request = new AcademicNotificationAudienceInput(AcademicNotificationEventKeys.DiaryFollowUpRequested,
			"Academic.Diary", diaryId, new(NotificationGuardianMode.PrimaryOnly, []), [CommunicationChannelType.InApp], []);
		var access = GetRequiredService<IAcademicNotificationAccessService>();
		await WithUnitOfWorkAsync(async () =>
			(await Should.ThrowAsync<BusinessException>(() => access.RequireSendAsync(request, CancellationToken.None)))
				.Code.ShouldBe(NotificationErrorCodes.SourceUnavailable));

		using (GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
			[new Claim(AbpClaimTypes.UserId, owner.ToString()), new Claim(AbpClaimTypes.Role, "admin")], "Test"))))
			await WithTransactionalUnitOfWorkAsync(async () =>
				(await Service.SubmitTeacherDiaryAsync(diaryId)).Status.ShouldBe(TeacherDiaryStatus.Submitted));

		await WithUnitOfWorkAsync(async () =>
		{
			var source = (await access.RequireSendAsync(request, CancellationToken.None)).Source;
			source.IsEligible.ShouldBeTrue();
			source.EligibleRecipients.ShouldBe([new("AssignedTeacher", UserId: owner, EmployeeId: OldTeacher)]);
			source.AudienceSlices!.ShouldHaveSingleItem().Scope.DivisionId.ShouldBe(DivisionId);
			source.Variables.Values.ShouldNotContain(x => x.Value == "Private learner notes" || x.Value == "Private diary remarks");
			(await GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.SourceId == diaryId))
				.ShouldBeEmpty();
		});
	}
}
