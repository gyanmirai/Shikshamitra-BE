using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class ConcessionRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid BoardId { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid DivisionId { get; private set; }
	public Guid StudentId { get; private set; }

	/// <summary>Fee structure resolved server-side from board+medium+standard.</summary>
	public Guid? FeeStructureId { get; private set; }

	/// <summary>Academic year in YY-YY format, e.g. "2025-26".</summary>
	public string AcademicYear { get; private set; }

	public ConcessionRequestStatus Status { get; private set; }

	/// <summary>Current wizard step (1–4).</summary>
	public int CurrentStep { get; private set; }

	/// <summary>Comma-separated list of completed step numbers, e.g. "1,2".</summary>
	public string CompletedStepsRaw { get; private set; }

	// Approval / Rejection metadata
	public Guid? ApproverId { get; private set; }
	public string? ApproverMessage { get; private set; }
	public DateTime? ApprovedAt { get; private set; }

	public Guid? RejectorId { get; private set; }
	public string? RejectionReason { get; private set; }
	public DateTime? RejectedAt { get; private set; }

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
		BoardId = boardId;
		EducationMediumId = educationMediumId;
		StandardId = standardId;
		DivisionId = divisionId;
		StudentId = studentId;
		FeeStructureId = feeStructureId;
		AcademicYear = academicYear;
		Status = ConcessionRequestStatus.PENDING;
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

		if (Status == ConcessionRequestStatus.PENDING)
			Status = ConcessionRequestStatus.PENDING;
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

	public void Approve(Guid approverId, string? message)
	{
		ApproverId = approverId;
		ApproverMessage = message;
		ApprovedAt = DateTime.UtcNow;
		Status = ConcessionRequestStatus.APPROVED;
	}

	public void Reject(Guid rejectorId, string reason)
	{
		RejectorId = rejectorId;
		RejectionReason = reason;
		RejectedAt = DateTime.UtcNow;
		Status = ConcessionRequestStatus.REJECTED;
	}
}
