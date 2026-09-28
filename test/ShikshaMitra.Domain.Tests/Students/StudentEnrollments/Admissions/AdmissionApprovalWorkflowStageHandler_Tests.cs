using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApprovalWorkflowStageHandler_Tests
{
	private readonly AdmissionWorkflowStageHandlerRegistry _registry = new(
		[new ApprovalAdmissionWorkflowStageHandler()]);

	[Theory]
	[InlineData(AdmissionApplicationStatus.Approved)]
	[InlineData(AdmissionApplicationStatus.Offered)]
	[InlineData(AdmissionApplicationStatus.OfferAccepted)]
	[InlineData(AdmissionApplicationStatus.Waitlisted)]
	[InlineData(AdmissionApplicationStatus.Rejected)]
	public async Task Admission_Decision_Stage_Should_Complete_After_A_Decision(
		AdmissionApplicationStatus status)
	{
		await Should.NotThrowAsync(() =>
			_registry.EnsureCanCompleteAsync(
				AdmissionWorkflowStageType.Approval,
				Context(status)));
	}

	[Theory]
	[InlineData(AdmissionApplicationStatus.Draft)]
	[InlineData(AdmissionApplicationStatus.Submitted)]
	[InlineData(AdmissionApplicationStatus.InReview)]
	public async Task Admission_Decision_Stage_Should_Wait_For_A_Decision(
		AdmissionApplicationStatus status)
	{
		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_registry.EnsureCanCompleteAsync(AdmissionWorkflowStageType.Approval, Context(status)));

		exception.Code.ShouldBe(AdmissionApplicationErrorCodes.StageCannotAdvance);
		exception.Data["StageType"].ShouldBe(nameof(AdmissionWorkflowStageType.Approval));
		exception.Data["ApplicationStatus"].ShouldBe(status.ToString());
	}

	private static AdmissionWorkflowStageRuntimeContext Context(AdmissionApplicationStatus status) =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status);
}
