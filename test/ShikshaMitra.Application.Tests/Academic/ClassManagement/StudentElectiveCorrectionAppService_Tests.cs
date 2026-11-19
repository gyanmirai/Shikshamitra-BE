using System;
using System.Security.Claims;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

public class StudentElectiveCorrectionAppService_Tests : ShikshaMitraApplicationTestBase<ElectiveCorrectionTransactionTestModule>
{
	private IStudentElectiveCorrectionAppService Service => GetRequiredService<IStudentElectiveCorrectionAppService>();
	private IRepository<T, Guid> Repo<T>() where T : class, Volo.Abp.Domain.Entities.IEntity<Guid> => GetRequiredService<IRepository<T, Guid>>();

	[Fact]
	public Task Forged_admin_claim_does_not_authorize_locked_corrections() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var user = await Repo<IdentityUser>().InsertAsync(new IdentityUser(Guid.NewGuid(), "ordinary", "ordinary@test.local", tenantId), true);
		using var principal = SignIn(user.Id);
		await Should.ThrowAsync<AbpAuthorizationException>(() => Service.PreviewAsync(new()));
	});

	[Fact]
	public Task Admin_review_is_required_and_correction_preserves_lock_and_original_receipt() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId, "Synthetic school");
		var role = await Repo<IdentityRole>().InsertAsync(new IdentityRole(Guid.NewGuid(), "admin", tenantId), true);
		var user = new IdentityUser(Guid.NewGuid(), "corrector", "corrector@test.local", tenantId);
		user.AddRole(role.Id);
		await Repo<IdentityUser>().InsertAsync(user, true);
		using var principal = SignIn(user.Id);
		var stage = await Repo<EducationMediumStage>().GetAsync(await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider));
		var medium = await Repo<EducationMedium>().GetAsync(stage.EducationMediumId);
		var board = await Repo<EducationBoard>().GetAsync(medium.EducationBoardId);
		var standard = await Repo<Standard>().InsertAsync(new Standard(Guid.NewGuid(), tenantId, "Correction class", 8, stage.Id, null), true);
		var division = await Repo<Division>().InsertAsync(new Division(Guid.NewGuid(), tenantId, "A", standard.Id, 30, "COR-A"), true);
		var classId = standard.Id; var divisionId = division.Id;
		var student = await Repo<Student>().InsertAsync(new Student(Guid.NewGuid(), tenantId, "Elective", null, "Student",
			board.AcademicYearId, board.Id, medium.Id, stage.Id, classId, divisionId, status: StudentLifeCycleStatusEnum.Enrolled), true);
		await Repo<StudentEnrollment>().InsertAsync(
			StudentEnrollment.Create(Guid.NewGuid(), tenantId, student.Id, board.AcademicYearId, classId, divisionId), true);
		var first = await Repo<Subject>().InsertAsync(new Subject(Guid.NewGuid(), tenantId, "Music", "#00A76F", medium.Id), true);
		var second = await Repo<Subject>().InsertAsync(new Subject(Guid.NewGuid(), tenantId, "Art", "#00A76F", medium.Id), true);
		var group = new ElectiveGroup(Guid.NewGuid(), tenantId, "Arts", "#00A76F", medium.Id);
		group.AddSubject(first.Id); group.AddSubject(second.Id);
		await Repo<ElectiveGroup>().InsertAsync(group, true);
		var selection = new StudentElectiveSelection(Guid.NewGuid(), tenantId, student.Id, group.Id, first.Id, medium.Id, first.Name);
		selection.LockSelection(); await Repo<StudentElectiveSelection>().InsertAsync(selection, true);
		var receipt = await Repo<StudentElectivePreferenceSubmission>().InsertAsync(new StudentElectivePreferenceSubmission(Guid.NewGuid(), tenantId,
			student.Id, board.AcademicYearId, group.Id, first.Id, first.Name, user.Id, DateTime.UtcNow, Guid.NewGuid()), true);
		const string corrected = AcademicNotificationEventKeys.ElectiveLockedSelectionCorrected;
		var header = await Repo<CommunicationTemplate>().InsertAsync(new CommunicationTemplate(Guid.NewGuid(), tenantId)
			{ TemplateName = "Elective correction notice" }, true);
		var template = new CommunicationTemplateRevision(Guid.NewGuid(), tenantId, header.Id, 1, corrected, "Guardian",
			CommunicationChannelType.InApp, "en", "Elective update", "Open the school inbox.", 1);
		template.Publish(user.Id, DateTimeOffset.UtcNow);
		await Repo<CommunicationTemplateRevision>().InsertAsync(template, true);
		var rule = new NotificationRule(Guid.NewGuid(), tenantId, corrected, NotificationRuleScope.Create());
		var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
			[CommunicationChannelType.InApp], [header.Id], NotificationRuleTimingConfiguration.Default with { QuietHoursEnabled = false }, "en");
		rule.Configure(true, GetRequiredService<NotificationRuleConfigurationCodec>().Serialize(
			config, GetRequiredService<AcademicNotificationCatalogue>().Get(corrected)), rule.ConcurrencyStamp);
		await Repo<NotificationRule>().InsertAsync(rule, true);
		var input = new ConfirmStudentElectiveCorrectionInput { AcademicMediumId = medium.Id, ClassId = classId, DivisionId = divisionId,
			ElectiveGroupId = group.Id, StudentId = student.Id, SelectedSubjectId = second.Id, PreferredSubjectId = second.Id, ReviewToken = "not-reviewed" };
		await Should.ThrowAsync<BusinessException>(() => Service.ConfirmAsync(input));
		var scopeOptions = GetRequiredService<IOptions<UserScopeOptions>>().Value;
		var originalAdminRoles = scopeOptions.AdminRoles.ToList();
		try
		{
			scopeOptions.AdminRoles.Clear();
			await Should.ThrowAsync<AbpAuthorizationException>(() => Service.PreviewAsync(input));
		}
		finally
		{
			scopeOptions.AdminRoles = originalAdminRoles;
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(user.Id);
		}
		var originalClassId = input.ClassId;
		input.ClassId = Guid.NewGuid();
		await Should.ThrowAsync<BusinessException>(() => Service.PreviewAsync(input));
		input.ClassId = originalClassId;
		using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
			await Should.ThrowAsync<AbpAuthorizationException>(() => Service.PreviewAsync(input));
		var review = await Service.PreviewAsync(input);
		review.Impacts.ShouldContain(x => x.Area == "Subject allocation" && x.Description.Contains("not published"));
		review.PreviousSubject.ShouldBe("Music"); review.SelectedSubject.ShouldBe("Art");
		(await Repo<StudentElectiveSelection>().GetAsync(selection.Id)).SelectedSubjectId.ShouldBe(first.Id);
		var examId = Guid.NewGuid();
		var mark = new ExamMark(Guid.NewGuid(), examId, student.Id, first.Id, divisionId, 78, false, tenantId: tenantId);
		mark.Publish(); await Repo<ExamMark>().InsertAsync(mark, true);
		input.ReviewToken = review.ReviewToken;
		(await Should.ThrowAsync<BusinessException>(() => Service.ConfirmAsync(input))).Code.ShouldBe("ClassManagement:ElectiveCorrectionReviewChanged");
		var resultHeader = new ExamResultHeader(Guid.NewGuid(), examId, divisionId, user.Id, tenantId);
		resultHeader.Publish(user.Id); resultHeader.Freeze(user.Id, "Original result");
		await Repo<ExamResultHeader>().InsertAsync(resultHeader, true);
		var result = await Repo<ExamResultDetail>().InsertAsync(new ExamResultDetail(Guid.NewGuid(), resultHeader.Id, examId,
			student.Id, divisionId, 100, 78, 78, "B", 1, 1, ExamStudentResultStatus.Pass, tenantId), true);
		var seat = await Repo<ExamSeatAllocation>().InsertAsync(new ExamSeatAllocation(Guid.NewGuid(), tenantId, Guid.NewGuid(), examId,
			student.Id, Guid.NewGuid(), 7, null, true), true);
		review = await Service.PreviewAsync(input);
		review.Impacts.ShouldContain(x => x.Area == "Exam marks" && x.RecordId == mark.Id);
		review.Impacts.ShouldContain(x => x.Area == "Exam results" && x.RecordId == result.Id);
		review.Impacts.ShouldContain(x => x.Area == "Exam seating" && x.RecordId == seat.Id);
		await Repo<AssignedSubject>().InsertAsync(new AssignedSubject(Guid.NewGuid(), tenantId, classId, second.Id,
			second.Name, SubjectStatus.Published, SubjectType.Elective, group.Id), true);
		input.ReviewToken = review.ReviewToken;
		(await Should.ThrowAsync<BusinessException>(() => Service.ConfirmAsync(input))).Code.ShouldBe("ClassManagement:ElectiveCorrectionReviewChanged");
		review = await Service.PreviewAsync(input);
		review.Impacts.ShouldNotContain(x => x.Area == "Subject allocation" && x.Description.Contains("not published"));
		input.ReviewToken = review.ReviewToken;
		(await Repo<NotificationIntent>().GetListAsync(x => x.EventKey == corrected)).ShouldBeEmpty();
		await Service.ConfirmAsync(input);
		var saved = (await Repo<StudentElectiveSelection>().GetQueryableAsync()).Where(x => x.Id == selection.Id)
			.Select(x => new { x.SelectedSubjectId, x.IsLocked, x.StudentPreference, x.PreferenceOverrideSubjectId }).Single();
		saved.SelectedSubjectId.ShouldBe(second.Id); saved.IsLocked.ShouldBeTrue(); saved.StudentPreference.ShouldBe("Art");
		saved.PreferenceOverrideSubjectId.ShouldBe(second.Id);
		(await Repo<StudentElectivePreferenceSubmission>().GetAsync(receipt.Id)).PreferredSubjectId.ShouldBe(first.Id);
		(await Repo<ExamMark>().GetAsync(mark.Id)).MarksObtained.ShouldBe(78);
		(await Repo<ExamResultHeader>().GetAsync(resultHeader.Id)).Status.ShouldBe(ExamResultStatus.Frozen);
		(await Repo<ExamSeatAllocation>().GetAsync(seat.Id)).SeatNumber.ShouldBe(7);
		var audit = (await Repo<StudentElectiveCorrection>().GetListAsync(x => x.StudentId == student.Id)).ShouldHaveSingleItem();
		audit.ChangedByUserId.ShouldBe(user.Id);
		await Should.ThrowAsync<BusinessException>(() => Service.ConfirmAsync(input));
		var intent = (await Repo<NotificationIntent>().GetListAsync(x => x.EventKey == corrected)).ShouldHaveSingleItem();
		intent.SourceId.ShouldBe(audit.Id);
		var source = await GetRequiredService<NotificationIntentSourceReader>()
			.LoadAsync(intent, DateTimeOffset.UtcNow, CancellationToken.None);
		source.Runtime.SourceScope.DivisionId.ShouldBe(divisionId);
		source.CurrentRecipients.ShouldBe([new("Student", StudentId: student.Id)]);
	});

	private IDisposable SignIn(Guid userId) => GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
		[new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.Role, "admin")], "test")));
}

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public sealed class ElectiveCorrectionTransactionTestModule : AbpModule
{
	public override void PostConfigureServices(ServiceConfigurationContext context)
	{
		// Confirm opens its own serializable unit, which must participate in intent capture.
		context.Services.Replace(ServiceDescriptor.Transient<IUnitOfWorkManager, UnitOfWorkManager>());
	}
}
