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
	[InlineData(AdmissionApplicationStatus.OfferAccepted)]
	public async Task Admission_Decision_Stage_Should_Wait_For_A_Decision(
		AdmissionApplicationStatus status)
	{
		await Should.ThrowAsync<BusinessException>(() =>
			_registry.EnsureCanCompleteAsync(AdmissionWorkflowStageType.Approval, Context(status)));
	}

	private static AdmissionWorkflowStageRuntimeContext Context(AdmissionApplicationStatus status) =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status);
}
