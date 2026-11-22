using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;
using ClassTeacherRecord = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.StudentAcademic360;

public sealed class Student360NotificationAccess_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public async Task Student_360_messages_and_meeting_requests_queue_only_the_reviewed_family_and_selected_teacher()
	{
		GetRequiredService<IConfiguration>()["App:ClientUrl"] = "https://school.example.invalid";
		using var school = Tenant.Change(TenantId, "Synthetic school");
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
			[new Claim(AbpClaimTypes.UserId, ActorId.ToString()), new Claim(AbpClaimTypes.TenantId, TenantId.ToString()),
				new Claim(AbpClaimTypes.Role, "admin")], "Test")));
		var studentId = Guid.NewGuid();
		var unresolvedStudentId = Guid.NewGuid();
		var father = Guid.NewGuid();
		var self = Guid.NewGuid();
		var studentReference = new NotificationRecipientRefDto("Student", StudentId: studentId);
		NotificationRecipientRefDto classTeacher = null!;
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
			await GetRequiredService<IUserScopeAssignmentRepository>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(),
				TenantId, ActorId, academicYearId: board.AcademicYearId, educationBoardId: board.Id, educationMediumId: medium.Id,
				educationMediumStageId: standard.EducationMediumStageId, standardId: StandardId, divisionId: DivisionId), autoSave: true);
			var student = new Student(studentId, TenantId, "Selected", null, "Student",
				board.AcademicYearId, board.Id, medium.Id, standard.EducationMediumStageId, StandardId, DivisionId,
				status: StudentLifeCycleStatusEnum.Enrolled);
			var family = new StudentFamilyDetail(studentId, TenantId);
			family.UpdateFatherDetails(Contact("Test father"));
			family.UpdateMotherDetails(Contact("Test mother", "mother@example.invalid"));
			StudentFamilyContactSelection.Select(student, "mother", family);
			await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, autoSave: true);
			await GetRequiredService<IRepository<StudentFamilyDetail, Guid>>().InsertAsync(family, autoSave: true);
			var unresolvedStudent = new Student(unresolvedStudentId, TenantId, "Unresolved", null, "Student",
				board.AcademicYearId, board.Id, medium.Id, standard.EducationMediumStageId, StandardId, DivisionId,
				status: StudentLifeCycleStatusEnum.Enrolled);
			var unresolvedFamily = new StudentFamilyDetail(unresolvedStudentId, TenantId);
			unresolvedFamily.UpdateFatherDetails(Contact("Second father", "father2@example.invalid"));
			unresolvedFamily.UpdateMotherDetails(Contact("Second mother", "mother2@example.invalid"));
			await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(unresolvedStudent, autoSave: true);
			await GetRequiredService<IRepository<StudentFamilyDetail, Guid>>().InsertAsync(unresolvedFamily, autoSave: true);
			var teacher = await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(OldTeacher);
			classTeacher = new("ClassTeacher", UserId: teacher.UserId, EmployeeId: teacher.Id);
			await GetRequiredService<IRepository<ClassTeacherRecord, Guid>>().InsertAsync(
				new(Guid.NewGuid(), TenantId, StandardId, DivisionId, teacher.Id, medium.Id), autoSave: true);
			foreach (var (userId, accountType, relationship) in new[]
			{
				(father, "guardian", "father"), (self, "student", "self")
			})
			{
				await GetRequiredService<IIdentityUserRepository>().InsertAsync(
					new IdentityUser(userId, relationship + userId.ToString("N"), $"{userId:N}@example.invalid", TenantId), autoSave: true);
				await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(
					new StudentPortalAccountLink(Guid.NewGuid(), TenantId, studentId, userId, accountType, relationship, ActorId), autoSave: true);
			}
			await PublishTemplateAsync(AcademicNotificationEventKeys.Student360GuardianMessage, "Guardian");
			await PublishTemplateAsync(AcademicNotificationEventKeys.Student360MeetingRequested, "Guardian");
			await PublishTemplateAsync(AcademicNotificationEventKeys.Student360MeetingRequested, "ClassTeacher");
		});

		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IStudentAcademic360AppService>()
				.GetTabAsync(studentId, "timetable", new StudentAcademic360Input());
			var notices = GetRequiredService<IRepository<NotificationIntent, Guid>>();
			(await notices.GetListAsync(x => x.SourceId == studentId)).ShouldBeEmpty();

			var access = GetRequiredService<IAcademicNotificationAccessService>();
			foreach (var key in new[] { AcademicNotificationEventKeys.Student360GuardianMessage, AcademicNotificationEventKeys.Student360MeetingRequested })
			{
				var context = await access.RequireSendAsync(new(key, "Academic.Student360", studentId,
					new(NotificationGuardianMode.PrimaryOnly, []), [CommunicationChannelType.InApp], []), CancellationToken.None);
				var expected = key == AcademicNotificationEventKeys.Student360MeetingRequested
					? new[] { studentReference, classTeacher }
					: [studentReference];
				context.Source.EligibleRecipients.ShouldBe(expected);
				context.Source.AudienceSlices!.ShouldHaveSingleItem().Scope.DivisionId.ShouldBe(DivisionId);
			}
			(await notices.GetListAsync(x => x.SourceId == studentId)).ShouldBeEmpty();
		});

		var app = GetRequiredService<IAcademicNotificationManualAppService>();
		var request = new PreviewAcademicNotificationManualDto(AcademicNotificationEventKeys.Student360GuardianMessage,
			"Academic.Student360", studentId, [studentReference], [CommunicationChannelType.InApp]);
		var reviewToken = Guid.Empty;
		await WithUnitOfWorkAsync(async () =>
		{
			var audience = await app.GetStudent360AudienceAsync(studentId, request.EventKey);
			audience.Family.ShouldBe([new("mother", true), new("father", false), new("self", false)]);
			audience.Staff.ShouldBeEmpty();
			JsonSerializer.SerializeToElement(audience, new JsonSerializerOptions(JsonSerializerDefaults.Web))
				.TryGetProperty("primaryGuardianNotConfigured", out var configuredStatus).ShouldBeTrue();
			configuredStatus.GetBoolean().ShouldBeFalse();
			var unresolved = await app.GetStudent360AudienceAsync(unresolvedStudentId, request.EventKey);
			unresolved.Family.ShouldBe([new("father", false), new("mother", false)]);
			JsonSerializer.SerializeToElement(unresolved, new JsonSerializerOptions(JsonSerializerDefaults.Web))
				.TryGetProperty("primaryGuardianNotConfigured", out var unresolvedStatus).ShouldBeTrue();
			unresolvedStatus.GetBoolean().ShouldBeTrue();
			await Should.ThrowAsync<AbpAuthorizationException>(() =>
				app.GetStudent360AudienceAsync(Guid.NewGuid(), request.EventKey));
			await Should.ThrowAsync<AbpAuthorizationException>(() => app.PreviewAsync(request with
			{
				SelectedRecipients = [new("Student", StudentId: Guid.NewGuid())]
			}));
			var invalid = await Should.ThrowAsync<BusinessException>(() =>
				app.PreviewAsync(request with { SelectedFamilyRelationships = ["guardian"] }));
			invalid.Code.ShouldBe(NotificationErrorCodes.RecipientIneligible);
			var defaultReview = await app.PreviewAsync(request);
			defaultReview.EligibleRecipientCount.ShouldBe(0);
			defaultReview.ReviewToken.ShouldBeNull();
			var review = await app.PreviewAsync(request with { SelectedFamilyRelationships = ["father"] });
			review.EligibleRecipientCount.ShouldBe(1);
			reviewToken = review.ReviewToken.ShouldNotBeNull(System.Text.Json.JsonSerializer.Serialize(new { review.BlockingIssues, review.Channels }));
			(await GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.SourceId == studentId)).ShouldBeEmpty();
		});

		await WithTransactionalUnitOfWorkAsync(async () =>
		{
			var send = new ConfirmAcademicNotificationManualDto(reviewToken, "student360-father-only");
			var operation = await app.SendAsync(send);
			operation.Status.ShouldBe(CommunicationCampaignStatus.Queued);
			(await app.SendAsync(send)).OperationId.ShouldBe(operation.OperationId);
			var intent = (await GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.CampaignId == operation.OperationId))
				.ShouldHaveSingleItem();
			var source = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, default);
			source.Runtime.SelectedFamilyRelationships.ShouldBe(["father"]);
			var resolver = GetRequiredService<System.Collections.Generic.IEnumerable<INotificationRuntimeRecipientResolver>>()
				.Single(x => x.Supports(request.EventKey));
			var recipient = (await resolver.ResolveAsync(source, default)).ShouldHaveSingleItem();
			recipient.IsEligible.ShouldBeTrue();
			recipient.Recipient.UserId.ShouldBe(father);
		});

		var meeting = request with
		{
			EventKey = AcademicNotificationEventKeys.Student360MeetingRequested,
			SelectedRecipients = [studentReference, classTeacher],
			SelectedFamilyRelationships = ["father"]
		};
		await WithUnitOfWorkAsync(async () =>
		{
			var audience = await app.GetStudent360AudienceAsync(studentId, meeting.EventKey);
			audience.Staff.ShouldBe([classTeacher]);
			await Should.ThrowAsync<AbpAuthorizationException>(() => app.PreviewAsync(meeting with
			{
				SelectedRecipients = [studentReference, classTeacher with { EmployeeId = OtherTeacher }]
			}));
			var familyOnly = await app.PreviewAsync(meeting with { SelectedRecipients = [studentReference] });
			familyOnly.EligibleRecipientCount.ShouldBe(1);
			var reviewed = await app.PreviewAsync(meeting);
			reviewed.EligibleRecipientCount.ShouldBe(2);
			reviewed.Channels.ShouldHaveSingleItem().ReadyDeliveryCount.ShouldBe(2);
			reviewToken = reviewed.ReviewToken.ShouldNotBeNull();
			(await GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.EventKey == meeting.EventKey)).ShouldBeEmpty();
		});

		await WithTransactionalUnitOfWorkAsync(async () =>
		{
			var send = new ConfirmAcademicNotificationManualDto(reviewToken, "student360-meeting");
			var operation = await app.SendAsync(send);
			operation.Status.ShouldBe(CommunicationCampaignStatus.Queued);
			(await app.SendAsync(send)).OperationId.ShouldBe(operation.OperationId);
			var intent = (await GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.CampaignId == operation.OperationId))
				.ShouldHaveSingleItem();
			var source = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, default);
			source.CurrentRecipients.ShouldBe([studentReference, classTeacher], ignoreOrder: true);
			var resolver = GetRequiredService<System.Collections.Generic.IEnumerable<INotificationRuntimeRecipientResolver>>()
				.Single(x => x.Supports(meeting.EventKey));
			var targets = await resolver.ResolveAsync(source, default);
			targets.Count.ShouldBe(2);
			targets.ShouldAllBe(x => x.IsEligible);
			targets.Single(x => x.Recipient.Kind == "Guardian").Recipient.UserId.ShouldBe(father);
			targets.Single(x => x.Recipient.Kind == "ClassTeacher").Recipient.ShouldBe(classTeacher);
		});
	}

	private async Task PublishTemplateAsync(string eventKey, string recipientKind)
	{
		var template = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = $"{eventKey} {recipientKind}" };
		var definition = GetRequiredService<AcademicNotificationCatalogue>().Get(eventKey);
		var content = GetRequiredService<NotificationDefaultTemplateFactory>().Create(definition, recipientKind, CommunicationChannelType.InApp, "en");
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1, eventKey, recipientKind,
			CommunicationChannelType.InApp, "en", content.Subject, content.Body, definition.SchemaVersion);
		revision.Publish(ActorId, DateTimeOffset.UtcNow);
		await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(template, autoSave: true);
		await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
		template.SetNotificationRevision(revision.Id);
		await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().UpdateAsync(template, autoSave: true);
	}

	private static ParentGuardianDetails Contact(string name, string? email = null) => new(name, null, null, email, null, null,
		null, null, null, null, null, null, null);
}
