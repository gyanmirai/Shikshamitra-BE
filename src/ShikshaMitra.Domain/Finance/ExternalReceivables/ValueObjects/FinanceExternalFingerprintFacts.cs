using System;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed record FinanceExternalFingerprintFacts(Guid TenantId, FinanceExternalSourceKind Kind,
	Guid SourceId, string SourceVersion, Guid AcademicYearId, bool AcademicYearConfirmed,
	string AcademicYearEvidenceJson, FinanceExternalPayerKind PayerKind, Guid? StudentId, Guid? EmployeeId,
	decimal Amount, string Currency, DateOnly AssessmentDate, DateOnly DueDate,
	bool SchoolDatesConfirmed, string Reason, string EvidenceReference);
