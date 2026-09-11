using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("MISSING")]
	[InlineData("ELIGIBILITY")]
	[InlineData("WRONG_TYPE")]
	public Task Eligibility_publish_rejects_missing_self_or_wrong_type_source_stages(string sourceCode) => RunCertificationScenarioAsync(async () =>
	{
		var policy = EligibilityPolicyJson([new { code = "EXAM", name = "Prior exam", kind = "assessment", sourceStageCode = sourceCode, requiredOutcome = "passed" }]);
		var exception = await Should.ThrowAsync<BusinessException>(() => CreateEligibilityFixtureAsync(policy, new DateTime(2020, 4, 1), priorStages:
			new List<CreateUpdateAdmissionWorkflowStageDto> { new() { Code = "WRONG_TYPE", Name = "Manual source", StageType = AdmissionWorkflowStageType.Custom } }));
		exception.Code.ShouldBe(AdmissionWorkflowErrorCodes.InvalidTransition);
		((string[])exception.Data["ValidationIssues"]!).ShouldContain(AdmissionEligibilityErrorCodes.InvalidPolicy);
	});
	[Fact]
	public Task Eligibility_publish_rejects_foreign_or_unavailable_form_id() => RunCertificationScenarioAsync(async () =>
	{
		var policy = EligibilityPolicyJson([new { code = "MARKS", name = "Marks", kind = "application", formDefinitionId = Guid.NewGuid(),
			conditionJson = "{\"operator\":\"GreaterThanOrEqual\",\"field\":\"application.ANSWER\",\"value\":50}" }]);
		var exception = await Should.ThrowAsync<BusinessException>(() => CreateEligibilityFixtureAsync(policy, new DateTime(2020, 4, 1)));
		exception.Code.ShouldBe(AdmissionWorkflowErrorCodes.InvalidTransition);
		((string[])exception.Data["ValidationIssues"]!).ShouldContain(AdmissionEligibilityErrorCodes.InvalidPolicy);
	});
}
