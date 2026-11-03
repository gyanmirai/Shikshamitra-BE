using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.Concessions;

public partial class ConcessionRequestAppService
{
	private static readonly JsonSerializerOptions CommandJsonOptions = new(JsonSerializerDefaults.Web);
	private const string StagedDocumentAccessPrefix = "/api/concession-request/staged-documents/";
	private const string StagedDocumentAccessSuffix = "/file";
	private static string BuildDocumentAccessUrl(Guid requestId, Guid documentId) =>
		$"/api/concession-request/{requestId}/documents/{documentId}/file";
	private static string BuildStagedDocumentAccessUrl(Guid uploadId) =>
		$"{StagedDocumentAccessPrefix}{uploadId:D}{StagedDocumentAccessSuffix}";

	private static bool TryParseStagedDocumentAccessUrl(string url, out Guid uploadId)
	{
		uploadId = Guid.Empty;
		if (!url.StartsWith(StagedDocumentAccessPrefix, StringComparison.Ordinal) ||
			!url.EndsWith(StagedDocumentAccessSuffix, StringComparison.Ordinal))
			return false;

		var idText = url[StagedDocumentAccessPrefix.Length..
			^StagedDocumentAccessSuffix.Length];
		return Guid.TryParseExact(idText, "D", out uploadId);
	}

	private async Task EnsureAnyPermissionAsync(params string[] permissions)
	{
		foreach (var permission in permissions)
		{
			if (await AuthorizationService.IsGrantedAsync(permission)) return;
		}

		throw new Volo.Abp.Authorization.AbpAuthorizationException(
			$"Required one of the following permissions: {string.Join(", ", permissions)}");
	}

	private static string ComputeCommandHash(object payload)
	{
		var json = JsonSerializer.Serialize(payload, CommandJsonOptions);
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
	}

	private static string ComputePreviewFingerprint(
		Guid policyVersionId,
		IEnumerable<DueFee> dueFees,
		IEnumerable<ConcessionFeeHeadMapping> mappings)
	{
		var evidence = new
		{
			PolicyVersionId = policyVersionId,
			Dues = dueFees.OrderBy(x => x.Id).Select(x => new
			{
				x.Id,
				x.FeeHeadId,
				OutstandingAmount = x.OutstandingAmount.ToString("G29", CultureInfo.InvariantCulture),
				PaidAmount = x.PaidAmount.ToString("G29", CultureInfo.InvariantCulture),
				RefundedAmount = x.RefundedAmount.ToString("G29", CultureInfo.InvariantCulture),
				NetAmount = x.NetAmount.ToString("G29", CultureInfo.InvariantCulture),
				x.ConcurrencyStamp
			}),
			Mappings = mappings.OrderBy(x => x.FeeHeadId).Select(x => new
			{
				x.FeeHeadId,
				x.UserSpecifiedConcessionAmountType,
				UserSpecifiedConcessionAmountValue = x.UserSpecifiedConcessionAmountValue.ToString("G29", CultureInfo.InvariantCulture),
				FeeHeadConcessionAmount = x.FeeHeadConcessionAmount.ToString("G29", CultureInfo.InvariantCulture)
			})
		};
		return ComputeCommandHash(evidence);
	}

