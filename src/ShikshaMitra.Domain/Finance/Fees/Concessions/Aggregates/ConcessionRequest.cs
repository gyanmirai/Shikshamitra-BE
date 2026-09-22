using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
	public Guid StudentFeeStructureId { get; private set; }
	public Guid ConcessionPolicyId { get; private set; }
	public Guid ConcessionPolicyVersionId { get; private set; }
	public Guid ConcessionTypeId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid BoardId { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid DivisionId { get; private set; }
	public ConcessionRequestStatus Status { get; private set; }
	public string? Remarks { get; private set; }
	public string? RejectionReason { get; private set; }
	public string? ApproverMessage { get; private set; }
	public string? CancellationReason { get; private set; }
	public Guid? CancelledBy { get; private set; }
	public DateTime? CancelledOn { get; private set; }
	public DateTime? SubmittedOn { get; private set; }
	public Guid? SourceRequestId { get; private set; }
	public string SelectedDueFeeIdsJson { get; private set; } = "[]";
	public string? RequestInputSnapshotJson { get; private set; }
	public string? TypeRequirementsSnapshotJson { get; private set; }
	public string? CreationIdempotencyKey { get; private set; }
	public string? CreationPayloadHash { get; private set; }

	public virtual ICollection<ConcessionFeeHeadMapping> ConcessionFeeHeadMappings { get; private set; } = [];
	public virtual ICollection<ConcessionRequestDocument> ConcessionRequestDocuments { get; private set; } = [];
	public virtual ICollection<ConcessionDueFeeAllocation> ConcessionDueFeeAllocations { get; private set; } = [];
	public virtual ICollection<ConcessionReversal> ConcessionReversals { get; private set; } = [];

	private ConcessionRequest() { }

	public ConcessionRequest(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid studentFeeStructureId,
		Guid concessionPolicyId,
		Guid concessionPolicyVersionId,
		Guid concessionTypeId,
		Guid academicYearId,
		Guid boardId,
		Guid educationMediumId,
		Guid standardId,
		Guid divisionId,
		string? remarks,
		ConcessionRequestStatus initialStatus = ConcessionRequestStatus.Pending,
		Guid? sourceRequestId = null,
		string? creationIdempotencyKey = null,
		string? creationPayloadHash = null)
		: base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		StudentFeeStructureId = studentFeeStructureId;
		ConcessionPolicyId = concessionPolicyId;
		ConcessionPolicyVersionId = concessionPolicyVersionId;
		ConcessionTypeId = concessionTypeId;
		if (initialStatus is not (ConcessionRequestStatus.Draft or ConcessionRequestStatus.Pending))
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidTransition);
		Status = initialStatus;
		Remarks = remarks;
		SourceRequestId = sourceRequestId;
		CreationIdempotencyKey = NormalizeOptional(creationIdempotencyKey, 128);
		CreationPayloadHash = NormalizeOptional(creationPayloadHash, 64);
		SetAcademicScope(academicYearId, boardId, educationMediumId, standardId, divisionId);
	}

	public IReadOnlyCollection<Guid> GetSelectedDueFeeIds()
	{
		try
		{
			return JsonSerializer.Deserialize<List<Guid>>(SelectedDueFeeIdsJson) ?? [];
		}
		catch (JsonException)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.ConfigurationSnapshotInvalid);
		}
	}

	public void PrepareSubmissionEvidence(
		IEnumerable<Guid> selectedDueFeeIds,
		string requestInputSnapshotJson,
		string typeRequirementsSnapshotJson)
	{
		EnsureDraft();
		var dueFeeIds = selectedDueFeeIds.Where(x => x != Guid.Empty).Distinct().OrderBy(x => x).ToList();
		if (dueFeeIds.Count == 0 || string.IsNullOrWhiteSpace(requestInputSnapshotJson) ||
			string.IsNullOrWhiteSpace(typeRequirementsSnapshotJson))
			throw new BusinessException(FinanceConcessionErrorCodes.ConfigurationSnapshotInvalid);
		SelectedDueFeeIdsJson = JsonSerializer.Serialize(dueFeeIds);
		RequestInputSnapshotJson = requestInputSnapshotJson;
		TypeRequirementsSnapshotJson = typeRequirementsSnapshotJson;
	}

	public void ClearSubmissionEvidence()
	{
		EnsureDraft();
		SelectedDueFeeIdsJson = "[]";
		RequestInputSnapshotJson = null;
		TypeRequirementsSnapshotJson = null;
	}

	public void Submit(DateTime submittedOn)
	{
		EnsureDraft();
		if (GetSelectedDueFeeIds().Count == 0 || string.IsNullOrWhiteSpace(RequestInputSnapshotJson) ||
			string.IsNullOrWhiteSpace(TypeRequirementsSnapshotJson))
			throw new BusinessException(FinanceConcessionErrorCodes.ConfigurationSnapshotInvalid);
		Status = ConcessionRequestStatus.Pending;
		SubmittedOn = submittedOn;
	}

	public Guid GetAcademicYearId() => AcademicYearId;
	public Guid GetBoardId() => BoardId;
	public Guid GetEducationMediumId() => EducationMediumId;
	public Guid GetStandardId() => StandardId;
	public Guid GetDivisionId() => DivisionId;
	public Guid? GetApprovedBy() => this.GetProperty<Guid?>(ApprovedByProperty);
	public DateTime? GetApprovedOn() => this.GetProperty<DateTime?>(ApprovedOnProperty);

	public void SetAcademicScope(Guid academicYearId, Guid boardId, Guid mediumId, Guid standardId, Guid divisionId)
	{
		AcademicYearId = academicYearId;
		BoardId = boardId;
		EducationMediumId = mediumId;
		StandardId = standardId;
		DivisionId = divisionId;
		this.SetProperty(AcademicYearIdProperty, academicYearId);
		this.SetProperty(BoardIdProperty, boardId);
		this.SetProperty(EducationMediumIdProperty, mediumId);
		this.SetProperty(StandardIdProperty, standardId);
		this.SetProperty(DivisionIdProperty, divisionId);
	}

	public decimal GetTotalRequestedConcession() =>
		ConcessionFeeHeadMappings.Sum(m => m.FeeHeadConcessionAmount);

	public decimal GetTotalApprovedConcession() =>
		ConcessionDueFeeAllocations.Sum(a => a.ConcessionAmount);

	public decimal GetTotalReversedConcession() =>
		ConcessionReversals.Sum(r => r.Amount);

	public decimal GetNetApprovedConcession() =>
		Math.Max(0m, GetTotalApprovedConcession() - GetTotalReversedConcession());

	public void ReplaceFeeHeadMappings(IEnumerable<ConcessionFeeHeadMapping> mappings)
	{
		EnsureDraft();

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
		EnsureDraft();
		ConcessionRequestDocuments.Clear();
		foreach (var doc in documents)
		{
			ConcessionRequestDocuments.Add(doc);
		}
	}

	public void UpdateRemarks(string? remarks)
	{
		EnsureDraft();
		Remarks = remarks;
	}

	public void Approve(Guid approverId, string? message, IEnumerable<ConcessionDueFeeAllocation> allocations, DateTime approvedOn)
	{
		EnsurePending();

		Status = ConcessionRequestStatus.Approved;
		ApproverMessage = message;
		this.SetProperty(ApprovedByProperty, approverId);
		this.SetProperty(ApprovedOnProperty, approvedOn);

		ConcessionDueFeeAllocations.Clear();
		foreach (var allocation in allocations)
		{
			ConcessionDueFeeAllocations.Add(allocation);
		}
	}

	public void Reject(Guid rejectorId, string reason, DateTime rejectedOn)
	{
		EnsurePending();

		if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidRejectionReason);
		}

		Status = ConcessionRequestStatus.Rejected;
		RejectionReason = reason.Trim();
		this.SetProperty(ApprovedByProperty, rejectorId);
		this.SetProperty(ApprovedOnProperty, rejectedOn);
	}

	public void RecordReversals(IEnumerable<ConcessionReversal> reversals)
	{
		if (Status is not (ConcessionRequestStatus.Approved or ConcessionRequestStatus.PartiallyReversed))
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidReversal)
				.WithData("Status", Status.ToString());

		foreach (var reversal in reversals)
			ConcessionReversals.Add(reversal);

		var reversed = GetTotalReversedConcession();
		var approved = GetTotalApprovedConcession();
		if (reversed <= 0m || reversed > approved)
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidReversal);

		Status = reversed == approved
			? ConcessionRequestStatus.Reversed
			: ConcessionRequestStatus.PartiallyReversed;
	}

	public void Cancel(Guid actorId, string reason, DateTime cancelledOn)
	{
		if (Status is not (ConcessionRequestStatus.Draft or ConcessionRequestStatus.Pending))
		{
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidTransition)
				.WithData("Status", Status.ToString());
		}
		var normalizedReason = reason?.Trim();
		if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length is < 10 or > 2000)
			throw new BusinessException(FinanceConcessionErrorCodes.CancellationReasonRequired);

		Status = ConcessionRequestStatus.Cancelled;
		CancellationReason = normalizedReason;
		CancelledBy = actorId;
		CancelledOn = cancelledOn;
	}

	public void EnsureDraft()
	{
		if (Status != ConcessionRequestStatus.Draft)
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidTransition)
				.WithData("Status", Status.ToString());
	}

	public void ChangePolicySnapshot(Guid concessionPolicyId, Guid concessionPolicyVersionId)
	{
		EnsureDraft();
		if (concessionPolicyId == Guid.Empty || concessionPolicyVersionId == Guid.Empty)
			throw new BusinessException(FinanceConcessionErrorCodes.ConfigurationSnapshotInvalid);
		ConcessionPolicyId = concessionPolicyId;
		ConcessionPolicyVersionId = concessionPolicyVersionId;
	}

	public void EnsurePending()
	{
		if (Status != ConcessionRequestStatus.Pending)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.NotPending)
				.WithData("Status", Status.ToString());
		}
	}

	public void EnsureCanDelete() => EnsureDraft();

	private static string? NormalizeOptional(string? value, int maxLength)
	{
		var normalized = value?.Trim();
		if (string.IsNullOrWhiteSpace(normalized)) return null;
		if (normalized.Length > maxLength)
			throw new BusinessException(FinanceConcessionErrorCodes.IdempotencyKeyConflict);
		return normalized;
	}
}
