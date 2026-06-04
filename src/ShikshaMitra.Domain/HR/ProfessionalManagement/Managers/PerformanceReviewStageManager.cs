using System;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceReviewStageManager(
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant)
	: DomainService
{
	public virtual PerformanceReviewStage CreateStage(
		PerformanceReviewAssignment assignment,
		PerformanceReviewStageType stageType)
	{
		return new PerformanceReviewStage(
			guidGenerator.Create(),
			currentTenant.Id,
			assignment.Id,
			stageType,
			ProfessionalManagementRules.ResolveStageReviewer(assignment, stageType));
	}

	public virtual void SaveStage(
		PerformanceReviewStage stage,
		string? summaryComment,
		bool submit,
		DateTime timestamp)
	{
		if (submit)
		{
			stage.Submit(summaryComment, timestamp);
			return;
		}

		stage.SaveDraft(summaryComment);
	}
}
