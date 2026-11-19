using System;
using System.Text.Json.Nodes;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentOnboardingIdentifierRules_Tests
{
	private const string IdentifierLocked = "Students:Onboarding:IdentifierLocked";
	private static readonly DateTime Now = new(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Locked_GR_rejects_canonical_changes_and_preserves_both_representations()
	{
		var student = CreateStudent();
		student.AssignGrNumber("GR-1001", 1001);
		student.SetIdentifierLocked("gr", true);
		Should.Throw<BusinessException>(() => student.AssignGrNumber("GR-1002", 1002)).Code.ShouldBe(IdentifierLocked);
		student.GrNumber.ShouldBe("GR-1001");
		student.GrNo.ShouldBe(1001);
	}

	[Theory]
	[InlineData(1002)]
	[InlineData(null)]
	public void Locked_GR_rejects_legacy_changes_even_if_canonical_value_is_unchanged(int? replacement)
	{
		var student = CreateStudent();
		student.AssignGrNumber("GR-1001", 1001);
		student.SetIdentifierLocked("gr", true);
		Should.Throw<BusinessException>(() => student.AssignGrNumber("GR-1001", replacement)).Code.ShouldBe(IdentifierLocked);
		student.GrNo.ShouldBe(1001);
	}

	[Fact]
	public void Canonical_only_locked_GR_cannot_be_changed_through_legacy_assignment()
	{
		var student = CreateStudent();
		student.AssignGrNumber("GR-1001");
		student.SetIdentifierLocked("gr", true);
		Should.Throw<BusinessException>(() => student.AssignGrNo(1002)).Code.ShouldBe(IdentifierLocked);
		student.GrNo.ShouldBeNull();
		student.GrNumber.ShouldBe("GR-1001");
	}

	[Fact]
	public void Legacy_only_locked_GR_cannot_be_changed_through_canonical_assignment()
	{
		var student = CreateStudent();
		student.AssignGrNo(1001);
		student.SetIdentifierLocked("gr", true);
		Should.Throw<BusinessException>(() => student.AssignGrNumber("GR-1002", 1002)).Code.ShouldBe(IdentifierLocked);
		student.GrNumber.ShouldBeNull();
		student.GrNo.ShouldBe(1001);
	}

	[Fact]
	public void Identifier_locks_allow_idempotent_retries_and_do_not_lock_the_other_number()
	{
		var student = CreateStudent();
		student.AssignGrNumber("GR-1001", 1001);
		student.AssignRollNo(1);
		student.SetIdentifierLocked("gr", true);
		Should.NotThrow(() => student.AssignGrNumber(" GR-1001 ", 1001));
		Should.NotThrow(() => student.AssignGrNo(1001));
		Should.NotThrow(() => student.AssignRollNo(2));
		student.SetIdentifierLocked("roll", true);
		Should.NotThrow(() => student.AssignRollNo(2));
		Should.Throw<BusinessException>(() => student.AssignRollNo(3)).Code.ShouldBe(IdentifierLocked);
		Should.Throw<BusinessException>(() => student.AssignRollNo(null)).Code.ShouldBe(IdentifierLocked);
		student.RollNo.ShouldBe(2);
	}

	[Fact]
	public void New_students_can_receive_their_first_numbers_when_identifier_locks_are_already_set()
	{
		var student = CreateStudent();
		student.SetIdentifierLocked("gr", true);
		student.SetIdentifierLocked("roll", true);
		Should.NotThrow(() => student.AssignGrNumber("GR-1001", 1001));
		Should.NotThrow(() => student.AssignRollNo(1));
		student.IsIdentifierLocked("gr").ShouldBeTrue();
		student.IsIdentifierLocked("roll").ShouldBeTrue();
		Should.Throw<BusinessException>(() => student.AssignRollNo(2)).Code.ShouldBe(IdentifierLocked);
	}

	[Fact]
	public void Unlocking_identifiers_allows_a_following_correction()
	{
		var student = CreateStudent();
		student.AssignGrNumber("GR-1001", 1001);
		student.AssignRollNo(1);
		student.SetIdentifierLocked("gr", true);
		student.SetIdentifierLocked("roll", true);
		student.SetIdentifierLocked("gr", false);
		student.SetIdentifierLocked("roll", false);
		student.AssignGrNumber("GR-1002", 1002, "Correct the recorded GR number");
		student.AssignRollNo(2);
		student.GrNo.ShouldBe(1002);
		student.RollNo.ShouldBe(2);
	}

	[Fact]
	public void GR_corrections_require_a_reason_after_the_first_assignment()
	{
		var student = CreateStudent();
		Should.NotThrow(() => student.AssignGrNumber("1001", 1001));
		Should.Throw<BusinessException>(() => student.AssignGrNumber("1002", 1002))
			.Code.ShouldBe(StudentOnboardingErrorCodes.WaiverReasonRequired);
		Should.Throw<BusinessException>(() => student.AssignGrNo(1002))
			.Code.ShouldBe(StudentOnboardingErrorCodes.WaiverReasonRequired);
		student.GrNumber.ShouldBe("1001");
		student.GrNo.ShouldBe(1001);
		student.AssignGrNo(1002, "Correct a transcription error");
		student.GrNumber.ShouldBe("1002");
		student.GrNo.ShouldBe(1002);
	}

	[Fact]
	public void Case_can_record_GR_before_Roll_and_preserves_an_existing_Roll_reservation()
	{
		var item = CreateCase();
		var allocation = Guid.NewGuid();
		item.RecordGrIdentifier(allocation, Now);
		item.IdentifierAllocationId.ShouldBe(allocation);
		item.RollNumber.ShouldBeNull();
		item.RecordIdentifier(allocation, 1, Now);
		var updatedAllocation = Guid.NewGuid();
		item.RecordGrIdentifier(updatedAllocation, Now);
		item.IdentifierAllocationId.ShouldBe(updatedAllocation);
		item.RollNumber.ShouldBe(1);
	}

	[Fact]
	public void Case_locks_protect_each_number_but_leave_division_placement_available()
	{
		var item = CreateCase();
		var allocation = Guid.NewGuid();
		item.RecordIdentifier(allocation, 1, Now);
		item.SetIdentifierLocked("gr", true);
		item.SetIdentifierLocked("roll", true);
		Should.NotThrow(() => item.RecordIdentifier(allocation, 1, Now));
		Should.NotThrow(() => item.RecordGrIdentifier(allocation, Now));
		Should.Throw<BusinessException>(() => item.RecordGrIdentifier(Guid.NewGuid(), Now)).Code.ShouldBe(IdentifierLocked);
		Should.Throw<BusinessException>(() => item.RecordIdentifier(allocation, 2, Now)).Code.ShouldBe(IdentifierLocked);
		Should.Throw<BusinessException>(() => item.RecordIdentifier(null, 1, Now)).Code.ShouldBe(IdentifierLocked);
		Should.Throw<BusinessException>(() => item.ClearRollNumberForPlacementChange(Now)).Code.ShouldBe(IdentifierLocked);
		var division = Guid.NewGuid();
		Should.NotThrow(() => item.RecordPlacement(division, Guid.NewGuid(), Now));
		item.DivisionId.ShouldBe(division);
		item.SetIdentifierLocked("roll", false);
		item.ClearRollNumberForPlacementChange(Now);
		item.RollNumber.ShouldBeNull();
		item.IdentifierAllocationId.ShouldBe(allocation);
	}

	[Fact]
	public void Offering_link_selects_the_readiness_rule_independently_of_admission_route()
	{
		var item = CreateCase();
		item.SetAdmissionRoute("Transfer student");
		var root = EmptyConfiguration();
		root["onboardingProfiles"] = new JsonArray(Profile("offering-rule", item.OfferingId));
		var configuration = new OnboardingRuleConfiguration(root.ToJsonString());
		configuration.ResolveProfile(item, "Returning student")!.Value.GetProperty("id").GetString().ShouldBe("offering-rule");
	}

	[Fact]
	public void Duplicate_Offering_links_are_rejected_instead_of_arbitrarily_selecting_a_rule()
	{
		var item = CreateCase();
		var root = EmptyConfiguration();
		root["onboardingProfiles"] = new JsonArray(Profile("first", item.OfferingId), Profile("second", item.OfferingId));
		var configuration = new OnboardingRuleConfiguration(root.ToJsonString());
		Should.Throw<BusinessException>(() => configuration.ResolveProfile(item)).Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
	}

	[Theory]
	[InlineData("grNumber")]
	[InlineData("rollNumber")]
	public void Runtime_resolves_only_published_numbering_rules(string section)
	{
		var item = CreateCase();
		var root = EmptyConfiguration();
		root[section]!["configurations"] = new JsonArray(Rule("draft", "draft", false), Rule("old", "superseded", false), Rule("live", "published", true));
		var configuration = new OnboardingRuleConfiguration(root.ToJsonString());
		configuration.ResolveRule(section, item, Now)!.Value.GetProperty("id").GetString().ShouldBe("live");
		root[section]!["configurations"] = new JsonArray(Rule("draft", "draft", false));
		new OnboardingRuleConfiguration(root.ToJsonString()).ResolveRule(section, item, Now).ShouldBeNull();
	}

	[Theory]
	[InlineData("grNumber")]
	[InlineData("rollNumber")]
	public void Published_rules_are_immutable_and_history_cannot_be_removed(string section)
	{
		var root = EmptyConfiguration();
		root[section]!["configurations"] = new JsonArray(Rule("live", "published", true));
		var settings = CreateSettings(root);
		var changed = root.DeepClone().AsObject();
		changed[section]!["configurations"]![0]!["startingNumber"] = 50;
		Should.Throw<BusinessException>(() => settings.Update(StudentOnboardingSettingsConsts.CurrentSchemaVersion, changed.ToJsonString())).Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		changed[section]!["configurations"] = new JsonArray();
		Should.Throw<BusinessException>(() => settings.Update(StudentOnboardingSettingsConsts.CurrentSchemaVersion, changed.ToJsonString())).Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		settings.ConfigurationJson.ShouldBe(root.ToJsonString());
	}

	[Theory]
	[InlineData("grNumber")]
	[InlineData("rollNumber")]
	public void A_saved_draft_preserves_the_live_rule_and_publish_atomically_supersedes_it(string section)
	{
		var root = EmptyConfiguration();
		root[section]!["configurations"] = new JsonArray(Rule("live", "published", true));
		var settings = CreateSettings(root);
		var draft = Rule("new", "draft", false);
		draft["previousVersionId"] = "live";
		draft["version"] = 2;
		root[section]!["configurations"]!.AsArray().Add(draft);
		settings.Update(StudentOnboardingSettingsConsts.CurrentSchemaVersion, root.ToJsonString());
		new OnboardingRuleConfiguration(settings.ConfigurationJson).ResolveRule(section, CreateCase(), Now)!.Value.GetProperty("id").GetString().ShouldBe("live");
		root[section]!["configurations"]![0]!["status"] = "superseded";
		root[section]!["configurations"]![0]!["isActive"] = false;
		Should.Throw<BusinessException>(() => settings.Update(StudentOnboardingSettingsConsts.CurrentSchemaVersion, root.ToJsonString())).Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		draft["status"] = "published";
		draft["isActive"] = true;
		settings.Update(StudentOnboardingSettingsConsts.CurrentSchemaVersion, root.ToJsonString());
		new OnboardingRuleConfiguration(settings.ConfigurationJson).ResolveRule(section, CreateCase(), Now)!.Value.GetProperty("id").GetString().ShouldBe("new");
	}

	[Theory]
	[InlineData("grNumber")]
	[InlineData("rollNumber")]
	public void A_new_rule_must_be_saved_as_draft_before_it_can_be_published(string section)
	{
		var root = EmptyConfiguration();
		var settings = CreateSettings(root);
		root[section]!["configurations"] = new JsonArray(Rule("new", "published", true));
		Should.Throw<BusinessException>(() => settings.Update(StudentOnboardingSettingsConsts.CurrentSchemaVersion, root.ToJsonString()))
			.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
	}

	[Theory]
	[InlineData("grNumber")]
	[InlineData("rollNumber")]
	public void Superseded_numbering_history_cannot_be_rewritten(string section)
	{
		var root = EmptyConfiguration();
		var current = Rule("current", "published", true);
		current["previousVersionId"] = "old";
		current["version"] = 2;
		root[section]!["configurations"] = new JsonArray(Rule("old", "superseded", false), current);
		var settings = CreateSettings(root);
		root[section]!["configurations"]![0]!["startingNumber"] = 100;
		Should.Throw<BusinessException>(() => settings.Update(StudentOnboardingSettingsConsts.CurrentSchemaVersion, root.ToJsonString()))
			.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
	}

	[Theory]
	[InlineData("grNumber")]
	[InlineData("rollNumber")]
	public void A_stale_sibling_draft_cannot_publish_after_its_predecessor_has_been_superseded(string section)
	{
		var root = EmptyConfiguration();
		var first = Rule("first-draft", "draft", false);
		var second = Rule("second-draft", "draft", false);
		foreach (var rule in new[] { first, second })
		{
			rule["previousVersionId"] = "original";
			rule["version"] = 2;
		}
		root[section]!["configurations"] = new JsonArray(Rule("original", "published", true), first, second);
		var settings = CreateSettings(root);
		root[section]!["configurations"]![0]!["status"] = "superseded";
		root[section]!["configurations"]![0]!["isActive"] = false;
		first["status"] = "published";
		first["isActive"] = true;
		settings.Update(StudentOnboardingSettingsConsts.CurrentSchemaVersion, root.ToJsonString());
		second["status"] = "published";
		second["isActive"] = true;
		Should.Throw<BusinessException>(() => settings.Update(StudentOnboardingSettingsConsts.CurrentSchemaVersion, root.ToJsonString()))
			.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		new OnboardingRuleConfiguration(settings.ConfigurationJson).ResolveRule(section, CreateCase(), Now)!.Value.GetProperty("id").GetString().ShouldBe("first-draft");
	}

	[Theory]
	[InlineData("grNumber", false)]
	[InlineData("rollNumber", false)]
	[InlineData("grNumber", true)]
	[InlineData("rollNumber", true)]
	public void Published_history_cannot_be_removed_by_omitting_its_container(string section, bool omitConfigurations)
	{
		var root = EmptyConfiguration();
		root[section]!["configurations"] = new JsonArray(Rule("published", "published", true));
		var settings = CreateSettings(root);
		if (omitConfigurations) root[section]!.AsObject().Remove("configurations");
		else root.Remove(section);
		Should.Throw<BusinessException>(() => settings.Update(StudentOnboardingSettingsConsts.CurrentSchemaVersion, root.ToJsonString()))
			.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
	}

	[Theory]
	[InlineData("grNumber")]
	[InlineData("rollNumber")]
	public void Published_rule_cannot_be_downgraded_to_an_inactive_legacy_rule(string section)
	{
		var root = EmptyConfiguration();
		root[section]!["configurations"] = new JsonArray(Rule("published", "published", true));
		var settings = CreateSettings(root);
		root[section]!["configurations"]![0]!.AsObject().Remove("status");
		root[section]!["configurations"]![0]!["isActive"] = false;
		Should.Throw<BusinessException>(() => settings.Update(StudentOnboardingSettingsConsts.CurrentSchemaVersion, root.ToJsonString()))
			.Code.ShouldBe(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		new OnboardingRuleConfiguration(settings.ConfigurationJson).ResolveRule(section, CreateCase(), Now)!.Value.GetProperty("id").GetString().ShouldBe("published");
	}

	private static Student CreateStudent() => new(Guid.NewGuid(), null, "Test", null, "Student",
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

	private static StudentOnboardingCase CreateCase() => new(Guid.NewGuid(), null, Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), $"onboarding:{Guid.NewGuid():N}", Now, Now.AddDays(2));

	private static StudentOnboardingSettings CreateSettings(JsonObject root)
	{
		var working = root.DeepClone().AsObject();
		foreach (var section in new[] { "grNumber", "rollNumber" })
			working[section]!["configurations"] = new JsonArray();
		var settings = new StudentOnboardingSettings(Guid.NewGuid(), null, null,
			StudentOnboardingSettingsConsts.CurrentSchemaVersion, working.ToJsonString());
		foreach (var section in new[] { "grNumber", "rollNumber" })
		foreach (var original in root[section]!["configurations"]!.AsArray())
		{
			var rule = original!.DeepClone().AsObject();
			var targetStatus = rule["status"]!.GetValue<string>();
			rule["status"] = "draft";
			rule["isActive"] = false;
			working[section]!["configurations"]!.AsArray().Add(rule);
			settings.Update(StudentOnboardingSettingsConsts.CurrentSchemaVersion, working.ToJsonString());
			if (targetStatus == "draft") continue;
			rule["status"] = "published";
			rule["isActive"] = true;
			var previousId = rule["previousVersionId"]?.GetValue<string>();
			if (previousId != null)
				foreach (var candidate in working[section]!["configurations"]!.AsArray())
					if (candidate!["id"]!.GetValue<string>() == previousId)
					{
						candidate["status"] = "superseded";
						candidate["isActive"] = false;
					}
			settings.Update(StudentOnboardingSettingsConsts.CurrentSchemaVersion, working.ToJsonString());
		}
		return settings;
	}

	private static JsonObject EmptyConfiguration() => JsonNode.Parse("""
		{"readinessAccess":{},"onboardingProfiles":[],"grNumber":{"configurations":[]},
		"rollNumber":{"configurations":[]},"divisionChange":{"configurations":[]}}
		""")!.AsObject();

	private static JsonObject Rule(string id, string status, bool active) => new()
	{
		["id"] = id, ["status"] = status, ["isActive"] = active, ["version"] = 1,
		["startingNumber"] = 1, ["formatPattern"] = "{SEQUENCE}",
		["boardMode"] = "all", ["mediumMode"] = "all", ["stageMode"] = "all",
		["classMode"] = "all", ["divisionMode"] = "all"
	};

	private static JsonObject Profile(string id, Guid offeringId) => new()
	{
		["id"] = id, ["offeringIds"] = new JsonArray(offeringId.ToString()),
		["admissionRoutes"] = new JsonArray("Fresh admission"),
		["applicability"] = new JsonObject(), ["rules"] = new JsonObject()
	};
}
