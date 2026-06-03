using System;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceReviewResponse : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReviewAssignmentId { get; private set; }
	public Guid StageId { get; private set; }
	public Guid CriterionId { get; private set; }
	public int? Rating { get; private set; }
	public bool IsNotApplicable { get; private set; }
	public string? Comment { get; private set; }

	protected PerformanceReviewResponse()
	{
	}

	public PerformanceReviewResponse(Guid id, Guid? tenantId, Guid reviewAssignmentId, Guid stageId, Guid criterionId, int? rating, bool isNotApplicable, string? comment) : base(id)
	{
		TenantId = tenantId;
		ReviewAssignmentId = reviewAssignmentId;
		StageId = stageId;
		CriterionId = criterionId;
		Update(rating, isNotApplicable, comment);
	}

	public void Update(int? rating, bool isNotApplicable, string? comment)
	{
		Rating = isNotApplicable ? null : rating;
		IsNotApplicable = isNotApplicable;
		Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
	}
}
