using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Admissions intent only. It does not create Finance assignments, dues, or allocations.</summary>
public sealed record AdmissionFeeSelectionSnapshot(
	Guid StructureId, string StructureName, int RevisionNumber, Guid AcademicYearId,
	Guid StandardId, Guid OwnerStageId, string? Currency, IReadOnlyList<AdmissionFeeSelectionHead> Heads,
	Guid SavedBy, DateTimeOffset SavedAt, Guid? ConfirmedBy = null, DateTimeOffset? OnboardingConfirmedAt = null);

public sealed record AdmissionFeeSelectionHead(
	Guid FeeHeadId, string FeeHeadName, decimal? ConfiguredAmount, bool IsOptional, bool IsOneTime,
	Guid? PaymentTermId, Guid? PaymentTermVersionId, bool IsVariable = false);
