using System;
using System.Collections.Generic;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentRecordsPolicy_Tests
{
	[Fact]
	public void Aggregate_rejects_invalid_policy_even_without_application_service()
	{
		Should.Throw<BusinessException>(() => new StudentRecordsConfiguration(Guid.NewGuid(), Guid.NewGuid(), null, "[]"));
		var policy = StudentRecordsPolicy.Starter(); policy.Changes.Remove("LegacyOverride");
		Should.Throw<BusinessException>(() => new StudentRecordsConfiguration(Guid.NewGuid(), Guid.NewGuid(), null, policy.ToJson()));
	}

	[Fact]
	public void Starter_is_valid_and_requires_governance_for_high_risk_actions()
	{
		var policy = StudentRecordsPolicy.Starter();
		StudentRecordsPolicyValidator.Validate(policy);
		foreach (var action in new[] { "Identity", "Placement", "Guardian", "Medical", "Bank", "Status", "Correction", "LegacyOverride" })
			policy.Changes[action].RequiresApproval(false, false, false).ShouldBeTrue();
		policy.Changes["Contact"].RequiresApproval(false, false, false).ShouldBeFalse();
		policy.Provisioning.AllowPlacementEdits.ShouldBeFalse();
	}
	[Fact]
	public void Published_snapshot_does_not_change_with_later_draft()
	{
		var tenant = Guid.NewGuid();
		var draft = StudentRecordsPolicy.Starter();
		var configuration = new StudentRecordsConfiguration(Guid.NewGuid(), tenant, null, draft.ToJson());
		var version = new StudentRecordsPolicyVersion(Guid.NewGuid(), tenant, configuration.Id, null, 1, configuration.DraftPolicyJson);
		configuration.Publish(version.Id);
		draft.Provisioning.Mode = "Full";
		configuration.SaveDraft(draft.ToJson());
		StudentRecordsPolicy.FromJson(version.PolicyJson).Provisioning.Mode.ShouldBe("Summary");
		configuration.PublishedVersionId.ShouldBe(version.Id);
		configuration.PublishedVersionNumber.ShouldBe(1);
	}
	[Fact]
	public void Conditional_approval_matches_all_configured_request_attributes()
	{
		var rule = new StudentRecordsChangeRule { Condition = new() { IsPastDated = true, IsFormerStudent = false } };
		rule.RequiresApproval(true, false, false).ShouldBeTrue();
		rule.RequiresApproval(false, false, false).ShouldBeFalse();
		rule.RequiresApproval(true, false, true).ShouldBeFalse();
	}
	[Fact]
	public void Sensitive_masks_use_explicit_allowed_roles_and_hide_from_unlisted_roles()
	{
		var policy = StudentRecordsPolicy.Starter();
		policy.Access.Masks.Add(new() { FieldGroup = "Medical", AllowedRoleNames = ["Nurse"] });
		policy.IsMasked("Medical", ["Clerk"]).ShouldBeTrue();
		policy.IsMasked("Medical", ["nurse"]).ShouldBeFalse();
		policy.IsMasked("Identity", []).ShouldBeFalse();
	}
	[Theory]
	[InlineData(0)]
	[InlineData(4)]
	public void Approval_requires_one_to_three_ordered_stages(int count)
	{
		var policy = StudentRecordsPolicy.Starter();
		policy.Changes["Identity"].Stages = [];
		for (var index = 0; index < count; index++) policy.Changes["Identity"].Stages.Add(new());
		Should.Throw<BusinessException>(() => StudentRecordsPolicyValidator.Validate(policy)).Code.ShouldBe("Students:Records:InvalidPolicy");
	}
	[Fact]
	public void Legacy_override_cannot_be_direct_or_conditional()
	{
		var policy = StudentRecordsPolicy.Starter();
		policy.Changes["LegacyOverride"].Mode = "Direct";
		Should.Throw<BusinessException>(() => StudentRecordsPolicyValidator.Validate(policy));
		policy.Changes["LegacyOverride"].Mode = "Approval";
		policy.Changes["LegacyOverride"].Condition = new() { IsPastDated = true };
		Should.Throw<BusinessException>(() => StudentRecordsPolicyValidator.Validate(policy));
	}
	[Fact]
	public void Invalid_routes_groups_or_provisioning_placement_are_rejected()
	{
		var policy = StudentRecordsPolicy.Starter();
		policy.Transitions.DefaultReturnRoute = "ResumeStage";
		Should.Throw<BusinessException>(() => StudentRecordsPolicyValidator.Validate(policy));
		policy.Transitions.DefaultReturnRoute = "ReuseProfile";
		policy.Provisioning.AllowPlacementEdits = true;
		Should.Throw<BusinessException>(() => StudentRecordsPolicyValidator.Validate(policy));
		policy.Provisioning.Mode = "Full";
		policy.Access.Masks = [new() { FieldGroup = "Unsupported" }];
		Should.Throw<BusinessException>(() => StudentRecordsPolicyValidator.Validate(policy));
	}
	[Fact]
	public void Policy_round_trip_preserves_explicit_school_choices()
	{
		var policy = StudentRecordsPolicy.Starter();
		policy.Transitions.BatchExceptionMode = "HoldAll";
		policy.Transitions.OutcomeChecks["Graduate"].RequireAcademicResult = true;
		var reloaded = StudentRecordsPolicy.FromJson(policy.ToJson());
		reloaded.Transitions.BatchExceptionMode.ShouldBe("HoldAll");
		reloaded.Transitions.OutcomeChecks["Graduate"].RequireAcademicResult.ShouldBeTrue();
	}
}
