using System;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.Collections;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Finance payer for an admission application before enrollment. Its original
/// applicant and academic facts remain evidence after linking a real student.
/// Canonical dues, allocations and receipts retain this account throughout handoff.
/// </summary>
public class FinanceFeeAccount : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid ApplicantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid BoardId { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public Guid? MediumStageId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid? DivisionId { get; private set; }
	public string ApplicantName { get; private set; } = string.Empty;
	public string Currency { get; private set; } = "INR";
	public Guid? StudentId { get; private set; }
	public DateTimeOffset? StudentLinkedAt { get; private set; }
	public Guid? StructureId { get; private set; }
	public int? StructureRevision { get; private set; }
	public string? AssignmentSnapshotJson { get; private set; }
	public string? AssignmentRequestFingerprint { get; private set; }
	public Guid? StudentFeeStructureId { get; private set; }
	public bool HasPaymentEvidence { get; private set; }

	private FinanceFeeAccount() { }

	public FinanceFeeAccount(Guid id, Guid? tenantId, Guid applicationId, Guid applicantId,
		Guid academicYearId, Guid boardId, Guid educationMediumId, Guid? mediumStageId,
		Guid standardId, string applicantName, string currency = "INR", Guid? studentId = null) : base(id)
	{
		if (id == Guid.Empty || applicationId == Guid.Empty || applicantId == Guid.Empty ||
			academicYearId == Guid.Empty || boardId == Guid.Empty || educationMediumId == Guid.Empty ||
			standardId == Guid.Empty || mediumStageId == Guid.Empty || studentId == Guid.Empty ||
			string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
			throw new BusinessException(FinanceFeeAccountErrorCodes.InvalidAccount);
		TenantId = tenantId;
		ApplicationId = applicationId;
		ApplicantId = applicantId;
		AcademicYearId = academicYearId;
		BoardId = boardId;
		EducationMediumId = educationMediumId;
		MediumStageId = mediumStageId;
		StandardId = standardId;
		ApplicantName = Check.NotNullOrWhiteSpace(applicantName, nameof(applicantName), maxLength: 250).Trim();
		Currency = currency.ToUpperInvariant();
		StudentId = studentId;
	}

	/// <summary>Preserves the Finance calculation and versions used to generate dues exactly once.</summary>
	public void CaptureAssignment(Guid structureId, int revision, string snapshotJson, string commandFingerprint)
	{
		if (structureId == Guid.Empty || revision < 1)
			throw new BusinessException(FinanceFeeAccountErrorCodes.InvalidAccount);
		Check.NotNullOrWhiteSpace(snapshotJson, nameof(snapshotJson));
		Check.NotNullOrWhiteSpace(commandFingerprint, nameof(commandFingerprint), maxLength: 64);
		if (AssignmentSnapshotJson != null)
		{
			if (StructureId != structureId || StructureRevision != revision || AssignmentRequestFingerprint != commandFingerprint)
				throw new BusinessException(FinanceFeeAccountErrorCodes.AssignmentConflict);
			return;
		}
		StructureId = structureId;
		StructureRevision = revision;
		AssignmentSnapshotJson = snapshotJson;
		AssignmentRequestFingerprint = commandFingerprint;
	}

	public void LinkStudent(Guid studentId, DateTimeOffset linkedAt)
	{
		StudentId = FinanceFeePayerIdentity.LinkStudent(StudentId, studentId);
		StudentLinkedAt ??= linkedAt.ToUniversalTime();
	}

	public void SetDivision(Guid? divisionId)
	{
		if (divisionId == Guid.Empty)
			throw new BusinessException(FinanceFeeAccountErrorCodes.InvalidAccount);
		DivisionId = divisionId;
	}

	/// <summary>Reads pinned Finance policy identities without depending on application DTOs.</summary>
	public (Guid PolicyId, Guid VersionId)? GetPolicyReference(string family)
	{
		if (string.IsNullOrWhiteSpace(AssignmentSnapshotJson))
			throw new BusinessException(FinanceFeeAccountErrorCodes.InvalidAccount);
		try
		{
			using var document = JsonDocument.Parse(AssignmentSnapshotJson);
			var policies = Property(document.RootElement, "Policies");
			foreach (var policy in policies.EnumerateArray())
			{
				if (!string.Equals(Property(policy, "Family").GetString(), family, StringComparison.OrdinalIgnoreCase)) continue;
				var policyId = Property(policy, "PolicyId").GetGuid();
				var versionId = Property(policy, "VersionId").GetGuid();
				if (policyId == Guid.Empty || versionId == Guid.Empty)
					throw new BusinessException(FinanceFeeAccountErrorCodes.InvalidAccount);
				return (policyId, versionId);
			}
			return null;
		}
		catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
		{
			throw new BusinessException(FinanceFeeAccountErrorCodes.InvalidAccount);
		}
	}

	private static JsonElement Property(JsonElement element, string name) =>
		element.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Value;

	/// <summary>Payment evidence locks the priced obligation even if an instrument is later returned.</summary>
	public void RecordPaymentEvidence()
	{
		if (AssignmentSnapshotJson == null)
			throw new BusinessException(FinanceFeeAccountErrorCodes.InvalidAccount);
		HasPaymentEvidence = true;
	}

	/// <summary>
	/// Resets a never-paid preparation. The Finance coordinator must first prove that
	/// no allocation/collection references the draft dues and remove only those drafts transactionally.
	/// </summary>
	public void ResetUnpaidPreparation()
	{
		if (HasPaymentEvidence || StudentFeeStructureId.HasValue)
			throw new BusinessException(FinanceFeeAccountErrorCodes.AssignmentConflict);
		StructureId = null;
		StructureRevision = null;
		AssignmentSnapshotJson = null;
		AssignmentRequestFingerprint = null;
	}

	/// <summary>Links the actual enrolled-student assignment without generating another due schedule.</summary>
	public void BindStudentAssignment(Guid assignmentId)
	{
		if (!StudentId.HasValue || assignmentId == Guid.Empty || AssignmentSnapshotJson == null)
			throw new BusinessException(FinanceFeeAccountErrorCodes.InvalidAccount);
		if (StudentFeeStructureId.HasValue && StudentFeeStructureId != assignmentId)
			throw new BusinessException(FinanceFeeAccountErrorCodes.AssignmentConflict);
		StudentFeeStructureId = assignmentId;
	}
}
