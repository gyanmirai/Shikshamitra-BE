using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.Fees.Concessions;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.ConcessionRequests;

public class ConcessionRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public const string BoardIdProperty = "BoardId";
	public const string EducationMediumIdProperty = "EducationMediumId";
	public const string StandardIdProperty = "StandardId";
	public const string DivisionIdProperty = "DivisionId";
	public const string AcademicYearIdProperty = "AcademicYearId";
	public const string ApprovedByProperty = "ApprovedBy";
	public const string ApprovedOnProperty = "ApprovedOn";

	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid ConcessionTypeId { get; private set; }
	public ConcessionRequestStatus Status { get; private set; }
	public string? Remarks { get; private set; }
	public string? RejectionReason { get; private set; }
	public string? ApproverMessage { get; private set; }

	public virtual ICollection<ConcessionFeeHeadMapping> ConcessionFeeHeadMappings { get; private set; } = [];
	public virtual ICollection<ConcessionRequestDocument> ConcessionRequestDocuments { get; private set; } = [];
	public virtual ICollection<ConcessionDueFeeAllocation> ConcessionDueFeeAllocations { get; private set; } = [];

	private ConcessionRequest() { }

	public ConcessionRequest(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid concessionTypeId,
		Guid academicYearId,
		Guid boardId,
		Guid educationMediumId,
		Guid standardId,
		Guid divisionId,
		string? remarks)
		: base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		ConcessionTypeId = concessionTypeId;
		Status = ConcessionRequestStatus.Pending;
		Remarks = remarks;
		SetAcademicScope(academicYearId, boardId, educationMediumId, standardId, divisionId);
	}

	public Guid GetAcademicYearId() => this.GetProperty<Guid>(AcademicYearIdProperty);
	public Guid GetBoardId() => this.GetProperty<Guid>(BoardIdProperty);
	public Guid GetEducationMediumId() => this.GetProperty<Guid>(EducationMediumIdProperty);
	public Guid GetStandardId() => this.GetProperty<Guid>(StandardIdProperty);
	public Guid GetDivisionId() => this.GetProperty<Guid>(DivisionIdProperty);
	public Guid? GetApprovedBy() => this.GetProperty<Guid?>(ApprovedByProperty);
	public DateTime? GetApprovedOn() => this.GetProperty<DateTime?>(ApprovedOnProperty);

	public void SetAcademicScope(Guid academicYearId, Guid boardId, Guid mediumId, Guid standardId, Guid divisionId)
	{
		this.SetProperty(AcademicYearIdProperty, academicYearId);
		this.SetProperty(BoardIdProperty, boardId);
		this.SetProperty(EducationMediumIdProperty, mediumId);
		this.SetProperty(StandardIdProperty, standardId);
		this.SetProperty(DivisionIdProperty, divisionId);
	}

	public decimal GetTotalRequestedConcession() =>
		ConcessionFeeHeadMappings.Sum(m => m.FeeHeadConcessionAmount);

	public void ReplaceFeeHeadMappings(IEnumerable<ConcessionFeeHeadMapping> mappings)
	{
		EnsurePending();

		var incoming = mappings.ToList();
		var incomingFeeHeadIds = incoming.Select(m => m.FeeHeadId).ToHashSet();

		// Remove stale mappings — EF Core emits DELETE only for these
		foreach (var stale in ConcessionFeeHeadMappings
			.Where(m => !incomingFeeHeadIds.Contains(m.FeeHeadId))
			.ToList())
		{
			ConcessionFeeHeadMappings.Remove(stale);
		}

		// Update existing (EF Core emits UPDATE, same key — no constraint conflict)
		// or add truly new ones (EF Core emits INSERT for a brand-new FeeHeadId only)
		foreach (var newMapping in incoming)
		{
			var existing = ConcessionFeeHeadMappings
				.FirstOrDefault(m => m.FeeHeadId == newMapping.FeeHeadId);

			if (existing is not null)
				existing.UpdateAmounts(
					newMapping.PreConcessionFeeHeadAmount,
					newMapping.FeeHeadConcessionAmount,
					newMapping.PostConcessionFeeHeadAmount,
					newMapping.UserSpecifiedConcessionAmountType,
					newMapping.UserSpecifiedConcessionAmountValue);
			else
				ConcessionFeeHeadMappings.Add(newMapping);
		}
	}

	public void ReplaceDocuments(IEnumerable<ConcessionRequestDocument> documents)
	{
		EnsurePending();
		ConcessionRequestDocuments.Clear();
		foreach (var doc in documents)
		{
			ConcessionRequestDocuments.Add(doc);
		}
	}

	public void UpdateRemarks(string? remarks)
	{
		EnsurePending();
		Remarks = remarks;
	}

	public void Approve(Guid approverId, string? message, IEnumerable<ConcessionDueFeeAllocation> allocations)
	{
		EnsurePending();

		Status = ConcessionRequestStatus.Approved;
		ApproverMessage = message;
		this.SetProperty(ApprovedByProperty, approverId);
		this.SetProperty(ApprovedOnProperty, DateTime.UtcNow);

		ConcessionDueFeeAllocations.Clear();
		foreach (var allocation in allocations)
		{
			ConcessionDueFeeAllocations.Add(allocation);
		}
	}

	public void Reject(Guid rejectorId, string reason)
	{
		EnsurePending();

		if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidRejectionReason);
		}

		if (rejectorId == CreatorId)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.ApproverCannotBeRequestor);
		}

		Status = ConcessionRequestStatus.Rejected;
		RejectionReason = reason.Trim();
		this.SetProperty(ApprovedByProperty, rejectorId);
		this.SetProperty(ApprovedOnProperty, DateTime.UtcNow);
	}

	public void Cancel()
	{
		if (Status is ConcessionRequestStatus.Approved or ConcessionRequestStatus.Rejected)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.CannotApproveOrRejectNonPending)
				.WithData("Status", Status.ToString());
		}

		Status = ConcessionRequestStatus.Cancelled;
	}

	public void EnsurePending()
	{
		if (Status != ConcessionRequestStatus.Pending)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.NotPending)
				.WithData("Status", Status.ToString());
		}
	}

	public void EnsureCanDelete() => EnsurePending();
}
