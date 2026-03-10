using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class ConcessionRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid StudentId { get; private set; }

	/// <summary>Fee structure resolved server-side from board+medium+standard.</summary>
	public Guid? FeeStructureId { get; private set; }

	/// <summary>Current wizard step (1–4).</summary>
	public int CurrentStep { get; private set; }

	/// <summary>Comma-separated list of completed step numbers, e.g. "1,2".</summary>
	public string CompletedStepsRaw { get; private set; }

	// Navigation
	public virtual ICollection<ConcessionRequestConcessionType> ConcessionRequestConcessionTypes { get; private set; }

	protected ConcessionRequest()
	{
		ConcessionRequestConcessionTypes = new List<ConcessionRequestConcessionType>();
	}

	public ConcessionRequest(
		Guid id,
		Guid? tenantId,
		Guid boardId,
		Guid educationMediumId,
		Guid standardId,
		Guid divisionId,
		Guid studentId,
		Guid? feeStructureId,
		string academicYear) : base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		FeeStructureId = feeStructureId;
		CurrentStep = 1;
		CompletedStepsRaw = "1";
		ConcessionRequestConcessionTypes = new List<ConcessionRequestConcessionType>();
	}

	public void AdvanceStep(int step)
	{
		if (step > CurrentStep)
			CurrentStep = step;

		var steps = GetCompletedSteps();
		if (!steps.Contains(step))
		{
			steps.Add(step);
			CompletedStepsRaw = string.Join(",", steps);
		}
	}

	public List<int> GetCompletedSteps()
	{
		if (string.IsNullOrWhiteSpace(CompletedStepsRaw))
			return new List<int>();

		var result = new List<int>();
		foreach (var part in CompletedStepsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			if (int.TryParse(part.Trim(), out var n))
				result.Add(n);
		}
		return result;
	}
}
