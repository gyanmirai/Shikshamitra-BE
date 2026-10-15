using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>Student-owned facts; Finance owns every subsequent price, obligation and payment.</summary>
public interface IAdmissionFinanceContextReader
{
	Task<AdmissionFinanceContext> GetAsync(Guid applicationId);
	Task RecordPaymentEvidenceAsync(Guid applicationId, Guid accountId);
}

public sealed record AdmissionFinanceContext(
	Guid ApplicationId, Guid ApplicantId, Guid AcademicYearId, Guid BoardId,
	Guid MediumId, Guid StageId, Guid StandardId, Guid? DivisionId, Guid? StudentId,
	string ApplicantName, string ApplicationNumber, string StandardName, string SourceRevision,
	Guid? StructureId, int? StructureRevision, IReadOnlyList<Guid> OptionalHeadIds,
	bool CanCollect, string? Blocker, Guid? EnrollmentId);
