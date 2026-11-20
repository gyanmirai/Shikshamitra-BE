using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
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

namespace ShikshaMitra.Academic.StudentAcademic360;

public sealed class Student360NotificationAccess_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public async Task Student_360_read_is_silent_and_only_the_reviewed_family_receives_the_send()
	{
		GetRequiredService<IConfiguration>()["App:ClientUrl"] = "https://school.example.invalid";
		using var school = Tenant.Change(TenantId, "Synthetic school");
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
			[new Claim(AbpClaimTypes.UserId, ActorId.ToString()), new Claim(AbpClaimTypes.TenantId, TenantId.ToString()),
				new Claim(AbpClaimTypes.Role, "admin")], "Test")));
		var studentId = Guid.NewGuid();
		var father = Guid.NewGuid();
		var self = Guid.NewGuid();
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
			var template = new CommunicationTemplate(Guid.NewGuid(), TenantId) { TemplateName = "Academic message" };
			var revision = new CommunicationTemplateRevision(Guid.NewGuid(), TenantId, template.Id, 1,
				AcademicNotificationEventKeys.Student360GuardianMessage, "Guardian", CommunicationChannelType.InApp, "en",
				"Academic update from {{school_name}}", "Hello {{recipient_name}}. Please contact the school. {{source_link}}", 1);
			revision.Publish(ActorId, DateTimeOffset.UtcNow);
			await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().InsertAsync(template, autoSave: true);
			await GetRequiredService<IRepository<CommunicationTemplateRevision, Guid>>().InsertAsync(revision, autoSave: true);
			template.SetNotificationRevision(revision.Id);
			await GetRequiredService<IRepository<CommunicationTemplate, Guid>>().UpdateAsync(template, autoSave: true);
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
				context.Source.EligibleRecipients.ShouldBe([new("Student", StudentId: studentId)]);
				context.Source.AudienceSlices!.ShouldHaveSingleItem().Scope.DivisionId.ShouldBe(DivisionId);
			}
			(await notices.GetListAsync(x => x.SourceId == studentId)).ShouldBeEmpty();
		});

		var app = GetRequiredService<IAcademicNotificationManualAppService>();
		var request = new PreviewAcademicNotificationManualDto(AcademicNotificationEventKeys.Student360GuardianMessage,
			"Academic.Student360", studentId, [new("Student", StudentId: studentId)], [CommunicationChannelType.InApp]);
		var reviewToken = Guid.Empty;
		await WithUnitOfWorkAsync(async () =>
		{
			var audience = await app.GetStudent360AudienceAsync(studentId, request.EventKey);
			audience.Family.ShouldBe([new("mother", true), new("father", false), new("self", false)]);
			audience.Staff.ShouldBeEmpty();
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
			var intent = (await GetRequiredService<IRepository<NotificationIntent, Guid>>().GetListAsync(x => x.SourceId == studentId))
				.ShouldHaveSingleItem();
			var source = await GetRequiredService<NotificationIntentSourceReader>().LoadAsync(intent, DateTimeOffset.UtcNow, default);
			source.Runtime.SelectedFamilyRelationships.ShouldBe(["father"]);
			var resolver = GetRequiredService<System.Collections.Generic.IEnumerable<INotificationRuntimeRecipientResolver>>()
				.Single(x => x.Supports(request.EventKey));
			var recipient = (await resolver.ResolveAsync(source, default)).ShouldHaveSingleItem();
			recipient.IsEligible.ShouldBeTrue();
			recipient.Recipient.UserId.ShouldBe(father);
		});
	}

	private static ParentGuardianDetails Contact(string name, string? email = null) => new(name, null, null, email, null, null,
		null, null, null, null, null, null, null);
}
