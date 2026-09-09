using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Finance.Fees.Concessions;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

public class FeeHeadConcessionDto
{
	public Guid FeeHeadId { get; set; }
	public int BasedOn { get; set; }
	public decimal Amount { get; set; }
}

public class CreateConcessionRequestDto
{
	public Guid StudentId { get; set; }
	public Guid ConcessionTypeId { get; set; }
	public string? Remarks { get; set; }
	public List<FeeHeadConcessionDto> FeeHeadConcessions { get; set; } = [];
	public List<UploadConcessionDocumentEntryDto> Documents { get; set; } = [];
}

public class UpdateConcessionRequestDto : CreateConcessionRequestDto;

public class FeeHeadConcessionDetailDto
{
	public Guid FeeHeadId { get; set; }
	public string FeeHeadName { get; set; } = string.Empty;
	public decimal AnnualBaseAmount { get; set; }
	public int BasedOn { get; set; }
	public decimal Amount { get; set; }
	public decimal DiscountAmount { get; set; }
	public decimal NetAmount { get; set; }
}

public class ConcessionDocumentRequirementDto
{
	public Guid DocumentId { get; set; }
	public string DocumentName { get; set; } = string.Empty;
	public bool IsRequired { get; set; }
	public long MaxFileSizeBytes { get; set; }
	public List<ShikshaMitra.Common.DocumentType> AcceptedFormats { get; set; } = [];
	public string? Url { get; set; }
	public bool IsUploaded { get; set; }
}

public class ConcessionSubmittedDocumentDto
{
	public Guid DocumentId { get; set; }
	public string DocumentName { get; set; } = string.Empty;
	public string Url { get; set; } = string.Empty;
	public int Status { get; set; }
}

