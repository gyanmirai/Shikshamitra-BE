using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionEligibilityPolicyAndRules_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("{}")]
	[InlineData("{\"eligibility\":{\"enabled\":false,\"version\":2}}")]
	public void Eligibility_missing_or_disabled_policy_preserves_legacy(string? json) => AdmissionEligibilityPolicy.Read(json).ShouldBeNull();
	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public void Eligibility_future_enabled_version_cannot_be_used(int version) => Should.Throw<ArgumentException>(() => (Policy() with { Version = version }).Validate());
	[Fact]
	public void Eligibility_policy_requires_unique_bounded_criteria_and_valid_age_limits()
	{
		Should.Throw<ArgumentException>(() => (Policy() with { Criteria = [] }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { Criteria = [Age(), Age()] }).Validate());
		Should.Throw<ArgumentException>(() => (Age() with { MinAgeMonths = -1 }).Validate());
		Should.Throw<ArgumentException>(() => (Age() with { MinAgeMonths = 80 }).Validate());
		Should.Throw<ArgumentException>(() => (Age() with { CutoffDate = "2026-02-30" }).Validate());
		Should.Throw<ArgumentException>(() => (Age() with { MinAgeMonths = null, MaxAgeMonths = null }).Validate());
	}
	[Fact]
	public void Eligibility_reference_kinds_cannot_mix_sources_or_use_waiver_scores()
	{
		Should.Throw<ArgumentException>(() => new AdmissionEligibilityCriterion { Code = "MARKS", Name = "Marks", Kind = "application", FormDefinitionId = Guid.NewGuid(),
			ConditionJson = "{\"operator\":\"Equals\",\"field\":\"assessment.outcome\",\"value\":\"passed\"}" }.Validate());
		Should.Throw<ArgumentException>(() => new AdmissionEligibilityCriterion { Code = "EXAM", Name = "Exam", Kind = "assessment", SourceStageCode = "EXAM", RequiredOutcome = "waived", MinimumPercentage = 40 }.Validate());
	}
	[Theory]
	[InlineData("2020-02-29", "2021-02-27", 12, null, "failed")]
	[InlineData("2020-02-29", "2021-02-28", 12, null, "passed")]
	[InlineData("2020-01-31", "2020-02-29", 1, 1, "passed")]
	[InlineData("2020-01-31", "2020-03-30", 1, 1, "passed")]
	[InlineData("2020-01-31", "2020-03-31", 1, 1, "failed")]
	[InlineData(null, "2026-04-01", 60, 72, "unknown")]
	[InlineData("2027-01-01", "2026-04-01", 60, 72, "unknown")]
	public void Eligibility_age_uses_calendar_boundaries(string? birth, string cutoff, int? min, int? max, string expected) =>
		AdmissionEligibilityRules.AgeOutcome(birth == null ? null : DateOnly.Parse(birth), DateOnly.Parse(cutoff), min, max).ShouldBe(expected);
	[Theory]
	[InlineData("2020-02-29", "2021-02-27", 11)]
	[InlineData("2020-02-29", "2021-02-28", 12)]
	[InlineData("2020-01-31", "2020-03-30", 1)]
	[InlineData("2020-01-31", "2020-03-31", 2)]
	[InlineData("2020-11-01", "2026-04-01", 65)]
	public void Eligibility_displayed_age_matches_calendar_thresholds(string birth, string cutoff, int months)
	{
		var date = DateOnly.Parse(birth); var at = DateOnly.Parse(cutoff);
		AdmissionEligibilityRules.CompletedAgeMonths(date, at).ShouldBe(months);
		AdmissionEligibilityRules.AgeOutcome(date, at, months, months).ShouldBe("passed");
		AdmissionEligibilityRules.AgeOutcome(date, at, months + 1, null).ShouldBe("failed");
	}
	[Fact]
	public void Eligibility_unknown_and_saved_blank_are_distinct_and_groups_are_three_valued()
	{
		var leaf = "{\"operator\":\"IsEmpty\",\"field\":\"application.name\"}";
		AdmissionEligibilityRules.ConditionOutcome(leaf, new Dictionary<string, string>()).ShouldBe("unknown");
		AdmissionEligibilityRules.ConditionOutcome(leaf, new Dictionary<string, string> { ["application.name"] = "" }).ShouldBe("passed");
		var missing = "{\"operator\":\"Equals\",\"field\":\"application.other\",\"value\":\"A\"}";
		AdmissionEligibilityRules.ConditionOutcome("{\"operator\":\"ANY\",\"conditions\":[" + leaf + "," + missing + "]}", new Dictionary<string, string> { ["application.name"] = "" }).ShouldBe("passed");
		AdmissionEligibilityRules.ConditionOutcome("{\"operator\":\"ALL\",\"conditions\":[" + leaf + "," + missing + "]}", new Dictionary<string, string> { ["application.name"] = "" }).ShouldBe("unknown");
		// A known failing branch cannot turn missing mandatory information into an exception-eligible result.
		AdmissionEligibilityRules.ConditionOutcome("{\"operator\":\"ALL\",\"conditions\":[" + leaf + "," + missing + "]}", new Dictionary<string, string> { ["application.name"] = "Provided" }).ShouldBe("unknown");
	}
	[Theory]
	[InlineData(AdmissionFormFieldType.Number, "Contains", "1")]
	[InlineData(AdmissionFormFieldType.Checkbox, "GreaterThan", "1")]
	[InlineData(AdmissionFormFieldType.Date, "Equals", "2020-01-01")]
	[InlineData(AdmissionFormFieldType.Number, "Equals", "not numeric")]
	public void Eligibility_form_field_schema_rejects_incompatible_operators_and_values(AdmissionFormFieldType type, string operation, string value) =>
		Should.Throw<ArgumentException>(() => AdmissionEligibilityConditionSchema.Validate(JsonSerializer.Serialize(new { @operator = operation, field = "application.answer", value }),
			new Dictionary<string, AdmissionFormFieldType> { ["application.answer"] = type }));
	[Fact]
	public void Eligibility_reserved_application_status_cannot_be_an_answer_source() => Should.Throw<ArgumentException>(() =>
		AdmissionEligibilityConditionSchema.Validate("{\"operator\":\"Equals\",\"field\":\"application.status\",\"value\":\"Approved\"}",
			new Dictionary<string, AdmissionFormFieldType> { ["application.status"] = AdmissionFormFieldType.Text }));
	[Theory]
	[InlineData("\"80, 90\"")]
	[InlineData("\"80,90\"")]
	[InlineData("[80,90]")]
	public void Eligibility_numeric_membership_schema_and_runtime_use_the_same_values(string values)
	{
		var types = new Dictionary<string, AdmissionFormFieldType> { ["application.answer"] = AdmissionFormFieldType.Number };
		foreach (var operation in new[] { "In", "NotIn" })
		{
			var json = "{\"operator\":\"" + operation + "\",\"field\":\"application.answer\",\"value\":" + values + "}";
			AdmissionEligibilityConditionSchema.Validate(json, types);
			foreach (var member in new[] { "80", "90", "80.0" })
				AdmissionEligibilityRules.ConditionOutcome(json, new Dictionary<string, string> { ["application.answer"] = member }, types).ShouldBe(operation == "In" ? "passed" : "failed");
			AdmissionEligibilityRules.ConditionOutcome(json, new Dictionary<string, string> { ["application.answer"] = "8090" }, types).ShouldBe(operation == "In" ? "failed" : "passed");
		}
	}
	[Theory]
	[InlineData("\"80, invalid\"")]
	[InlineData("[80,\"invalid\"]")]
	[InlineData("\", ,\"")]
	public void Eligibility_numeric_membership_rejects_invalid_or_empty_lists(string values) => Should.Throw<ArgumentException>(() =>
		AdmissionEligibilityConditionSchema.Validate("{\"operator\":\"In\",\"field\":\"application.answer\",\"value\":" + values + "}",
			new Dictionary<string, AdmissionFormFieldType> { ["application.answer"] = AdmissionFormFieldType.Number }));
	[Fact]
	public void Eligibility_exception_audit_is_immutable_separate_and_requires_reason()
	{
		var actor = Guid.NewGuid(); var row = Evaluation();
		var request = new AdmissionEligibilityException(Guid.NewGuid(), null, row, "School exception", actor, DateTime.UtcNow);
		Should.Throw<BusinessException>(() => new AdmissionEligibilityException(Guid.NewGuid(), null, row, "Approved", actor, DateTime.UtcNow, request, "approved"));
		Should.Throw<ArgumentException>(() => new AdmissionEligibilityException(Guid.NewGuid(), null, row, " ", actor, DateTime.UtcNow));
		var decision = new AdmissionEligibilityException(Guid.NewGuid(), null, row, "Evidence reviewed", Guid.NewGuid(), DateTime.UtcNow, request, "approved");
		request.Decision.ShouldBe("pending"); decision.RequestId.ShouldBe(request.Id); row.Outcome.ShouldBe("ineligible");
		Should.Throw<BusinessException>(() => new AdmissionEligibilityException(Guid.NewGuid(), null, Evaluation("needsInformation"), "Missing record", actor, DateTime.UtcNow));
	}
	private static AdmissionEligibilityCriterion Age() => new() { Code = "AGE", Name = "Age", Kind = "age", CutoffDate = "2026-04-01", MinAgeMonths = 60, MaxAgeMonths = 72 };
	private static AdmissionEligibilityPolicy Policy() => new() { Version = 1, Enabled = true, Criteria = [Age()] };
	private static AdmissionEligibilityEvaluation Evaluation(string outcome = "ineligible") => new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), 1, outcome, "{}", "{}", "[]", new string('A', 64), Guid.NewGuid(), DateTime.UtcNow);
}
