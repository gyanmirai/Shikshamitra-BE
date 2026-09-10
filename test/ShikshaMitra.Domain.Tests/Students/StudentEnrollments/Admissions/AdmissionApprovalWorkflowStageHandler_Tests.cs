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

	[Fact]
	public async Task Approval_Stage_Should_Complete_After_The_Application_Is_Approved()
	{
		await Should.NotThrowAsync(() =>
			_registry.EnsureCanCompleteAsync(
				AdmissionWorkflowStageType.Approval,
				Context(AdmissionApplicationStatus.Approved)));
	}

	[Theory]
	[InlineData(AdmissionApplicationStatus.Draft)]
	[InlineData(AdmissionApplicationStatus.Submitted)]
	[InlineData(AdmissionApplicationStatus.InReview)]
	[InlineData(AdmissionApplicationStatus.Waitlisted)]
	[InlineData(AdmissionApplicationStatus.Rejected)]
	public async Task Approval_Stage_Should_Wait_For_An_Approved_Decision(
		AdmissionApplicationStatus status)
	{
		await Should.ThrowAsync<BusinessException>(() =>
			_registry.EnsureCanCompleteAsync(AdmissionWorkflowStageType.Approval, Context(status)));
	}

	private static AdmissionWorkflowStageRuntimeContext Context(AdmissionApplicationStatus status) =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status);
}
