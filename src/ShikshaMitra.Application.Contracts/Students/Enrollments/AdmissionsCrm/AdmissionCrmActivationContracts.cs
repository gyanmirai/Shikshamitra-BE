using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class CreateAdmissionCrmNumberSeriesDto
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmIdentifierType IdentifierType { get; set; }
	public Guid? EducationBoardId { get; set; }
	public Guid? AcademicMediumId { get; set; }
	public Guid? StandardId { get; set; }
	public string Prefix { get; set; } = string.Empty;
	public string Suffix { get; set; } = string.Empty;
	public int Padding { get; set; } = 6;
	public long StartingSequence { get; set; } = 1;
	public long? MaximumSequence { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveUntil { get; set; }
}

public class AdmissionCrmNumberSeriesDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public AdmissionCrmIdentifierType IdentifierType { get; set; }
	public Guid? EducationBoardId { get; set; }
	public Guid? AcademicMediumId { get; set; }
	public Guid? StandardId { get; set; }
	public string Prefix { get; set; } = string.Empty;
	public string Suffix { get; set; } = string.Empty;
	public int Padding { get; set; }
	public long StartingSequence { get; set; }
	public long NextSequence { get; set; }
	public long? MaximumSequence { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveUntil { get; set; }
	public AdmissionCrmNumberSeriesStatus Status { get; set; }
	public string NextPreview { get; set; } = string.Empty;
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AdmissionCrmPlacementOptionDto
{
	public Guid EducationBoardId { get; set; }
	public string BoardName { get; set; } = string.Empty;
	public Guid AcademicMediumId { get; set; }
	public string MediumName { get; set; } = string.Empty;
	public Guid AcademicMediumStageId { get; set; }
	public string StageName { get; set; } = string.Empty;
	public Guid StandardId { get; set; }
	public string StandardName { get; set; } = string.Empty;
	public Guid DivisionId { get; set; }
	public string DivisionName { get; set; } = string.Empty;
	public int DivisionStrength { get; set; }
}

public class AdmissionCrmActivationReadinessItemDto
{
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public AdmissionCrmActivationStepStatus Status { get; set; }
	public bool Blocking { get; set; }
	public string Detail { get; set; } = string.Empty;
}

public class PreviewAdmissionCrmActivationDto
{
	public Guid EducationBoardId { get; set; }
	public Guid AcademicMediumId { get; set; }
	public Guid AcademicMediumStageId { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
	public Guid GrNumberSeriesId { get; set; }
	public int? RollNumber { get; set; }
	public bool RequireStudentAccount { get; set; } = true;
	public bool RequireGuardianAccount { get; set; } = true;
	public IReadOnlyList<AdmissionCrmServiceHandoffRequestDto> ServiceHandoffs { get; set; } = [];
}

public class AdmissionCrmServiceHandoffRequestDto
{
	public AdmissionCrmServiceHandoffType Type { get; set; }
	public string DestinationModule { get; set; } = string.Empty;
	public bool ConsentConfirmed { get; set; }
	public IReadOnlyDictionary<string, string?> Payload { get; set; } = new Dictionary<string, string?>();
}

public class AdmissionCrmActivationPreviewDto
{
	public Guid LeadId { get; set; }
	public IReadOnlyList<AdmissionCrmActivationReadinessItemDto> Items { get; set; } = [];
	public bool IsReady { get; set; }
	public string? ExistingStudentId { get; set; }
	public string GrNumberPreview { get; set; } = string.Empty;
	public int DivisionStrength { get; set; }
	public int DivisionOccupancy { get; set; }
}

public class RequestAdmissionCrmActivationDto : PreviewAdmissionCrmActivationDto
{
	public ShikshaMitra.Students.GenderEnum Gender { get; set; }
}

public class AdmissionCrmActivationStepDto
{
	public string Key { get; set; } = string.Empty;
	public AdmissionCrmActivationStepStatus Status { get; set; }
	public string Detail { get; set; } = string.Empty;
	public DateTime UpdatedAt { get; set; }
}

public class AdmissionCrmIdentifierAllocationDto : EntityDto<Guid>
{
	public AdmissionCrmIdentifierType IdentifierType { get; set; }
	public string Value { get; set; } = string.Empty;
	public long Sequence { get; set; }
	public AdmissionCrmIdentifierStatus Status { get; set; }
	public DateTime AllocatedAt { get; set; }
	public DateTime? CommittedAt { get; set; }
	public bool IsManual { get; set; }
	public Guid? AssignedByUserId { get; set; }
	public string? LifecycleReason { get; set; }
}

public class AssignManualAdmissionCrmIdentifierDto
{
	public Guid NumberSeriesId { get; set; }
	public long Sequence { get; set; }
	public string Value { get; set; } = string.Empty;
	public string Reason { get; set; } = string.Empty;
}

public class AdmissionCrmIdentifierGovernanceReportDto
{
	public Guid AcademicYearId { get; set; }
	public Guid NumberSeriesId { get; set; }
	public string SeriesCode { get; set; } = string.Empty;
	public long StartingSequence { get; set; }
	public long NextSequence { get; set; }
	public long? MaximumSequence { get; set; }
	public int AllocatedCount { get; set; }
	public int CommittedCount { get; set; }
	public int ManualCount { get; set; }
	public int VoidedCount { get; set; }
	public bool IsExhausted { get; set; }
	public IReadOnlyList<long> GapSequences { get; set; } = [];
	public bool GapReportTruncated { get; set; }
	public IReadOnlyList<AdmissionCrmIdentifierAllocationDto> Allocations { get; set; } = [];
}

public class AdmissionCrmActivationDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public Guid? StudentId { get; set; }
	public string ActivationReference { get; set; } = string.Empty;
	public AdmissionCrmActivationStatus Status { get; set; }
	public Guid EducationBoardId { get; set; }
	public Guid AcademicMediumId { get; set; }
	public Guid AcademicMediumStageId { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
	public Guid GrNumberSeriesId { get; set; }
	public int? RollNumber { get; set; }
	public ShikshaMitra.Students.GenderEnum Gender { get; set; }
	public bool RequireStudentAccount { get; set; }
	public bool RequireGuardianAccount { get; set; }
	public IReadOnlyList<AdmissionCrmActivationStepDto> Steps { get; set; } = [];
	public IReadOnlyList<AdmissionCrmIdentifierAllocationDto> Identifiers { get; set; } = [];
	public int AttemptCount { get; set; }
	public string? RepairStep { get; set; }
	public string? RepairReason { get; set; }
	public DateTime? FirstDayReadyAt { get; set; }
	public Guid? CancellationRequestedByUserId { get; set; }
	public DateTime? CancellationRequestedAt { get; set; }
	public string? CancellationReason { get; set; }
	public Guid? CancelledByUserId { get; set; }
	public DateTime? CancelledAt { get; set; }
	public IReadOnlyList<AdmissionCrmServiceHandoffDto> ServiceHandoffs { get; set; } = [];
}

public class AdmissionCrmServiceHandoffDto : EntityDto<Guid>
{
	public Guid ActivationId { get; set; }
	public Guid LeadId { get; set; }
	public Guid StudentId { get; set; }
	public AdmissionCrmServiceHandoffType Type { get; set; }
	public string DestinationModule { get; set; } = string.Empty;
	public bool ConsentConfirmed { get; set; }
	public IReadOnlyDictionary<string, string?> Payload { get; set; } = new Dictionary<string, string?>();
	public AdmissionCrmServiceHandoffStatus Status { get; set; }
	public int AttemptCount { get; set; }
	public DateTime RequestedAt { get; set; }
	public DateTime? DispatchedAt { get; set; }
	public DateTime? AcknowledgedAt { get; set; }
	public string? ExternalReference { get; set; }
	public string? LastError { get; set; }
}

public class UpdateAdmissionCrmServiceHandoffDto
{
	public AdmissionCrmServiceHandoffStatus Status { get; set; }
	public string? ExternalReference { get; set; }
	public string? Reason { get; set; }
}

public class AdmissionCrmActivationCancellationCheckDto
{
	public string Key { get; set; } = string.Empty;
	public bool Passed { get; set; }
	public string Detail { get; set; } = string.Empty;
}

public class AdmissionCrmActivationCancellationPreviewDto
{
	public Guid ActivationId { get; set; }
	public bool CanCancel { get; set; }
	public DateTime? CancellationWindowEndsAt { get; set; }
	public IReadOnlyList<AdmissionCrmActivationCancellationCheckDto> Checks { get; set; } = [];
}

public class RequestAdmissionCrmActivationCancellationDto
{
	public string Reason { get; set; } = string.Empty;
}
