using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public class AcademicNotificationRuntimeRecipientResolver_Tests
{
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly IAcademicNotificationRecipientProjection _projection = Substitute.For<IAcademicNotificationRecipientProjection>();
	private readonly NotificationSourceScopeDto _scope = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
	private readonly List<AcademicNotificationStudent> _children = [];
	private readonly AcademicNotificationRuntimeRecipientResolver _resolver;
	private readonly AcademicNotificationCatalogue _catalogue = new([]);

	public AcademicNotificationRuntimeRecipientResolver_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		_projection.LoadAsync(_tenantId, Arg.Any<IReadOnlyList<NotificationRecipientRefDto>>(), Arg.Any<CancellationToken>())
			.Returns(_ => new AcademicNotificationRecipientData(_children, []));
		_resolver = new(_tenant, _projection);
	}

	[Fact]
	public async Task Default_family_recipient_uses_the_current_saved_primary_and_preserves_each_child()
	{
		var first = AddChild("mother");
		var second = AddChild("mother");
		var source = Source([first, second]);
		var results = await _resolver.ResolveAsync(source, default);
		var email = results.Where(x => x.Channel == CommunicationChannelType.Email && x.IsEligible).ToArray();
		email.Select(x => x.SourceRecipient).ShouldBe(new[] { first, second }, ignoreOrder: true);
		email.ShouldAllBe(x => x.Recipient.FamilyRelationship == "mother" &&
			x.Destination == "mother@example.invalid" && x.RecipientName == "Mother" && x.StudentName == "Student Example");
		results.ShouldNotContain(x => x.Recipient.Kind == "StudentAccount");
		results.ShouldAllBe(x => x.IsEligible || x.Destination == null);
		email[0].ToString()!.ShouldNotContain("mother@example.invalid");
	}

	[Fact]
	public async Task A_changed_primary_replaces_the_runtime_candidate_and_invalidates_the_old_association()
	{
		var child = AddChild("mother");
		var source = Source([child]);
		var before = (await _resolver.ResolveAsync(source, default)).Single(x => x.Channel == CommunicationChannelType.Email);
		StudentFamilyContactSelection.Select(_children[0].Student, "father");
		var after = (await _resolver.ResolveAsync(source, default)).Single(x => x.Channel == CommunicationChannelType.Email);
		before.Recipient.FamilyRelationship.ShouldBe("mother");
		after.Recipient.FamilyRelationship.ShouldBe("father");
		after.AssociationRevision.ShouldNotBe(before.AssociationRevision);
		after.Destination.ShouldBe("father@example.invalid");
	}

	[Fact]
	public async Task Revoking_a_guardian_link_removes_account_delivery_and_changes_the_association_revision()
	{
		var child = AddChild("mother");
		var guardian = Link(child, "guardian", "mother");
		var source = Source([child]);
		var before = (await _resolver.ResolveAsync(source, default)).Single(x => x.Channel == CommunicationChannelType.InApp);
		before.Recipient.UserId.ShouldBe(guardian.Id);
		before.IsEligible.ShouldBeTrue();
		_children[0] = _children[0] with { Accounts = [] };
		var after = (await _resolver.ResolveAsync(source, default)).Single(x => x.Channel == CommunicationChannelType.InApp);
		after.Recipient.UserId.ShouldBeNull();
		after.IsEligible.ShouldBeFalse();
		after.ExclusionCode.ShouldBe("VerifiedAccountRequired");
		after.AssociationRevision.ShouldNotBe(before.AssociationRevision);
	}

	[Fact]
	public async Task Only_explicitly_selected_guardians_and_verified_student_accounts_expand_the_audience()
	{
		var child = AddChild("mother");
		var studentUser = Link(child, "student", "self");
		var source = Source([child], includeStudent: true, additional: ["father"]);
		var results = await _resolver.ResolveAsync(source, default);
		results.ShouldContain(x => x.Recipient.FamilyRelationship == "father" && x.Destination == "father@example.invalid");
		results.ShouldNotContain(x => x.Recipient.FamilyRelationship == "guardian");
		results.ShouldContain(x => x.Recipient.UserId == studentUser.Id && x.IsEligible &&
			x.Channel == CommunicationChannelType.InApp);
		results.ShouldContain(x => x.Recipient.UserId == studentUser.Id && !x.IsEligible &&
			x.Channel == CommunicationChannelType.Email && x.Destination == null);
	}

	[Fact]
	public async Task Pending_guardian_review_does_not_offer_a_destination_even_with_a_retained_account()
	{
		var child = AddChild("mother");
		Link(child, "guardian", "mother");
		_children[0] = _children[0] with { GuardianReviewPending = true };
		var results = await _resolver.ResolveAsync(Source([child]), default);
		results.Count.ShouldBe(2);
		results.ShouldAllBe(x => !x.IsEligible && x.Destination == null &&
			x.ExclusionCode == "GuardianAccessReviewPending");
	}

	[Theory]
	[InlineData("removed", NotificationErrorCodes.RecipientIneligible)]
	[InlineData("wrongDivision", "StudentScopeChanged")]
	[InlineData("departed", "StudentNotEnrolled")]
	public async Task Current_scope_and_enrollment_gate_contact_resolution(string scenario, string reason)
	{
		var child = AddChild("mother", wrongDivision: scenario == "wrongDivision");
		if (scenario == "departed") _children[0].Student.ChangeStatus(StudentLifeCycleStatusEnum.Left);
		var source = Source([child]);
		if (scenario == "removed") source = source with { CurrentRecipients = [] };
		var results = await _resolver.ResolveAsync(source, default);
		results.Count.ShouldBe(2);
		results.ShouldAllBe(x => !x.IsEligible && x.Destination == null && x.ExclusionCode == reason);
		if (scenario == "removed") await _projection.DidNotReceive().LoadAsync(_tenantId,
			Arg.Any<IReadOnlyList<NotificationRecipientRefDto>>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Foreign_tenant_cannot_produce_runtime_addresses()
	{
		var child = AddChild("mother");
		await Should.ThrowAsync<AbpAuthorizationException>(() => _resolver.ResolveAsync(Source([child]) with { TenantId = Guid.NewGuid() }, default));
		await _projection.DidNotReceive().LoadAsync(_tenantId, Arg.Any<IReadOnlyList<NotificationRecipientRefDto>>(), Arg.Any<CancellationToken>());
	}

	private NotificationCurrentSource Source(NotificationRecipientRefDto[] captured, bool includeStudent = false, string[]? additional = null)
	{
		var config = new NotificationRuleRuntimeConfiguration(1, additional == null ? NotificationGuardianMode.PrimaryOnly : NotificationGuardianMode.PrimaryAndSelected,
			additional ?? [], includeStudent, [], [CommunicationChannelType.Email, CommunicationChannelType.InApp], [Guid.NewGuid()],
			NotificationRuleTimingConfiguration.Default, "en");
		var data = new NotificationSourceSnapshot(true, null, "v1", "/academic/homework", new Dictionary<string, NotificationVariableValueDto>(), captured);
		return new(_tenantId, _catalogue.Get("Academic.Homework.Published"), new(1, _scope, config), captured, captured, data, null, null);
	}

	private NotificationRecipientRefDto AddChild(string primary, bool wrongDivision = false)
	{
		var student = new Student(Guid.NewGuid(), _tenantId, "Student", null, "Example", _scope.AcademicYearId!.Value,
			_scope.BoardId!.Value, _scope.MediumId!.Value, Guid.NewGuid(), _scope.ClassId,
			wrongDivision ? Guid.NewGuid() : _scope.DivisionId, status: StudentLifeCycleStatusEnum.Enrolled);
		student.UpdateFamilyInfo(Profile("Father", "father@example.invalid"), Profile("Mother", "mother@example.invalid"),
			Profile("Guardian", "guardian@example.invalid"), []);
		StudentFamilyContactSelection.Select(student, primary);
		_children.Add(new(student, student.FamilyDetail, false, new List<AcademicNotificationAccount>()));
		return new("Student", student.Id);
	}

	private IdentityUser Link(NotificationRecipientRefDto child, string accountType, string relationship)
	{
		var studentId = child.StudentId!.Value;
		var user = new IdentityUser(Guid.NewGuid(), "linked", "student@example.invalid", _tenantId);
		var link = new StudentPortalAccountLink(Guid.NewGuid(), _tenantId, studentId, user.Id, accountType, relationship, Guid.NewGuid());
		((List<AcademicNotificationAccount>)_children.Single(x => x.Student.Id == studentId).Accounts).Add(new(link, user));
		return user;
	}

	private static ParentGuardianDetails Profile(string name, string email) => new(name, null, null, email, null, null,
		null, null, null, null, null, null, null);
}
