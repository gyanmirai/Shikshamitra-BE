using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public class AcademicNotificationRecipientResolver_Tests
{
	private readonly Guid _tenant = Guid.NewGuid();
	private readonly Guid _actor = Guid.NewGuid();
	private readonly List<AcademicNotificationStudent> _students = [];
	private readonly List<AcademicNotificationStaff> _staff = [];
	private readonly IAcademicNotificationAccessService _access = Substitute.For<IAcademicNotificationAccessService>();
	private readonly IAcademicNotificationRecipientProjection _projection = Substitute.For<IAcademicNotificationRecipientProjection>();
	private readonly UserScopeContext _scope;
	private NotificationSourceSnapshot _source;
	private readonly AcademicNotificationRecipientResolver _resolver;
	private const string EventKey = "Academic.Homework.Published";

	public AcademicNotificationRecipientResolver_Tests()
	{
		_scope = new() { TenantId = _tenant, UserId = _actor, IsAdmin = true };
		_source = new(true, null, "published-v1", null, new Dictionary<string, NotificationVariableValueDto>(), []);
		_access.RequireSendAsync(Arg.Any<AcademicNotificationAudienceInput>(), Arg.Any<CancellationToken>())
			.Returns(_ => new AcademicNotificationAccessContext(_tenant, _actor, _scope, _source));
		_projection.LoadAsync(_tenant, Arg.Any<IReadOnlyList<NotificationRecipientRefDto>>(), Arg.Any<CancellationToken>())
			.Returns(_ => new AcademicNotificationRecipientData(_students, _staff));
		_resolver = new(_access, _projection, new AcademicNotificationCatalogue([]));
	}

	[Theory]
	[InlineData("father")]
	[InlineData("mother")]
	[InlineData("guardian")]
	public async Task Saved_primary_is_the_only_family_recipient_by_default(string primary)
	{
		var student = AddStudent(primary);
		var result = await ResolveAsync();
		result.EligibleRecipientCount.ShouldBe(1);
		result.Recipients.Single(x => x.IsEligible).Recipient.ShouldBe(new("Guardian", student.Student.Id, primary));
	}

	[Fact]
	public async Task Ambiguous_primary_has_an_explicit_reason_and_never_falls_back_to_student()
	{
		var student = AddStudent(null);
		AddAccount(student, "student", "self");
		var result = await ResolveAsync();
		result.EligibleRecipientCount.ShouldBe(0);
		result.Recipients.ShouldContain(x => x.ExclusionCode == NotificationErrorCodes.PrimaryGuardianNotConfigured);
		result.Recipients.ShouldNotContain(x => x.Recipient.Kind == "StudentAccount");
	}

	[Fact]
	public async Task Missing_primary_destination_does_not_choose_the_other_parent_or_block_another_child()
	{
		var first = AddStudent("mother");
		first.Family!.UpdateMotherDetails(Profile("Mother", null));
		AddStudent("father");
		var result = await ResolveAsync();
		result.EligibleRecipientCount.ShouldBe(1);
		result.ExcludedRecipientCount.ShouldBe(1);
		result.Recipients.ShouldContain(x => !x.IsEligible && x.Recipient.StudentId == first.Student.Id);
	}

	[Fact]
	public async Task Additional_guardian_is_explicit_and_requires_a_current_saved_relationship()
	{
		var student = AddStudent("mother");
		var result = await ResolveAsync(new(NotificationGuardianMode.PrimaryAndSelected, ["father", "guardian"]));
		result.EligibleRecipientCount.ShouldBe(3);
		student.Family!.UpdateGuardianDetails(null);
		result = await ResolveAsync(new(NotificationGuardianMode.PrimaryAndSelected, ["guardian"]));
		result.EligibleRecipientCount.ShouldBe(1);
		result.Recipients.ShouldContain(x => x.Recipient.FamilyRelationship == "guardian" && !x.IsEligible);
	}

	[Fact]
	public async Task Pending_guardian_review_blocks_external_contacts_and_retained_links()
	{
		var student = AddStudent("mother");
		AddAccount(student, "guardian", "mother");
		_students[0] = student with { GuardianReviewPending = true };
		var result = await ResolveAsync(channels: [CommunicationChannelType.Email, CommunicationChannelType.InApp]);
		result.EligibleRecipientCount.ShouldBe(0);
		result.Recipients.Count.ShouldBe(2);
		result.Recipients.All(x => x.ExclusionCode == "GuardianAccessReviewPending").ShouldBeTrue();
	}

	[Fact]
	public async Task Student_account_requires_opt_in_and_an_active_verified_link()
	{
		var student = AddStudent("mother");
		var account = AddAccount(student, "student", "self");
		(await ResolveAsync()).Recipients.ShouldNotContain(x => x.Recipient.Kind == "StudentAccount");
		var policy = new AcademicNotificationRecipientPolicy(NotificationGuardianMode.PrimaryOnly, [], true);
		var result = await ResolveAsync(policy, [CommunicationChannelType.InApp]);
		result.Recipients.ShouldContain(x => x.Recipient.UserId == account.User.Id && x.IsEligible);
		account.User.SetIsActive(false);
		(await ResolveAsync(policy, [CommunicationChannelType.InApp])).Recipients.ShouldNotContain(x => x.IsEligible);
	}

	[Fact]
	public async Task Shared_email_does_not_create_inbox_or_push_ownership()
	{
		AddStudent("mother");
		var result = await ResolveAsync(channels: [CommunicationChannelType.InApp, CommunicationChannelType.WebPush]);
		result.EligibleRecipientCount.ShouldBe(0);
		result.Recipients.Count.ShouldBe(2);
		result.Recipients.All(x => x.ExclusionCode == "VerifiedAccountRequired").ShouldBeTrue();
	}

	[Fact]
	public async Task Foreign_tenant_and_out_of_scope_children_are_denied_before_contact_preview()
	{
		AddStudent("mother", tenant: Guid.NewGuid());
		await Should.ThrowAsync<AbpAuthorizationException>(() => ResolveAsync());
		_students.Clear();
		_source = _source with { EligibleRecipients = [] };
		AddStudent("mother");
		_scope.IsAdmin = false;
		await Should.ThrowAsync<AbpAuthorizationException>(() => ResolveAsync());
	}

	[Fact]
	public async Task Client_cannot_widen_the_source_audience_with_a_late_or_non_elective_student()
	{
		AddStudent("mother");
		var input = Input() with { AffectedRecipients = [new("Student", Guid.NewGuid())] };
		await Should.ThrowAsync<AbpAuthorizationException>(() => _resolver.ResolveAsync(input, default));
	}

	[Fact]
	public async Task Departed_student_is_excluded_even_if_previously_admitted()
	{
		var student = AddStudent("mother");
		student.Student.ChangeStatus(StudentLifeCycleStatusEnum.Left);
		var result = await ResolveAsync();
		result.EligibleRecipientCount.ShouldBe(0);
		result.Recipients.ShouldContain(x => x.ExclusionCode == "StudentNotEnrolled");
	}

	[Fact]
	public async Task No_reviewer_does_not_expand_to_every_administrator()
	{
		_source = _source with { EligibleRecipients = [] };
		var input = Input() with { EventKey = "Academic.Evaluation.GradingDue", SourceType = "Academic.Evaluation" };
		var result = await _resolver.ResolveAsync(input, default);
		result.EligibleRecipientCount.ShouldBe(0);
		result.BlockingIssues.ShouldContain("ReviewerNotConfigured");
	}

	[Fact]
	public async Task Preview_captures_association_revision_and_does_not_return_contact_strings()
	{
		var student = AddStudent("mother");
		var before = (await ResolveAsync()).Recipients.Single(x => x.IsEligible);
		before.AssociationRevision.ShouldNotBeNullOrWhiteSpace();
		student.Family!.UpdateMotherDetails(Profile("Mother", "new@example.invalid"));
		var after = (await ResolveAsync()).Recipients.Single(x => x.IsEligible);
		after.AssociationRevision.ShouldNotBe(before.AssociationRevision);
		System.Text.Json.JsonSerializer.Serialize(after).ShouldNotContain("example.invalid");
	}

	[Fact]
	public async Task A_recipient_with_one_usable_channel_is_not_counted_as_fully_excluded()
	{
		AddStudent("mother");
		var result = await ResolveAsync(channels: [CommunicationChannelType.Email, CommunicationChannelType.InApp]);
		result.EligibleRecipientCount.ShouldBe(1);
		result.ExcludedRecipientCount.ShouldBe(0);
		result.Recipients.ShouldContain(x => x.Channel == CommunicationChannelType.InApp && !x.IsEligible);
	}

	[Fact]
	public async Task Assigned_staff_receives_an_opaque_revision_that_changes_with_the_destination()
	{
		var userId = Guid.NewGuid();
		var employeeId = Guid.NewGuid();
		_staff.Add(new(userId, employeeId, true, "teacher@example.invalid", null));
		_source = _source with { EligibleRecipients = [new("AssignedTeacher", UserId: userId, EmployeeId: employeeId)] };
		var before = (await ResolveAsync()).Recipients.Single();
		before.IsEligible.ShouldBeTrue();
		before.AssociationRevision.ShouldNotBeNullOrWhiteSpace();
		_staff[0] = _staff[0] with { Email = "changed@example.invalid" };
		var after = (await ResolveAsync()).Recipients.Single();
		after.AssociationRevision.ShouldNotBe(before.AssociationRevision);
	}

	[Fact]
	public async Task Invalid_guardian_mode_and_unsupported_channels_are_rejected()
	{
		AddStudent("mother");
		await Should.ThrowAsync<BusinessException>(() => ResolveAsync(new(NotificationGuardianMode.PrimaryOnly, ["father"])));
		await Should.ThrowAsync<BusinessException>(() => ResolveAsync(channels: [(CommunicationChannelType)99]));
	}

	private Task<NotificationAudiencePreviewDto> ResolveAsync(AcademicNotificationRecipientPolicy? policy = null,
		IReadOnlyList<CommunicationChannelType>? channels = null) => _resolver.ResolveAsync(Input() with {
		Policy = policy ?? new(NotificationGuardianMode.PrimaryOnly, []), Channels = channels ?? [CommunicationChannelType.Email]
	}, default);

	private AcademicNotificationAudienceInput Input() => new(EventKey, "Academic.Homework", Guid.NewGuid(),
		new(NotificationGuardianMode.PrimaryOnly, []), [CommunicationChannelType.Email], []);

	private AcademicNotificationStudent AddStudent(string? primary, Guid? tenant = null)
	{
		var student = new Student(Guid.NewGuid(), tenant ?? _tenant, "Child", null, "Family", Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status: StudentLifeCycleStatusEnum.Enrolled);
		student.UpdateFamilyInfo(Profile("Father", "father@example.invalid"), Profile("Mother", "mother@example.invalid"),
			Profile("Guardian", "guardian@example.invalid"), []);
		if (primary != null) StudentFamilyContactSelection.Select(student, primary);
		var row = new AcademicNotificationStudent(student, student.FamilyDetail, false, new List<AcademicNotificationAccount>());
		_students.Add(row);
		_source = _source with { EligibleRecipients = _source.EligibleRecipients.Append(new("Student", student.Id)).ToArray() };
		return row;
	}

	private AcademicNotificationAccount AddAccount(AcademicNotificationStudent student, string type, string relationship)
	{
		var user = new IdentityUser(Guid.NewGuid(), "test-" + Guid.NewGuid().ToString("N"), "mother@example.invalid", _tenant);
		var link = new StudentPortalAccountLink(Guid.NewGuid(), _tenant, student.Student.Id, user.Id, type, relationship, _actor);
		var account = new AcademicNotificationAccount(link, user);
		((List<AcademicNotificationAccount>)student.Accounts).Add(account);
		return account;
	}

	private static ParentGuardianDetails Profile(string name, string? email) => new(name, null, null,
		email, null, null, null, null, null, null, null, null, null);
}
