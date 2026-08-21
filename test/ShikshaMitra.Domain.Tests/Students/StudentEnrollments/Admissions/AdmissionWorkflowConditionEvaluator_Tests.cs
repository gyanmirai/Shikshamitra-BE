using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowConditionEvaluator_Tests
{
	private readonly AdmissionWorkflowConditionEvaluator _evaluator = new();

	[Fact]
	public void Should_Evaluate_Controlled_Nested_Rules()
	{
		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["application.score"] = "72",
			["application.category"] = "General"
		};
		const string rule =
			"""
			{
			  "operator": "ALL",
			  "conditions": [
			    { "operator": "GreaterThanOrEqual", "field": "application.score", "value": 60 },
			    { "operator": "In", "field": "application.category", "value": ["General", "EWS"] }
			  ]
			}
			""";

		_evaluator.Evaluate(rule, values).ShouldBeTrue();
	}

	[Fact]
	public void Should_Use_Ordinal_Ignore_Case_For_Text_Operators()
	{
		var values = new Dictionary<string, string> { ["decision"] = "APPROVED" };

		_evaluator.Evaluate(
			"""{ "operator": "Equals", "field": "decision", "value": "approved" }""",
			values).ShouldBeTrue();
		_evaluator.Evaluate(
			"""{ "operator": "NotIn", "field": "decision", "value": ["rejected", "waitlisted"] }""",
			values).ShouldBeTrue();
		_evaluator.Evaluate(
			"""{ "operator": "StartsWith", "field": "decision", "value": "app" }""",
			values).ShouldBeTrue();
		_evaluator.Evaluate(
			"""{ "operator": "EndsWith", "field": "decision", "value": "VED" }""",
			values).ShouldBeTrue();
		_evaluator.Evaluate(
			"""{ "operator": "NotContains", "field": "decision", "value": "REJECT" }""",
			values).ShouldBeTrue();
	}

	[Fact]
	public void Workflow_And_Stage_Instances_Should_Enforce_State_Transitions()
	{
		var workflow = new AdmissionApplicationWorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		var stage = new AdmissionApplicationStageInstance(Guid.NewGuid(), null, workflow.Id, Guid.NewGuid(), "FORM", "Application form", AdmissionWorkflowStageType.Form, 0, true);

		var userId = Guid.NewGuid();
		stage.Skip(userId, "Approved exception");
		stage.Status.ShouldBe(AdmissionRuntimeStageStatus.Skipped);
		stage.CompletedBy.ShouldBe(userId);
		stage.CompletionReason.ShouldBe("Approved exception");
		Should.Throw<Volo.Abp.BusinessException>(() => stage.Complete());

		workflow.Complete();
		workflow.Status.ShouldBe(AdmissionWorkflowInstanceStatus.Completed);
		workflow.CompletedAt.ShouldNotBeNull();
		Should.Throw<Volo.Abp.BusinessException>(() => workflow.Complete());
	}

	[Fact]
	public async System.Threading.Tasks.Task Stage_Handler_Registry_Should_Block_Form_Until_Submitted()
	{
		var registry = new AdmissionWorkflowStageHandlerRegistry(new IAdmissionWorkflowStageHandler[]
		{
			new InquiryAdmissionWorkflowStageHandler(),
			new FormAdmissionWorkflowStageHandler(),
			new CustomAdmissionWorkflowStageHandler()
		});

		var context = new AdmissionWorkflowStageRuntimeContext(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AdmissionApplicationStatus.Draft);
		await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
			registry.EnsureCanCompleteAsync(AdmissionWorkflowStageType.Form, context));
		await Should.NotThrowAsync(() =>
			registry.EnsureCanCompleteAsync(AdmissionWorkflowStageType.Form,
				context with { ApplicationStatus = AdmissionApplicationStatus.Submitted }));
	}

	[Theory]
	[InlineData(AdmissionWorkflowStageType.DocumentVerification)]
	[InlineData(AdmissionWorkflowStageType.Approval)]
	[InlineData(AdmissionWorkflowStageType.Enrollment)]
	public async System.Threading.Tasks.Task Manual_Decision_Handlers_Should_Require_A_Submitted_Application(
		AdmissionWorkflowStageType stageType)
	{
		var registry = new AdmissionWorkflowStageHandlerRegistry(new IAdmissionWorkflowStageHandler[]
		{
			new DocumentVerificationAdmissionWorkflowStageHandler(),
			new ApprovalAdmissionWorkflowStageHandler(),
			new EnrollmentAdmissionWorkflowStageHandler()
		});
		var context = new AdmissionWorkflowStageRuntimeContext(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AdmissionApplicationStatus.Draft);

		await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
			registry.EnsureCanCompleteAsync(stageType, context));
		await Should.NotThrowAsync(() =>
			registry.EnsureCanCompleteAsync(stageType,
				context with { ApplicationStatus = AdmissionApplicationStatus.Submitted }));
	}
}