public class ConcessionRequestDetailDto
{
	public Guid ConcessionRequestId { get; set; }
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public Guid ConcessionTypeId { get; set; }
	public Guid StudentFeeStructureId { get; set; }
	public Guid ConcessionPolicyId { get; set; }
	public Guid ConcessionPolicyVersionId { get; set; }
	public string ConcessionPolicyName { get; set; } = string.Empty;
	public string ConcessionPolicyCode { get; set; } = string.Empty;
	public int ConcessionPolicyVersion { get; set; }
	public string ConcessionTypeName { get; set; } = string.Empty;
	public ConcessionRequestStatus Status { get; set; }
	public string StatusName { get; set; } = string.Empty;
	public decimal TotalConcessionAmount { get; set; }
	public decimal TotalRequestedConcessionAmount { get; set; }
	public decimal TotalApprovedConcessionAmount { get; set; }
	public decimal TotalReversedConcessionAmount { get; set; }
	public decimal NetApprovedConcessionAmount { get; set; }
	public string? Remarks { get; set; }
	public Guid BoardId { get; set; }
	public Guid EducationMediumId { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
	public List<FeeHeadConcessionDetailDto> FeeHeadConcessions { get; set; } = [];
	public List<ConcessionDocumentRequirementDto> RequiredDocuments { get; set; } = [];
	public List<ConcessionSubmittedDocumentDto> SubmittedDocuments { get; set; } = [];
	public string DocumentSubmissionState { get; set; } = string.Empty;
	public List<Guid> MissingDocumentIds { get; set; } = [];
	public DateTime CreatedAt { get; set; }

	/// <summary>Alias for legacy FE field name.</summary>
	public Guid StudentConcessionTypeRequestId => ConcessionRequestId;
}

public class UploadConcessionDocumentEntryDto
{
	public Guid DocumentId { get; set; }
	public string Url { get; set; } = string.Empty;
	public int Status { get; set; }
}

public class UploadConcessionDocumentsDto
{
	public List<UploadConcessionDocumentEntryDto> Documents { get; set; } = [];
}

public class UploadConcessionDocumentFileDto
{
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public byte[] Content { get; set; } = [];
}

public class ConcessionDocumentFileUploadResponseDto
{
	public string Url { get; set; } = string.Empty;
}

public class ConcessionApplicationItemDto
{
	public Guid Id { get; set; }
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public string GrNumber { get; set; } = string.Empty;
	public string RollNo { get; set; } = string.Empty;
	public string StandardName { get; set; } = string.Empty;
	public string DivisionName { get; set; } = string.Empty;
	public string ConcessionTypeName { get; set; } = string.Empty;
	public decimal ConcessionAmount { get; set; }
	public decimal RequestedConcessionAmount { get; set; }
	public decimal ApprovedConcessionAmount { get; set; }
	public ConcessionRequestStatus Status { get; set; }
	public string StatusName { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; }
}

public class ConcessionApplicationsDto
{
	public int PendingCount { get; set; }
	public int ApprovedCount { get; set; }
	public int RejectedCount { get; set; }
	public int CancelledCount { get; set; }
	public int PartiallyReversedCount { get; set; }
	public int ReversedCount { get; set; }
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<ConcessionApplicationItemDto> Items { get; set; } = [];
}

public class DueFeeAllocationDto
{
	public Guid DueFeeId { get; set; }
	public decimal ConcessionAmount { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class FeeHeadApprovalDto
{
	public Guid FeeHeadId { get; set; }
	public decimal ApprovedConcessionAmount { get; set; }
	public List<DueFeeAllocationDto> DueFeeAllocations { get; set; } = [];
}

public class ApproveConcessionRequestDto
{
	public string? Message { get; set; }
	public string RequestConcurrencyStamp { get; set; } = string.Empty;
	public List<FeeHeadApprovalDto>? FeeHeadApprovals { get; set; }
}

public class RejectConcessionRequestDto
{
	public string Message { get; set; } = string.Empty;
}

public class DueFeeApprovalLineDto
{
	public Guid DueFeeId { get; set; }
	public Guid FeeHeadId { get; set; }
	public Guid? ConcessionDueFeeAllocationId { get; set; }
	public int Month { get; set; }
	public int Year { get; set; }
	public string MonthLabel { get; set; } = string.Empty;
	public decimal OriginalAmount { get; set; }
	public decimal PaidAmount { get; set; }
	public decimal OutstandingAmount { get; set; }
	public string Status { get; set; } = string.Empty;
	public bool IsSelectable { get; set; }
	public decimal MaxConcessionAmount { get; set; }
	public decimal AppliedConcessionAmount { get; set; }
	public decimal ReversedConcessionAmount { get; set; }
	public decimal ReversibleConcessionAmount { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class FeeHeadApprovalGroupDto
{
	public Guid FeeHeadId { get; set; }
	public string FeeHeadName { get; set; } = string.Empty;
	public decimal RequestedConcessionAmount { get; set; }
	public List<DueFeeApprovalLineDto> DueFeeLines { get; set; } = [];
}

public class ConcessionApprovalContextDto
{
	public Guid ConcessionRequestId { get; set; }
	public Guid StudentId { get; set; }
	public string ConcessionPolicyName { get; set; } = string.Empty;
	public string ConcessionPolicyCode { get; set; } = string.Empty;
	public int ConcessionPolicyVersion { get; set; }
	public string RequestConcurrencyStamp { get; set; } = string.Empty;
	public decimal TotalRequestedConcessionAmount { get; set; }
	public List<FeeHeadApprovalGroupDto> FeeHeadGroups { get; set; } = [];
}

public class ConcessionRequestPreviewInputDto
{
	public Guid StudentId { get; set; }
	public Guid ConcessionTypeId { get; set; }
	public List<FeeHeadConcessionDto> FeeHeadConcessions { get; set; } = [];
}

public class ConcessionRequestPreviewDto
{
	public Guid StudentFeeStructureId { get; set; }
	public Guid ConcessionPolicyId { get; set; }
	public Guid ConcessionPolicyVersionId { get; set; }
	public string ConcessionPolicyName { get; set; } = string.Empty;
	public string ConcessionPolicyCode { get; set; } = string.Empty;
	public int ConcessionPolicyVersion { get; set; }
	public string ApplyOn { get; set; } = string.Empty;
	public decimal TotalEligibleBase { get; set; }
	public decimal TotalConcessionAmount { get; set; }
	public decimal NetPayable { get; set; }
	public List<FeeHeadConcessionDetailDto> FeeHeadConcessions { get; set; } = [];
}

public class ApprovedDueFeeAllocationDto
{
	public Guid DueFeeId { get; set; }
	public Guid FeeHeadId { get; set; }
	public int Month { get; set; }
	public int Year { get; set; }
	public decimal ConcessionAmount { get; set; }
}

public class ConcessionApprovalResponseDto
{
	public Guid ConcessionRequestId { get; set; }
	public Guid StudentConcessionTypeRequestId { get; set; }
	public ConcessionRequestStatus Status { get; set; }
	public string StatusName { get; set; } = string.Empty;
	public DateTime ApprovedAt { get; set; }
	public Guid ApproverId { get; set; }
	public string? ApproverMessage { get; set; }
	public decimal TotalApprovedAmount { get; set; }
	public List<ApprovedDueFeeAllocationDto> DueFeeAllocations { get; set; } = [];
}

public class ConcessionRejectionResponseDto
{
	public Guid StudentConcessionTypeRequestId { get; set; }
	public ConcessionRequestStatus Status { get; set; }
	public string StatusName { get; set; } = string.Empty;
	public DateTime RejectedAt { get; set; }
	public string RejectionReason { get; set; } = string.Empty;
}

public class ConcessionCancellationResponseDto
{
	public Guid StudentConcessionTypeRequestId { get; set; }
	public ConcessionRequestStatus Status { get; set; }
	public string StatusName { get; set; } = string.Empty;
	public DateTime CancelledAt { get; set; }
}

public class ConcessionReversalLineInputDto
{
	public Guid ConcessionDueFeeAllocationId { get; set; }

	[Range(typeof(decimal), "0.01", "9999999999999999")]
	public decimal Amount { get; set; }
}

public class ReverseConcessionRequestDto
{
	[Required, MinLength(10), MaxLength(2000)]
	public string Reason { get; set; } = string.Empty;

	[Required, MinLength(1)]
	public List<ConcessionReversalLineInputDto> Lines { get; set; } = [];
}

public class ConcessionReversalLineDto
{
	public Guid ReversalId { get; set; }
	public Guid ConcessionDueFeeAllocationId { get; set; }
	public Guid DueFeeId { get; set; }
	public Guid FeeHeadId { get; set; }
	public decimal Amount { get; set; }
}

public class ConcessionReversalResponseDto
{
	public Guid ConcessionRequestId { get; set; }
	public ConcessionRequestStatus Status { get; set; }
	public string StatusName { get; set; } = string.Empty;
	public decimal TotalApprovedAmount { get; set; }
	public decimal TotalReversedAmount { get; set; }
	public decimal NetApprovedAmount { get; set; }
	public DateTime ReversedAt { get; set; }
	public Guid ReversedBy { get; set; }
	public List<ConcessionReversalLineDto> Lines { get; set; } = [];
}

public class StudentConcessionRequestItemDto
{
	public Guid StudentConcessionTypeRequestId { get; set; }
	public Guid ConcessionTypeId { get; set; }
	public string ConcessionTypeName { get; set; } = string.Empty;
	public ConcessionRequestStatus Status { get; set; }
	public decimal TotalAmount { get; set; }
	public decimal TotalRequestedAmount { get; set; }
	public decimal TotalApprovedAmount { get; set; }
	public decimal TotalReversedAmount { get; set; }
	public decimal NetApprovedAmount { get; set; }
	public List<FeeHeadConcessionDetailDto> FeeHeads { get; set; } = [];
	public List<ConcessionSubmittedDocumentDto> SubmittedDocuments { get; set; } = [];
	public List<ConcessionDocumentRequirementDto> RequiredDocuments { get; set; } = [];
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }
	public string? Message { get; set; }
	public DateTime? ApprovedAt { get; set; }
}

public class StudentBasicDetailDto
{
	public Guid StudentId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string RollNo { get; set; } = string.Empty;
	public string GrNumber { get; set; } = string.Empty;
	public string StandardName { get; set; } = string.Empty;
	public string DivisionName { get; set; } = string.Empty;
}

public class ConcessionBasicDetailDto
{
	public int TotalApplications { get; set; }
	public int PendingApplications { get; set; }
	public int ApprovedApplications { get; set; }
	public int RejectedApplications { get; set; }
}

public class StudentConcessionRequestsDto
{
	public StudentBasicDetailDto StudentDetails { get; set; } = new();
	public ConcessionBasicDetailDto ConcessionBasicDetail { get; set; } = new();
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<StudentConcessionRequestItemDto> Items { get; set; } = [];
}

public class ConcessionRequestListItemDto
{
	public Guid ConcessionRequestId { get; set; }
	public Guid StudentId { get; set; }
	public Guid ConcessionTypeId { get; set; }
	public ConcessionRequestStatus Status { get; set; }
	public DateTime CreationTime { get; set; }
}
