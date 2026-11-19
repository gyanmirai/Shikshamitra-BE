using System;
using System.Linq;
using System.Text.Json;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentSubjectScores_Tests
{
	private static readonly Guid Board = Guid.NewGuid();
	private static readonly Guid Medium = Guid.NewGuid();
	private static readonly Guid Class = Guid.NewGuid();
	private static readonly Guid Actor = Guid.NewGuid();

	[Theory]
	[InlineData("overall", "passed")]
	[InlineData("everySubject", "failed")]
	[InlineData("both", "failed")]
	public void Combined_result_uses_selected_rule_and_preserves_decimal_marks(string rule, string expected)
	{
		var policy = Policy(rule).Resolve(Board, Medium, Class);
		var attempt = Attempt(policy);
		attempt.RecordSubjectResult([Score("english", 15.5m), Score("maths", 44.5m)], null, Actor, policy);
		attempt.Marks.ShouldBe(60);
		attempt.Outcome.ShouldBe(expected);
		attempt.GetSubjectScores().Select(score => score.Marks).ShouldBe([15.5m, 44.5m]);
	}

	[Fact]
	public void Partial_draft_survives_serialization_without_creating_final_evidence_or_treating_blank_as_zero()
	{
		var policy = Policy().Resolve(Board, Medium, Class);
		var attempt = Attempt(policy);
		attempt.SaveResultDraft([Score("english", 0)], "Partial entry", policy);
		attempt.GetSubjectScores().Single(score => score.Code == "english").Marks.ShouldBe(0);
		attempt.GetSubjectScores().Single(score => score.Code == "maths").Marks.ShouldBeNull();
		attempt.Marks.ShouldBeNull();
		attempt.RecordedBy.ShouldBeNull();
		attempt.Status.ShouldBe("pending");
		attempt.Outcome.ShouldBe("pending");
		attempt.HasCompleteSubjectScores(policy).ShouldBeFalse();
		Should.Throw<BusinessException>(() => AdmissionAssessmentEvidence.EnsureComplete(attempt));
		Should.Throw<BusinessException>(() => attempt.RecordSubjectResult(attempt.GetSubjectScores().ToList(), null, Actor, policy));
		attempt.SaveResultDraft([Score("english", 20), Score("maths", 20)], null, policy);
		attempt.HasCompleteSubjectScores(policy).ShouldBeTrue();
		attempt.RecordSubjectResult(attempt.GetSubjectScores().ToList(), null, Actor, policy);
		attempt.Outcome.ShouldBe("passed");
		Should.Throw<BusinessException>(() => attempt.SaveResultDraft([], null, policy));
	}

	[Fact]
	public void Immutable_attempt_profile_wins_over_changed_names_rules_and_totals()
	{
		var policy = Policy("both").Resolve(Board, Medium, Class);
		var attempt = Attempt(policy);
		var altered = policy with { EffectiveProfile = policy.EffectiveProfile! with { PassingMarks = 0, PassRule = "overall", Subjects = [new() { Code = "different", Name = "Altered", TotalMarks = 100 }] } };
		attempt.RecordSubjectResult([Score("english", 15), Score("maths", 45)], null, Actor, altered);
		attempt.Outcome.ShouldBe("failed");
		var frozen = AdmissionAssessmentPolicy.Read(attempt.PolicySnapshotJson)!;
		frozen.EffectiveProfile!.Subjects.First().Name.ShouldBe("English");
		frozen.EffectiveProfile.PassRule.ShouldBe("both");
	}

	[Fact]
	public void Profile_resolution_overwrites_injected_effective_profile_and_never_falls_back_to_another_class()
	{
		var policy = Policy() with { EffectiveProfile = Policy().ClassProfiles.Single() with { PassingMarks = 0 } };
		policy.Resolve(Board, Medium, Class).EffectiveProfile!.PassingMarks.ShouldBe(40);
		Should.Throw<ArgumentException>(() => policy.Resolve(Board, Medium, Guid.NewGuid()));
		Should.Throw<ArgumentException>(() => policy.Resolve(Guid.NewGuid(), Medium, Class));
	}

	[Fact]
	public void Duplicate_class_assignments_subjects_and_invalid_thresholds_are_rejected()
	{
		var policy = Policy();
		var profile = policy.ClassProfiles.Single();
		Should.Throw<ArgumentException>(() => (policy with { ClassProfiles = [profile, profile with { Id = "other" }] }).Validate());
		Should.Throw<ArgumentException>(() => (policy with { ClassProfiles = [profile with { Subjects = [profile.Subjects[0], profile.Subjects[0]] }] }).Validate());
		Should.Throw<ArgumentException>(() => (policy with { ClassProfiles = [profile with { Subjects = [profile.Subjects[0] with { PassingMarks = null }] }] }).Validate());
		Should.Throw<ArgumentException>(() => (policy with { ClassProfiles = [profile with { PassingMarks = 101 }] }).Validate());
	}

	[Fact]
	public void Unknown_duplicate_and_out_of_range_marks_do_not_mutate_existing_draft()
	{
		var policy = Policy().Resolve(Board, Medium, Class);
		var attempt = Attempt(policy);
		attempt.SaveResultDraft([Score("english", 10)], null, policy);
		var before = attempt.SubjectScoresJson;
		Should.Throw<BusinessException>(() => attempt.SaveResultDraft([Score("english", 10), Score("english", 20)], null, policy));
		Should.Throw<BusinessException>(() => attempt.SaveResultDraft([Score("foreign", 10)], null, policy));
		Should.Throw<BusinessException>(() => attempt.SaveResultDraft([Score("english", 50.01m)], null, policy));
		Should.Throw<BusinessException>(() => attempt.SaveResultDraft([Score("english", -0.01m)], null, policy));
		attempt.SubjectScoresJson.ShouldBe(before);
		Should.Throw<BusinessException>(() => attempt.RecordResult(100, "passed", null, Actor, policy));
	}

	[Fact]
	public void Whole_exam_retake_starts_empty_and_absence_never_fabricates_subject_zeroes()
	{
		var policy = Policy().Resolve(Board, Medium, Class);
		var first = Attempt(policy);
		first.RecordSubjectResult([Score("english", 0), Score("maths", 0)], null, Actor, policy);
		first.RequestRetake("Repeat the exam");
		var second = Attempt(policy);
		second.GetSubjectScores().ShouldBeEmpty();
		second.RecordAttendance("absent", null, Actor, policy);
		second.Outcome.ShouldBe("absent");
		second.Marks.ShouldBeNull();
		second.GetSubjectScores().ShouldBeEmpty();
		first.GetSubjectScores().Count.ShouldBe(2);
	}

	[Fact]
	public void Multi_class_configuration_can_exceed_legacy_limit_but_keeps_the_assessment_bound()
	{
		var policy = Policy();
		policy = policy with { ClassProfiles = Enumerable.Range(1, 35).Select(index => policy.ClassProfiles[0] with { Id = $"profile-{index}", StandardIds = [Guid.NewGuid()] }).ToList() };
		policy.Validate();
		var json = JsonSerializer.Serialize(new { assessment = policy });
		json.Length.ShouldBeGreaterThan(AdmissionWorkflowConsts.MaxConfigurationLength);
		Should.NotThrow(() => Stage(AdmissionWorkflowStageType.Assessment, json));
		Should.Throw<ArgumentException>(() => Stage(AdmissionWorkflowStageType.Form, json));
		Should.Throw<ArgumentException>(() => Stage(AdmissionWorkflowStageType.Assessment, "{\"note\":\"" + new string('x', AdmissionWorkflowConsts.MaxAssessmentConfigurationLength) + "\"}"));
	}

	[Fact]
	public void Entered_draft_marks_count_as_started_even_without_required_attendance_and_zero_is_evidence()
	{
		var policy = Policy().Resolve(Board, Medium, Class);
		var empty = Attempt(policy);
		empty.SaveResultDraft([], null, policy);
		empty.HasEnteredSubjectMarks().ShouldBeFalse();
		empty.Cancel("Exam has not started");
		empty.Status.ShouldBe("cancelled");
		var started = Attempt(policy);
		started.SaveResultDraft([Score("english", 0)], null, policy);
		Should.Throw<BusinessException>(() => started.Cancel("Cannot discard entered marks"));
		started.Status.ShouldBe("pending");
		started.GetSubjectScores().Single(score => score.Code == "english").Marks.ShouldBe(0);
		started.SaveResultDraft([], "Correcting entered marks", policy);
		started.GetSubjectScores().ShouldAllBe(score => score.Marks == null);
		started.HasEnteredSubjectMarks().ShouldBeTrue();
		Should.Throw<BusinessException>(() => started.Cancel("Clearing draft cannot erase the started attempt"));
	}

	private static AdmissionWorkflowStageDefinition Stage(AdmissionWorkflowStageType type, string configuration) => new(Guid.NewGuid(), Guid.NewGuid(), "ASSESSMENT", "Assessment", type, 0, true, false, null, configuration);

	private static AdmissionAssessmentSubjectScore Score(string code, decimal? marks) => new() { Code = code, Marks = marks };
	private static AdmissionAssessmentAttempt Attempt(AdmissionAssessmentPolicy policy) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 1,
		JsonSerializer.Serialize(new { assessment = policy }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
	private static AdmissionAssessmentPolicy Policy(string rule = "both") => new()
	{
		Version = 2, Enabled = true, ScoringEnabled = true, ScoringMode = "subjects", MaxAttempts = 2,
		ClassProfiles = [new() { Id = "class-profile", Name = "Class 1 entrance", BoardId = Board, MediumId = Medium, StandardIds = [Class], PassRule = rule, PassingMarks = 40,
			Subjects = [new() { Code = "english", Name = "English", TotalMarks = 50, PassingMarks = 20 }, new() { Code = "maths", Name = "Maths", TotalMarks = 50, PassingMarks = 20 }] }]
	};
}