	private async Task<Guid?> FindCommandReplayAsync(string action, string? idempotencyKey, object payload)
	{
		if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;
		var normalizedAction = action.Trim().ToLowerInvariant();
		var normalizedKey = idempotencyKey.Trim();
		var actorId = currentUser.GetId();
		var receipt = await concessionCommandReceiptRepository.FindAsync(x =>
			x.ActorId == actorId && x.Action == normalizedAction && x.IdempotencyKey == normalizedKey);
		if (receipt is null) return null;
		if (!string.Equals(receipt.PayloadHash, ComputeCommandHash(payload), StringComparison.OrdinalIgnoreCase))
			throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.IdempotencyKeyConflict);
		return receipt.ConcessionRequestId;
	}

	private async Task RecordCommandReceiptAsync(
		string action,
		string? idempotencyKey,
		object payload,
		Guid requestId)
	{
		if (string.IsNullOrWhiteSpace(idempotencyKey)) return;
		await concessionCommandReceiptRepository.InsertAsync(new ConcessionCommandReceipt(
			guidGenerator.Create(),
			CurrentTenant.Id,
			requestId,
			currentUser.GetId(),
			action,
			idempotencyKey,
			ComputeCommandHash(payload)), autoSave: false);
	}

	private static void EnsureConcurrencyStamp(ConcessionRequest request, string concurrencyStamp)
	{
		if (string.IsNullOrWhiteSpace(concurrencyStamp))
			throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.ConcurrencyStampRequired);
		if (!string.Equals(request.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
			throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.ConcurrencyConflict);
	}

	private static void EnsurePendingDecisionStatus(ConcessionRequest request)
	{
		if (request.Status is not (ConcessionRequestStatus.Draft or ConcessionRequestStatus.Pending))
			throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.NotPending)
				.WithData("Status", request.Status.ToString());
	}

	private async Task PromoteDraftToPendingAsync(ConcessionRequest request)
	{
		if (request.Status == ConcessionRequestStatus.Pending) return;

		request.EnsureDraft();
		await concessionRequestManager.ValidateDraftApprovalAsync(request);
		await concessionRequestManager.EnsureBenefitAvailableAsync(request.StudentId, request.ConcessionTypeId, request.AcademicYearId, request.Id);

		request.Submit(Clock.Now);
	}

	private static bool IsConstraintViolation(Exception exception, string constraintName)
	{
		for (var current = exception; current is not null; current = current.InnerException)
		{
			var reportedConstraint = current.GetType().GetProperty("ConstraintName")?.GetValue(current) as string;
			if (string.Equals(reportedConstraint, constraintName, StringComparison.OrdinalIgnoreCase) ||
				current.Message.Contains(constraintName, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	private void EnsureRequestOwner(ConcessionRequest request)
	{
		if (!request.CreatorId.HasValue || currentUser.Id != request.CreatorId)
			throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.ScopeMismatch);
	}

	private async Task<bool> EnsureDecisionActorAsync(ConcessionRequest request)
	{
		if (!currentUser.Id.HasValue || !request.CreatorId.HasValue)
			throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.ApproverCannotBeRequestor)
				.WithData("Message", "Decision identity is unavailable.");
		if (currentUser.Id != request.CreatorId) return false;
		if (await userScopeService.IsAdminUserAsync(currentUser.Id.Value)) return true;
		throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.ApproverCannotBeRequestor)
			.WithData("Message", "Only a configured administrator may decide their own concession request.");
	}

	private static void EnsureRequiredDocumentsPresent(ConcessionRequest request)
	{
		IReadOnlyList<ConcessionTypeRequirementSnapshot> requirements;
		try
		{
			requirements = JsonSerializer.Deserialize<List<ConcessionTypeRequirementSnapshot>>(
				request.TypeRequirementsSnapshotJson ?? string.Empty, CommandJsonOptions) ?? [];
		}
		catch (JsonException)
		{
			throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.ConfigurationSnapshotInvalid);
		}

		var submittedIds = request.ConcessionRequestDocuments
			.Where(x => x.Status == 1 && !string.IsNullOrWhiteSpace(x.Url))
			.Select(x => x.DocumentId)
			.ToHashSet();
		var missing = requirements.Where(x => x.IsRequired && !submittedIds.Contains(x.DocumentId)).ToList();
		if (missing.Count > 0)
			throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.MissingRequiredDocuments)
				.WithData("DocumentIds", missing.Select(x => x.DocumentId).ToArray());
	}

	private static string GetDocumentSubmissionState(ConcessionRequest request)
	{
		try
		{
			var requirements = JsonSerializer.Deserialize<List<ConcessionTypeRequirementSnapshot>>(
				request.TypeRequirementsSnapshotJson ?? string.Empty, CommandJsonOptions) ?? [];
			if (requirements.Count == 0 && string.IsNullOrWhiteSpace(request.TypeRequirementsSnapshotJson))
				return request.Status == ConcessionRequestStatus.Draft ? "NotConfigured" : "NeedsReview";
			var submittedIds = request.ConcessionRequestDocuments
				.Where(x => x.Status == 1 && !string.IsNullOrWhiteSpace(x.Url))
				.Select(x => x.DocumentId)
				.ToHashSet();
			return requirements.Where(x => x.IsRequired).All(x => submittedIds.Contains(x.DocumentId))
				? "Complete" : "DocumentsPending";
		}
		catch (JsonException)
		{
			return "NeedsReview";
		}
	}

	private static List<ConcessionEligibleDueFeeDto> MapEligibleDueFees(
		IEnumerable<DueFee> dueFees,
		IReadOnlyDictionary<Guid, string> feeHeadNames,
		bool includePaid = false) => dueFees
		.OrderBy(x => x.InstallmentSequence.HasValue ? 0 : 1)
		.ThenBy(x => x.InstallmentSequence ?? int.MaxValue)
		.ThenBy(x => x.DemandDate ?? x.CoverageStartDate ?? x.ScheduleDueDate ?? x.DueDate)
		.ThenBy(x => x.Year)
		.ThenBy(x => x.Month)
		.ThenBy(x => x.Id)
		.Select(x => new ConcessionEligibleDueFeeDto
		{
			DueFeeId = x.Id,
			FeeHeadId = x.FeeHeadId,
			FeeHeadName = feeHeadNames.GetValueOrDefault(x.FeeHeadId, string.Empty),
			Month = (int)x.Month,
			Year = x.Year,
			InstallmentLabel = string.IsNullOrWhiteSpace(x.InstallmentName)
				? $"{x.Month} {x.Year}"
				: x.InstallmentName,
			OriginalAmount = x.OriginalAmount,
			PaidAmount = x.PaidAmount,
			ConcessionAmount = x.ConcessionAmount,
			AdjustmentCreditAmount = x.AdjustmentCreditAmount,
			OutstandingAmount = x.OutstandingAmount,
			EligibleConcessionAmount = includePaid
				? Math.Max(0m, x.OriginalAmount - x.NetAmount) : x.OutstandingAmount,
			Status = x.Status.ToString(),
			ConcurrencyStamp = x.ConcurrencyStamp
		}).ToList();

	private Task<FinanceAuditWriteResult> WriteRequestAuditAsync(
		ConcessionRequest request,
		string action,
		string summary,
		string? reason = null,
		object? context = null) => financeAuditWriter.WriteAsync(
		request.AcademicYearId,
		FinanceAuditEntityTypes.ConcessionRequest,
		request.Id,
		$"Concession request {request.Id:N}",
		action,
		summary,
		student: new FinanceAuditStudentContext { StudentId = request.StudentId },
		reason: reason,
		context: context,
		correlationId: $"concession-{request.Id:N}");

	private async Task<ConcessionApprovalResponseDto> BuildApprovalResponseAsync(ConcessionRequest request)
	{
		var dueFeeIds = request.ConcessionDueFeeAllocations.Select(x => x.DueFeeId).Distinct().ToList();
		var dueFees = dueFeeIds.Count == 0
			? []
			: await dueFeeRepository.GetListAsync(x => dueFeeIds.Contains(x.Id));
		var dueFeeMap = dueFees.ToDictionary(x => x.Id);
		var refunds = await feeRefundRepository.GetListAsync(x => x.ConcessionRequestId == request.Id);
		return new ConcessionApprovalResponseDto
		{
			ConcessionRequestId = request.Id,
			StudentConcessionTypeRequestId = request.Id,
			Status = request.Status,
			StatusName = request.Status.ToString(),
			ApprovedAt = request.GetApprovedOn() ?? request.LastModificationTime ?? request.CreationTime,
			ApproverId = request.GetApprovedBy() ?? Guid.Empty,
			ApproverMessage = request.ApproverMessage,
			TotalApprovedAmount = request.GetTotalApprovedConcession(),
			RefundRequests = refunds.Select(x => new ConcessionRefundReferenceDto
			{
				RefundRequestId = x.Id, VoucherNumber = x.VoucherNumber,
				OriginalReceiptId = x.OriginalReceiptId!.Value,
				Amount = x.CalculatedAmount, Status = x.Status.ToString()
			}).ToList(),
			DueFeeAllocations = request.ConcessionDueFeeAllocations.Select(x =>
			{
				dueFeeMap.TryGetValue(x.DueFeeId, out var dueFee);
				return new ApprovedDueFeeAllocationDto
				{
					DueFeeId = x.DueFeeId,
					FeeHeadId = x.FeeHeadId,
					Month = dueFee is null ? 0 : (int)dueFee.Month,
					Year = dueFee?.Year ?? 0,
					ConcessionAmount = x.ConcessionAmount,
				};
			}).ToList(),
		};
	}

	private static ConcessionRejectionResponseDto BuildRejectionResponse(ConcessionRequest request) => new()
	{
		StudentConcessionTypeRequestId = request.Id,
		Status = request.Status,
		StatusName = request.Status.ToString(),
		RejectedAt = request.GetApprovedOn() ?? request.LastModificationTime ?? request.CreationTime,
		RejectionReason = request.RejectionReason ?? string.Empty,
	};

	private static ConcessionCancellationResponseDto BuildCancellationResponse(ConcessionRequest request) => new()
	{
		StudentConcessionTypeRequestId = request.Id,
		Status = request.Status,
		StatusName = request.Status.ToString(),
		CancelledAt = request.CancelledOn ?? request.LastModificationTime ?? request.CreationTime,
	};

	private static ConcessionReversalResponseDto BuildReversalResponse(
		ConcessionRequest request,
		DateTime reversedAt,
		Guid reversedBy,
		IEnumerable<ConcessionReversal> reversals) => new()
	{
		ConcessionRequestId = request.Id,
		Status = request.Status,
		StatusName = request.Status.ToString(),
		TotalApprovedAmount = request.GetTotalApprovedConcession(),
		TotalReversedAmount = request.GetTotalReversedConcession(),
		NetApprovedAmount = request.GetNetApprovedConcession(),
		ReversedAt = reversedAt,
		ReversedBy = reversedBy,
		Lines = reversals.Select(x => new ConcessionReversalLineDto
		{
			ReversalId = x.Id,
			ConcessionDueFeeAllocationId = x.ConcessionDueFeeAllocationId,
			DueFeeId = x.DueFeeId,
			FeeHeadId = x.FeeHeadId,
			Amount = x.Amount,
		}).ToList(),
	};

    private static List<FeeHeadConcessionInput> MapFeeHeadInputs(IEnumerable<FeeHeadConcessionDto> items) =>
        items.Select(i => new FeeHeadConcessionInput(i.FeeHeadId, (DiscountBasis)i.BasedOn, i.Amount)).ToList();

    private static List<DueFeeConcessionInput> MapDueFeeInputs(IEnumerable<DueFeeConcessionDto> items) =>
        items.Select(i => new DueFeeConcessionInput(i.DueFeeId, i.FeeHeadId, i.Amount)).ToList();

    private async Task<ConcessionRequestDetailDto> MapDetailAsync(ConcessionRequest request)
    {
		var isAdministrator = currentUser.Id.HasValue && await userScopeService.IsAdminUserAsync(currentUser.Id.Value);
		var canViewDocuments = await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.ViewDocument);
		var canViewRefunds = await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View);
		var linkedRefunds = canViewRefunds
			? await feeRefundRepository.GetListAsync(x => x.ConcessionRequestId == request.Id)
			: [];
        var student = await studentRepository.GetAsync(request.StudentId);
        var type = await concessionTypeRepository.GetAsync(request.ConcessionTypeId);
        var feeHeadIds = request.ConcessionFeeHeadMappings.Select(m => m.FeeHeadId).ToList();
        var feeHeads = feeHeadIds.Count > 0
            ? await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id)) : [];
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);
        var policyVersion = await financePolicyVersionRepository.GetAsync(request.ConcessionPolicyVersionId);
        var policy = JsonSerializer.Deserialize<ConcessionPolicySnapshot>(policyVersion.SnapshotJson)
            ?? throw new Volo.Abp.BusinessException(ConcessionPolicyErrorCodes.PolicyVersionNotFound);

		List<ConcessionTypeRequirementSnapshot> requirementSnapshot;
		try
		{
			requirementSnapshot = string.IsNullOrWhiteSpace(request.TypeRequirementsSnapshotJson) &&
				!request.SubmittedOn.HasValue && request.Status is
					(ConcessionRequestStatus.Draft or ConcessionRequestStatus.Rejected or ConcessionRequestStatus.Cancelled)
				? type.ConcessionTypeDocuments.Select(d => new ConcessionTypeRequirementSnapshot(
					d.Id, d.DocumentName, d.IsRequired, d.MaxFileSizeBytes,
					d.AcceptedFormats.Select(format => format.DocumentType).ToList())).ToList()
				: JsonSerializer.Deserialize<List<ConcessionTypeRequirementSnapshot>>(
					request.TypeRequirementsSnapshotJson ?? string.Empty, CommandJsonOptions) ?? [];
		}
		catch (JsonException)
		{
			throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.ConfigurationSnapshotInvalid);
		}
		if (requirementSnapshot.Count == 0 && request.Status == ConcessionRequestStatus.Draft)
		{
			requirementSnapshot = type.ConcessionTypeDocuments.Select(d => new ConcessionTypeRequirementSnapshot(
				d.Id, d.DocumentName, d.IsRequired, d.MaxFileSizeBytes,
				d.AcceptedFormats.Select(format => format.DocumentType).ToList())).ToList();
		}

		var requiredDocs = requirementSnapshot.Select(d => new ConcessionDocumentRequirementDto
		{
			DocumentId = d.DocumentId,
			DocumentName = d.DocumentName,
			IsRequired = d.IsRequired,
			MaxFileSizeBytes = d.MaxFileSizeBytes,
			AcceptedFormats = d.AcceptedFormats.ToList(),
			IsUploaded = request.ConcessionRequestDocuments.Any(rd =>
				rd.DocumentId == d.DocumentId && rd.Status == 1 && !string.IsNullOrWhiteSpace(rd.Url)),
			Url = canViewDocuments && request.ConcessionRequestDocuments.Any(rd =>
				rd.DocumentId == d.DocumentId && rd.Status == 1 && !string.IsNullOrWhiteSpace(rd.Url))
				? BuildDocumentAccessUrl(request.Id, d.DocumentId)
				: null,
		}).ToList();

        return new ConcessionRequestDetailDto
        {
			ConcessionRequestId = request.Id,
			AcademicYearId = request.AcademicYearId,
			StudentId = request.StudentId,
            StudentFeeStructureId = request.StudentFeeStructureId,
            ConcessionPolicyId = request.ConcessionPolicyId,
            ConcessionPolicyVersionId = request.ConcessionPolicyVersionId,
            ConcessionPolicyName = policy.PolicyName,
            ConcessionPolicyCode = policy.PolicyCode,
            ConcessionPolicyVersion = policy.Version,
			AutoCreateRefund = policy.ShouldAutoCreateRefund,
			PaidConcessionAmount = request.GetRemainingPaidConcessionAmounts().Values.Sum(),
			CanCreateRefundRequests = !policy.ShouldAutoCreateRefund && !request.HaveRefundRequestsBeenCreated() &&
				request.Status is ConcessionRequestStatus.Approved or ConcessionRequestStatus.PartiallyReversed &&
				request.GetRemainingPaidConcessionAmounts().Values.Sum() > 0m,
            StudentName = FormatStudentName(student), ConcessionTypeId = request.ConcessionTypeId,
			ConcessionTypeName = type.Name, Status = request.Status, StatusName = request.Status.ToString(),
			ConcurrencyStamp = request.ConcurrencyStamp,
			RequesterId = request.CreatorId,
			SubmittedById = request.SubmittedOn.HasValue ? request.CreatorId : null,
			DecidedById = request.GetApprovedBy(),
			SourceRequestId = request.SourceRequestId,
			SubmittedAt = request.SubmittedOn,
			CancellationReason = request.CancellationReason,
            TotalConcessionAmount = request.GetTotalRequestedConcession(),
            TotalRequestedConcessionAmount = request.GetTotalRequestedConcession(),
			TotalApprovedConcessionAmount = request.GetTotalApprovedConcession(),
			TotalReversedConcessionAmount = request.GetTotalReversedConcession(),
			NetApprovedConcessionAmount = request.GetNetApprovedConcession(),
			RefundRequests = linkedRefunds.Select(x => new ConcessionRefundReferenceDto
			{
				RefundRequestId = x.Id, VoucherNumber = x.VoucherNumber,
				OriginalReceiptId = x.OriginalReceiptId!.Value,
				Amount = x.CalculatedAmount, Status = x.Status.ToString()
			}).ToList(),
            Remarks = request.Remarks,
            BoardId = request.GetBoardId(), EducationMediumId = request.GetEducationMediumId(),
            StandardId = request.GetStandardId(), DivisionId = request.GetDivisionId(),
            FeeHeadConcessions = request.ConcessionFeeHeadMappings.Select(m => new FeeHeadConcessionDetailDto
            {
                FeeHeadId = m.FeeHeadId, FeeHeadName = feeHeadMap.GetValueOrDefault(m.FeeHeadId, string.Empty),
                AnnualBaseAmount = m.PreConcessionFeeHeadAmount, BasedOn = (int)m.UserSpecifiedConcessionAmountType,
                Amount = m.UserSpecifiedConcessionAmountValue, DiscountAmount = m.FeeHeadConcessionAmount,
                NetAmount = m.PostConcessionFeeHeadAmount,
            }).ToList(),
            DueFeeConcessions = ConcessionRequestManager.ReadDueFeeEvidence(request)
                .Select(x => new DueFeeConcessionDto
                {
                    DueFeeId = x.DueFeeId, FeeHeadId = x.FeeHeadId, Amount = x.Amount,
                }).ToList(),
            RequiredDocuments = requiredDocs,
            SubmittedDocuments = request.ConcessionRequestDocuments.Select(d => new ConcessionSubmittedDocumentDto
            {
                DocumentId = d.DocumentId,
				DocumentName = requirementSnapshot.FirstOrDefault(ct => ct.DocumentId == d.DocumentId)?.DocumentName ?? string.Empty,
				Url = canViewDocuments && d.Status == 1 && !string.IsNullOrWhiteSpace(d.Url)
					? BuildDocumentAccessUrl(request.Id, d.DocumentId) : string.Empty,
				Status = d.Status,
            }).ToList(),
			DocumentSubmissionState = requiredDocs.Where(x => x.IsRequired).All(x => x.IsUploaded)
				? "Complete" : "DocumentsPending",
			MissingDocumentIds = requiredDocs.Where(x => x.IsRequired && !x.IsUploaded)
				.Select(x => x.DocumentId).ToList(),
            CreatedAt = request.CreationTime,
			AvailableActions = BuildAvailableActions(request, isAdministrator),
			Timeline = BuildTimeline(request),
        };
    }

	private List<string> BuildAvailableActions(ConcessionRequest request, bool isAdministrator)
	{
		var isOwner = request.CreatorId.HasValue && request.CreatorId == currentUser.Id;
		var canDecide = request.CreatorId.HasValue && currentUser.Id.HasValue && (!isOwner || isAdministrator);
		return request.Status switch
		{
			ConcessionRequestStatus.Draft =>
				isOwner && canDecide ? ["view", "edit", "delete", "upload-document", "approve", "reject"] :
				canDecide ? ["view", "approve", "reject"] :
				isOwner ? ["view", "edit", "delete", "upload-document"] : ["view"],
			ConcessionRequestStatus.Pending =>
				canDecide ? ["view", "approve", "reject"] : ["view"],
			ConcessionRequestStatus.Rejected or ConcessionRequestStatus.Cancelled when isOwner =>
				["view", "create-replacement"],
			ConcessionRequestStatus.Approved or ConcessionRequestStatus.PartiallyReversed =>
				["view", "reverse"],
			_ => ["view"],
		};
	}

	private static List<ConcessionTimelineEventDto> BuildTimeline(ConcessionRequest request)
	{
		var timeline = new List<ConcessionTimelineEventDto>
		{
			new()
			{
				Action = "created",
				Status = ConcessionRequestStatus.Draft,
				ActorId = request.CreatorId,
				OccurredAt = request.CreationTime,
			},
		};
		if (request.SubmittedOn.HasValue)
			timeline.Add(new ConcessionTimelineEventDto
			{
				Action = "submitted",
				Status = ConcessionRequestStatus.Pending,
				ActorId = request.CreatorId,
				OccurredAt = request.SubmittedOn.Value,
			});
		if (request.GetApprovedOn().HasValue && request.Status is
			ConcessionRequestStatus.Approved or ConcessionRequestStatus.PartiallyReversed or
			ConcessionRequestStatus.Reversed or ConcessionRequestStatus.Rejected)
			timeline.Add(new ConcessionTimelineEventDto
			{
				Action = request.Status == ConcessionRequestStatus.Rejected ? "rejected" : "approved",
				Status = request.Status == ConcessionRequestStatus.Rejected
					? ConcessionRequestStatus.Rejected
					: ConcessionRequestStatus.Approved,
				ActorId = request.GetApprovedBy(),
				OccurredAt = request.GetApprovedOn()!.Value,
				Reason = request.Status == ConcessionRequestStatus.Rejected
					? request.RejectionReason : request.ApproverMessage,
				Amount = request.Status == ConcessionRequestStatus.Rejected
					? null : request.GetTotalApprovedConcession(),
			});
		if (request.CancelledOn.HasValue)
			timeline.Add(new ConcessionTimelineEventDto
			{
				Action = "cancelled",
				Status = ConcessionRequestStatus.Cancelled,
				ActorId = request.CancelledBy,
				OccurredAt = request.CancelledOn.Value,
				Reason = request.CancellationReason,
			});
		timeline.AddRange(request.ConcessionReversals.Select(reversal => new ConcessionTimelineEventDto
		{
			Action = "reversed",
			Status = request.Status,
			ActorId = reversal.ReversedBy,
			OccurredAt = reversal.ReversedAt,
			Reason = reversal.Reason,
			Amount = reversal.Amount,
		}));
		return timeline.OrderBy(x => x.OccurredAt).ToList();
	}

	private async Task<List<ConcessionRequestDocument>> ValidateAndBuildDocumentsAsync(
		ConcessionRequest request,
		IEnumerable<UploadConcessionDocumentEntryDto> entries)
	{
		var submitted = entries.ToList();
		if (submitted.Select(x => x.DocumentId).Distinct().Count() != submitted.Count)
			throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);

		var type = await concessionTypeRepository.GetAsync(request.ConcessionTypeId);
		var requirements = type.ConcessionTypeDocuments.ToDictionary(x => x.Id);
		var result = new List<ConcessionRequestDocument>(submitted.Count);

		foreach (var entry in submitted)
		{
			if (entry.Status != 1 || string.IsNullOrWhiteSpace(entry.Url)
				|| !requirements.TryGetValue(entry.DocumentId, out var requirement))
				throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);

			const string fileUrlPrefix = "/api/files/";
			var fileUrl = entry.Url.Trim();
			var protectedUrl = BuildDocumentAccessUrl(request.Id, entry.DocumentId);
			if (string.Equals(fileUrl, protectedUrl, StringComparison.Ordinal))
			{
				var existing = request.ConcessionRequestDocuments.FirstOrDefault(x =>
					x.DocumentId == entry.DocumentId && x.Status == 1 && !string.IsNullOrWhiteSpace(x.Url))
					?? throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);
				result.Add(new ConcessionRequestDocument(
					guidGenerator.Create(), CurrentTenant.Id, request.Id,
					entry.DocumentId, existing.Url, entry.Status));
				continue;
			}
			if (!TryParseStagedDocumentAccessUrl(fileUrl, out var uploadId))
				throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);

			var upload = await concessionDocumentUploadRepository.FindAsync(uploadId)
				?? throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.DocumentOwnershipMismatch);
			if (!upload.Url.StartsWith(fileUrlPrefix, StringComparison.Ordinal) ||
				upload.Url.Length == fileUrlPrefix.Length)
				throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);
			var storedFileName = Uri.UnescapeDataString(upload.Url[fileUrlPrefix.Length..]);
			var blob = await fileStorageAppService.GetAsync(storedFileName);
			var extension = Path.GetExtension(blob.FileName).ToLowerInvariant();
			var documentType = extension switch
			{
				".jpg" => ShikshaMitra.Common.DocumentType.Jpg,
				".jpeg" => ShikshaMitra.Common.DocumentType.Jpeg,
				".png" => ShikshaMitra.Common.DocumentType.Png,
				".docx" => ShikshaMitra.Common.DocumentType.Docx,
				".pdf" => ShikshaMitra.Common.DocumentType.Pdf,
				".xlsx" => ShikshaMitra.Common.DocumentType.Xlsx,
				_ => throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission),
			};

			if (!requirement.AcceptedFormats.Any(x => x.DocumentType == documentType)
				|| blob.Content.LongLength > requirement.MaxFileSizeBytes)
				throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission)
					.WithData("DocumentId", requirement.Id);

			try
			{
				FileValidationService.ValidateDocumentFileSignature(blob.Content, extension);
			}
			catch (Volo.Abp.BusinessException)
			{
				throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission)
					.WithData("DocumentId", requirement.Id);
			}

			upload.Attach(
				request.Id,
				currentUser.GetId(),
				request.ConcessionTypeId,
				entry.DocumentId,
				Clock.Now);
			await concessionDocumentUploadRepository.UpdateAsync(upload, autoSave: false);

			result.Add(new ConcessionRequestDocument(
				guidGenerator.Create(), CurrentTenant.Id, request.Id,
				entry.DocumentId, upload.Url, entry.Status));
		}

		return result;
	}

	private async Task<List<ConcessionRequestDocument>> PreserveHiddenDocumentsAsync(
		ConcessionRequest request,
		List<ConcessionRequestDocument> replacements)
	{
		if (await AuthorizationService.IsGrantedAsync(
			ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.ViewDocument))
			return replacements;

		var replacedIds = replacements.Select(x => x.DocumentId).ToHashSet();
		replacements.AddRange(request.ConcessionRequestDocuments
			.Where(x => x.Status == 1 && !string.IsNullOrWhiteSpace(x.Url) && !replacedIds.Contains(x.DocumentId))
			.Select(x => new ConcessionRequestDocument(
				guidGenerator.Create(), CurrentTenant.Id, request.Id, x.DocumentId, x.Url, x.Status)));
		return replacements;
	}

	private async Task ReleaseRemovedDocumentUploadsAsync(
		ConcessionRequest request,
		IReadOnlyCollection<ConcessionRequestDocument> replacements)
	{
		request.EnsureDraft();
		var retainedUrls = replacements.Where(x => !string.IsNullOrWhiteSpace(x.Url))
			.Select(x => x.Url).ToHashSet(StringComparer.Ordinal);
		var removedUrls = request.ConcessionRequestDocuments
			.Where(x => !string.IsNullOrWhiteSpace(x.Url) && !retainedUrls.Contains(x.Url))
			.Select(x => x.Url)
			.Distinct(StringComparer.Ordinal)
			.ToList();
		if (removedUrls.Count == 0) return;

		var uploads = await concessionDocumentUploadRepository.GetListAsync(x =>
			x.ConcessionRequestId == request.Id && removedUrls.Contains(x.Url));
		foreach (var upload in uploads)
		{
			upload.ReleaseFromDraft(request.Id, Clock.Now);
			await concessionDocumentUploadRepository.UpdateAsync(upload, autoSave: false);
		}
	}

    private StudentConcessionRequestItemDto MapStudentItem(
        ConcessionRequest request, ConcessionType? type, Dictionary<Guid, string> feeHeadMap,
		bool canViewDocuments, IReadOnlyList<ConcessionRefundReferenceDto> refundRequests, bool isAdministrator)
    {
		List<ConcessionTypeRequirementSnapshot> requirements;
		try
		{
			requirements = string.IsNullOrWhiteSpace(request.TypeRequirementsSnapshotJson)
				? type?.ConcessionTypeDocuments.Select(d => new ConcessionTypeRequirementSnapshot(
					d.Id, d.DocumentName, d.IsRequired, d.MaxFileSizeBytes,
					d.AcceptedFormats.Select(format => format.DocumentType).ToList())).ToList() ?? []
				: JsonSerializer.Deserialize<List<ConcessionTypeRequirementSnapshot>>(
					request.TypeRequirementsSnapshotJson, CommandJsonOptions) ?? [];
		}
		catch (JsonException)
		{
			throw new Volo.Abp.BusinessException(FinanceConcessionErrorCodes.ConfigurationSnapshotInvalid);
		}

        return new StudentConcessionRequestItemDto
        {
            StudentConcessionTypeRequestId = request.Id, ConcessionTypeId = request.ConcessionTypeId,
            RequesterId = request.CreatorId,
            ConcessionTypeName = type?.Name ?? string.Empty, Status = request.Status,
            TotalAmount = request.GetTotalRequestedConcession(),
            TotalRequestedAmount = request.GetTotalRequestedConcession(),
            TotalApprovedAmount = request.GetTotalApprovedConcession(),
            TotalReversedAmount = request.GetTotalReversedConcession(),
            NetApprovedAmount = request.GetNetApprovedConcession(),
			RefundRequests = refundRequests.ToList(),
			AutoCreateRefund = request.ShouldAutoCreateRefund(),
			PaidConcessionAmount = request.GetRemainingPaidConcessionAmounts().Values.Sum(),
			CanCreateRefundRequests = !request.ShouldAutoCreateRefund() && !request.HaveRefundRequestsBeenCreated() &&
				request.Status is ConcessionRequestStatus.Approved or ConcessionRequestStatus.PartiallyReversed &&
				request.GetRemainingPaidConcessionAmounts().Values.Sum() > 0m,
            FeeHeads = request.ConcessionFeeHeadMappings.Select(m => new FeeHeadConcessionDetailDto
            {
                FeeHeadId = m.FeeHeadId, FeeHeadName = feeHeadMap.GetValueOrDefault(m.FeeHeadId, string.Empty),
                AnnualBaseAmount = m.PreConcessionFeeHeadAmount, BasedOn = (int)m.UserSpecifiedConcessionAmountType,
                Amount = m.UserSpecifiedConcessionAmountValue, DiscountAmount = m.FeeHeadConcessionAmount,
                NetAmount = m.PostConcessionFeeHeadAmount,
            }).ToList(),
            RequiredDocuments = requirements.Select(d => new ConcessionDocumentRequirementDto
            {
                DocumentId = d.DocumentId, DocumentName = d.DocumentName,
				IsRequired = d.IsRequired,
				MaxFileSizeBytes = d.MaxFileSizeBytes,
				AcceptedFormats = d.AcceptedFormats.ToList(),
				IsUploaded = request.ConcessionRequestDocuments.Any(rd => rd.DocumentId == d.DocumentId),
				Url = canViewDocuments && request.ConcessionRequestDocuments.Any(rd =>
					rd.DocumentId == d.DocumentId && rd.Status == 1 && !string.IsNullOrWhiteSpace(rd.Url))
					? BuildDocumentAccessUrl(request.Id, d.DocumentId)
					: null
			}).ToList(),
            CreatedAt = request.CreationTime, UpdatedAt = request.LastModificationTime,
            Message = request.ApproverMessage ?? request.RejectionReason,
			ApprovedAt = request.GetApprovedOn(),
			ConcurrencyStamp = request.ConcurrencyStamp,
			AvailableActions = BuildAvailableActions(request, isAdministrator),
			Timeline = BuildTimeline(request),
        };
    }

    private static string FormatStudentName(Student? student) =>
        student == null ? string.Empty
            : string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

    private static string FormatStudentGrNumber(Student? student) =>
        !string.IsNullOrWhiteSpace(student?.GrNumber)
            ? student.GrNumber
            : student?.GrNo?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

}
