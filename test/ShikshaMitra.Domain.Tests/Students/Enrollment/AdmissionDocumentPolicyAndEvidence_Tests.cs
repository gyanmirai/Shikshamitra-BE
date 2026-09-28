using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionDocumentPolicyAndEvidence_Tests
{
	[Fact]
	public void Class_scope_uses_identity_and_fails_closed_when_class_is_unknown()
	{
		var classOne = Guid.NewGuid();
		var classTen = Guid.NewGuid();
		var requirement = Requirement() with { StandardIds = [classTen] };
		var values = new Dictionary<string, string> { ["application.class"] = classTen.ToString() };
		AdmissionDocumentRules.ConditionStatus(requirement, values, classOne).ShouldBe("notMatched");
		AdmissionDocumentRules.ConditionStatus(requirement, values, classTen).ShouldBe("notConditional");
		AdmissionDocumentRules.ConditionStatus(requirement, values).ShouldBe("undetermined");
		AdmissionDocumentRules.ConditionStatus(Requirement(), values).ShouldBe("notConditional");
		var conditional = requirement with { Requirement = "conditional", ConditionJson = "{\"operator\":\"Equals\",\"field\":\"application.transfer\",\"value\":\"yes\"}" };
		AdmissionDocumentRules.ConditionStatus(conditional, values, classOne).ShouldBe("notMatched");
		AdmissionDocumentRules.ConditionStatus(conditional, values, classTen).ShouldBe("undetermined");
		values["application.transfer"] = "yes";
		AdmissionDocumentRules.ConditionStatus(conditional, values, classTen).ShouldBe("matched");
	}
	[Fact]
	public void Invalid_class_selections_cannot_publish()
	{
		var id = Guid.NewGuid();
		foreach (var ids in new List<Guid>[] { [], [Guid.Empty], [id, id] })
			Should.Throw<ArgumentException>(() => (Requirement() with { StandardIds = ids }).Validate());
	}
	[Theory]
	[InlineData(null)]
	[InlineData("{}")]
	[InlineData("{\"documentVerification\":{\"enabled\":false}}")]
	public void Missing_or_disabled_policy_preserves_legacy(string? json) => AdmissionDocumentPolicy.Read(json).ShouldBeNull();
	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public void Unsupported_policy_version_is_rejected(int version) => Should.Throw<ArgumentException>(() => (Policy() with { Version = version }).Validate());
	[Fact]
	public void Duplicate_codes_and_unsafe_file_constraints_are_rejected()
	{
		Should.Throw<ArgumentException>(() => (Policy() with { Requirements = [Requirement(), Requirement()] }).Validate());
		Should.Throw<ArgumentException>(() => (Requirement() with { AllowedExtensions = ["exe"] }).Validate());
		Should.Throw<ArgumentException>(() => (Requirement() with { MaxFileSizeMb = 21 }).Validate());
		Should.Throw<ArgumentException>(() => (Requirement() with { Requirement = "conditional", ConditionJson = "{}" }).Validate());
	}
	[Fact]
	public void Conditions_fail_closed_for_missing_fields_but_evaluate_saved_blank()
	{
		var requirement = Requirement() with { Requirement = "conditional", ConditionJson = "{\"operator\":\"IsEmpty\",\"field\":\"application.guardian\"}" };
		AdmissionDocumentRules.ConditionStatus(requirement, new Dictionary<string, string>()).ShouldBe("undetermined");
		AdmissionDocumentRules.ConditionStatus(requirement, new Dictionary<string, string> { ["application.guardian"] = "" }).ShouldBe("matched");
		AdmissionDocumentRules.ConditionStatus(requirement, new Dictionary<string, string> { ["application.guardian"] = "Provided" }).ShouldBe("notMatched");
	}
	[Theory]
	[InlineData("documents.status")]
	[InlineData("secret")]
	public void Circular_or_unscoped_document_conditions_are_rejected(string field) => Should.Throw<ArgumentException>(() =>
		AdmissionDocumentCondition.Validate(JsonSerializer.Serialize(new { @operator = "Equals", field, value = "verified" })));
	[Fact]
	public void Deep_and_large_conditions_are_rejected()
	{
		var leaf = "{\"operator\":\"IsEmpty\",\"field\":\"application.answer\"}";
		var nested = leaf;
		for (var index = 0; index < 6; index++) nested = "{\"operator\":\"ALL\",\"conditions\":[" + nested + "]}";
		Should.Throw<ArgumentException>(() => AdmissionDocumentCondition.Validate(nested));
		Should.Throw<ArgumentException>(() => AdmissionDocumentCondition.Validate("{\"operator\":\"ALL\",\"conditions\":[" + string.Join(",", Enumerable.Repeat(leaf, 31)) + "]}"));
	}
	[Theory]
	[InlineData("null")]
	[InlineData("{}")]
	[InlineData("[]")]
	[InlineData("\"not a number\"")]
	public void Malformed_numeric_operands_cannot_hide_a_conditional_requirement(string value) => Should.Throw<ArgumentException>(() =>
		AdmissionDocumentCondition.Validate("{\"operator\":\"GreaterThan\",\"field\":\"application.age\",\"value\":" + value + "}"));
	[Fact]
	public void Both_collection_needs_file_and_original_before_acceptance()
	{
		var upload = Evidence("upload", Requirement() with { CollectionMode = "both" });
		Should.Throw<BusinessException>(() => Review(upload, "accepted"));
		var observation = Evidence("original", Requirement() with { CollectionMode = "both" }, file: upload.GetFile());
		Review(observation, "accepted").Decision.ShouldBe("accepted");
		observation.FileSubmittedBy.ShouldBe(upload.SubmittedBy);
	}
	[Fact]
	public void Independent_review_excludes_retained_file_submitter_and_original_observer()
	{
		var upload = Evidence("upload", Requirement() with { CollectionMode = "both" });
		var observer = Guid.NewGuid();
		var row = new AdmissionDocumentEvidence(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Requirement() with { CollectionMode = "both" },
			2, "original", observer, DateTime.UtcNow, "{}", upload.GetFile(), observer, DateTime.UtcNow, "Inspected original");
		foreach (var actor in new[] { upload.SubmittedBy, observer })
			Should.Throw<BusinessException>(() => new AdmissionDocumentReview(Guid.NewGuid(), null, row, Policy() with { RequireIndependentReview = true }, "accepted", null, actor, DateTime.UtcNow));
	}
	[Fact]
	public void Replacement_requires_fresh_original_check_and_review()
	{
		var accepted = Evidence("original", Requirement() with { CollectionMode = "both" }, file: File(Guid.NewGuid()));
		var review = Review(accepted, "accepted");
		var replacement = Evidence("upload", Requirement() with { CollectionMode = "both" });
		replacement.OriginalSeen.ShouldBeFalse();
		AdmissionDocumentRules.Status(replacement, [review]).ShouldBe("pendingReview");
	}
	[Fact]
	public void Waiver_and_rejection_require_explanations_and_waiver_cannot_be_reviewed()
	{
		Should.Throw<BusinessException>(() => new AdmissionDocumentEvidence(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Requirement(), 1,
			"waiver", Guid.NewGuid(), DateTime.UtcNow, "{}"));
		Should.Throw<BusinessException>(() => Review(Evidence("upload", Requirement()), "rejected"));
		Should.Throw<BusinessException>(() => Review(Evidence("waiver", Requirement()), "accepted"));
	}
	[Fact]
	public void Student_link_is_idempotent_and_cannot_be_reassigned()
	{
		var row = Evidence("upload", Requirement());
		var student = Guid.NewGuid();
		var actor = Guid.NewGuid();
		var at = DateTime.UtcNow;
		row.LinkToStudent(student, actor, at);
		row.LinkToStudent(student, Guid.NewGuid(), at.AddDays(1));
		row.StudentLinkedBy.ShouldBe(actor);
		row.StudentLinkedAt.ShouldBe(at);
		Should.Throw<BusinessException>(() => row.LinkToStudent(Guid.NewGuid(), actor, at));
	}
	private static AdmissionDocumentRequirement Requirement() => new() { Code = "BIRTH", Name = "Birth certificate" };
	private static AdmissionDocumentPolicy Policy() => new() { Version = 1, Enabled = true, Requirements = [Requirement()] };
	private static AdmissionDocumentFile File(Guid actor) => new("proof.pdf", "application/pdf", 20, new string('a', 64), "private/blob", actor);
	private static AdmissionDocumentEvidence Evidence(string kind, AdmissionDocumentRequirement requirement, AdmissionDocumentFile? file = null)
	{
		var actor = Guid.NewGuid();
		return new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), requirement, 1, kind, actor, DateTime.UtcNow, "{}",
			kind == "upload" ? File(actor) : file, kind == "original" ? actor : null, kind == "original" ? DateTime.UtcNow : null, kind == "upload" ? null : "Reason recorded");
	}
	private static AdmissionDocumentReview Review(AdmissionDocumentEvidence evidence, string decision) => new(Guid.NewGuid(), null, evidence, Policy(), decision, null, Guid.NewGuid(), DateTime.UtcNow);
}
