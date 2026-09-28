using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.Common.Files;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Uow;
using System.Text.Json;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.Concessions;

public partial class ConcessionRequestAppService
{
	private List<ConcessionDueFeeAllocation> BuildSubmittedApprovalAllocations(
		ConcessionRequest request,
		List<DueFee> dueFees,
		ConcessionPolicySnapshot policy,
		IReadOnlyList<ConcessionFeeHeadMapping> liveMappings)
	{
		foreach (var requested in request.ConcessionFeeHeadMappings)
		{
			var live = liveMappings.FirstOrDefault(x => x.FeeHeadId == requested.FeeHeadId);
			if (live is null || live.FeeHeadConcessionAmount < requested.FeeHeadConcessionAmount)
				throw new BusinessException(FinanceConcessionErrorCodes.PreviewChanged)
					.WithData("FeeHeadId", requested.FeeHeadId);
		}
		if (ConcessionRequestManager.ReadDueFeeEvidence(request).Count > 0)
			return concessionRequestManager.BuildSubmittedDueFeeAllocations(request, dueFees, policy);

		var approvals = concessionRequestManager.BuildAutoApproveAllocations(request, dueFees, policy);
		if (approvals.Count != request.ConcessionFeeHeadMappings.Count ||
			approvals.Sum(x => x.ApprovedConcessionAmount) != request.GetTotalRequestedConcession())
			throw new BusinessException(FinanceConcessionErrorCodes.PreviewChanged);
		return concessionRequestManager.BuildDueFeeAllocations(
			CurrentTenant.Id, request.Id, policy, approvals, dueFees,
			request.ConcessionFeeHeadMappings.ToList(), liveMappings);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Submit)]
	public async Task<ApiDataResponseDto<ConcessionRequestDetailDto>> CreateAsync(
		Guid academicYearId, CreateConcessionRequestDto input)
	{
		await financeScopeAccessService.EnsureStudentAccessibleAsync(input.StudentId);
		using var uow = unitOfWorkManager.Begin(isTransactional: true);
		var replayId = await FindCommandReplayAsync("create_submit", input.IdempotencyKey, input);
		if (replayId.HasValue)
		{
			var replay = await concessionRequestRepository.GetWithDetailsAsync(replayId.Value);
			await financeScopeAccessService.EnsureStudentAccessibleAsync(replay.StudentId);
			return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(
				await MapDetailAsync(replay));
		}
		var request = await concessionRequestManager.CreatePendingAsync(
			CurrentTenant.Id, academicYearId, input.StudentId,
			input.ConcessionPolicyId, input.ConcessionPolicyVersionId, input.ConcessionTypeId,
			input.Remarks, MapFeeHeadInputs(input.FeeHeadConcessions), input.StudentFeeStructureId,
			input.SourceRequestId, currentUser.GetId(), input.IdempotencyKey, ComputeCommandHash(input),
			dueFeeConcessions: MapDueFeeInputs(input.DueFeeConcessions));
		var evaluation = await concessionRequestManager.EvaluateSubmittedAsync(request);
		var currentFingerprint = ComputePreviewFingerprint(
			request.ConcessionPolicyVersionId, evaluation.DueFees, evaluation.Mappings);
		if (!string.Equals(input.PreviewFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
			throw new BusinessException(FinanceConcessionErrorCodes.PreviewChanged);
		request.ReplaceDocuments(await ValidateAndBuildDocumentsAsync(request, input.Documents));
		EnsureRequiredDocumentsPresent(request);
		request.Submit(Clock.Now);
		await concessionRequestRepository.UpdateAsync(request, autoSave: false);
		await RecordCommandReceiptAsync("create_submit", input.IdempotencyKey, input, request.Id);
		await WriteRequestAuditAsync(request, "submit", "Concession request submitted for approval.");
		try
		{
			await uow.CompleteAsync();
		}
		catch (Exception exception) when (IsConstraintViolation(
			exception, "UX_ConcessionRequest_PendingStudentYearType"))
		{
			throw new BusinessException(FinanceConcessionErrorCodes.DuplicatePendingRequest);
		}
		return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(await MapDetailAsync(request));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
	public async Task<ApiDataResponseDto<ConcessionRequestDetailDto>> CreateDraftAsync(
		Guid academicYearId, CreateConcessionDraftDto input)
	{
		await financeScopeAccessService.EnsureStudentAccessibleAsync(input.StudentId);
		using var uow = unitOfWorkManager.Begin(isTransactional: true);
		var replayId = await FindCommandReplayAsync("create_draft", input.IdempotencyKey, input);
		if (replayId.HasValue)
		{
			var replay = await concessionRequestRepository.GetWithDetailsAsync(replayId.Value);
			await financeScopeAccessService.EnsureStudentAccessibleAsync(replay.StudentId);
			return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(
				await MapDetailAsync(replay));
		}
		var request = await concessionRequestManager.CreatePendingAsync(
			CurrentTenant.Id, academicYearId, input.StudentId,
			input.ConcessionPolicyId, input.ConcessionPolicyVersionId, input.ConcessionTypeId,
			input.Remarks, MapFeeHeadInputs(input.FeeHeadConcessions), input.StudentFeeStructureId,
			input.SourceRequestId, currentUser.GetId(), input.IdempotencyKey, ComputeCommandHash(input),
			allowIncomplete: true, enforcePendingUniqueness: false,
			dueFeeConcessions: MapDueFeeInputs(input.DueFeeConcessions));
		request.ReplaceDocuments(await ValidateAndBuildDocumentsAsync(request, input.Documents));
		await concessionRequestRepository.UpdateAsync(request, autoSave: false);
		await RecordCommandReceiptAsync("create_draft", input.IdempotencyKey, input, request.Id);
		await WriteRequestAuditAsync(request, "create", "Concession request draft created.");
		await uow.CompleteAsync();
		return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(await MapDetailAsync(request));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Submit)]
	public async Task<ApiDataResponseDto<ConcessionRequestDetailDto>> SubmitAsync(
		Guid id, SubmitConcessionRequestDto input)
	{
		using var uow = unitOfWorkManager.Begin(isTransactional: true);
		var replayId = await FindCommandReplayAsync("submit", input.IdempotencyKey, input);
		if (replayId.HasValue)
		{
			if (replayId.Value != id) throw new BusinessException(FinanceConcessionErrorCodes.IdempotencyKeyConflict);
			var replay = await concessionRequestRepository.GetWithDetailsAsync(id);
			await financeScopeAccessService.EnsureStudentAccessibleAsync(replay.StudentId);
			return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(await MapDetailAsync(replay));
		}
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		EnsureRequestOwner(request);
		EnsureConcurrencyStamp(request, input.ConcurrencyStamp);
		request.EnsureDraft();
		await concessionRequestManager.RefreshDraftAsync(request);
		EnsureRequiredDocumentsPresent(request);
		var evaluation = await concessionRequestManager.EvaluateSubmittedAsync(request);
		var currentFingerprint = ComputePreviewFingerprint(
			request.ConcessionPolicyVersionId, evaluation.DueFees, evaluation.Mappings);
		if (!string.Equals(input.PreviewFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
			throw new BusinessException(FinanceConcessionErrorCodes.PreviewChanged);
		if (await concessionRequestRepository.HasPendingForStudentAndTypeAsync(
			request.StudentId, request.ConcessionTypeId, request.AcademicYearId, request.Id))
			throw new BusinessException(FinanceConcessionErrorCodes.DuplicatePendingRequest);
		request.Submit(Clock.Now);
		await concessionRequestRepository.UpdateAsync(request, autoSave: false);
		await RecordCommandReceiptAsync("submit", input.IdempotencyKey, input, request.Id);
		await WriteRequestAuditAsync(request, "submit", "Concession request submitted for approval.");
		try
		{
			await uow.CompleteAsync();
		}
		catch (Exception exception) when (IsConstraintViolation(
			exception, "UX_ConcessionRequest_PendingStudentYearType"))
		{
			throw new BusinessException(FinanceConcessionErrorCodes.DuplicatePendingRequest);
		}
		return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(await MapDetailAsync(request));
	}

	public async Task<ConcessionRequestPreviewDto> PreviewAsync(
		Guid academicYearId, ConcessionRequestPreviewInputDto input)
	{
		await EnsureAnyPermissionAsync(
			ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Edit,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Submit);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(input.StudentId);
		var preview = await concessionRequestManager.PreviewAsync(
			CurrentTenant.Id,
			academicYearId,
			input.StudentId,
			input.ConcessionPolicyId,
			input.ConcessionPolicyVersionId,
			input.ConcessionTypeId,
			MapFeeHeadInputs(input.FeeHeadConcessions),
			input.StudentFeeStructureId,
			MapDueFeeInputs(input.DueFeeConcessions));
		var feeHeadIds = preview.Mappings.Select(x => x.FeeHeadId).ToList();
		var feeHeadNames = (await feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id)))
			.ToDictionary(x => x.Id, x => x.Name);
		var totalBase = preview.Mappings.Sum(x => x.PreConcessionFeeHeadAmount);
		var totalConcession = preview.Mappings.Sum(x => x.FeeHeadConcessionAmount);
		return new ConcessionRequestPreviewDto
		{
			StudentFeeStructureId = preview.Assignment.Id,
			ConcessionPolicyId = input.ConcessionPolicyId,
			ConcessionPolicyVersionId = input.ConcessionPolicyVersionId,
			ConcessionPolicyName = preview.Policy.PolicyName,
			ConcessionPolicyCode = preview.Policy.PolicyCode,
			ConcessionPolicyVersion = preview.Policy.Version,
			ApplyOn = preview.Policy.ApplyOn.ToString(),
			ValueSource = preview.Policy.ValueSource.ToString(),
			CalculationMethod = preview.Policy.CalculationMethod.ToString(),
			MaximumPercentage = preview.Policy.MaximumPercentage,
			MaximumFixedAmount = preview.Policy.MaximumFixedAmount,
			TotalEligibleBase = totalBase,
			TotalConcessionAmount = totalConcession,
			NetPayable = Math.Max(0m, totalBase - totalConcession),
			FeeHeadConcessions = preview.Mappings.Select(x => new FeeHeadConcessionDetailDto
			{
				FeeHeadId = x.FeeHeadId,
				FeeHeadName = feeHeadNames.GetValueOrDefault(x.FeeHeadId, string.Empty),
				AnnualBaseAmount = x.PreConcessionFeeHeadAmount,
				BasedOn = (int)x.UserSpecifiedConcessionAmountType,
				Amount = x.UserSpecifiedConcessionAmountValue,
				DiscountAmount = x.FeeHeadConcessionAmount,
				NetAmount = x.PostConcessionFeeHeadAmount,
			}).ToList(),
			EligibleDueFees = MapEligibleDueFees(preview.EligibleDueFees, feeHeadNames,
				preview.Policy.ApplyOn == ConcessionApplyOn.AllFees),
			PreviewFingerprint = ComputePreviewFingerprint(
				input.ConcessionPolicyVersionId, preview.EligibleDueFees, preview.Mappings),
			GeneratedAt = Clock.Now,
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Edit)]
	public async Task<ApiDataResponseDto<ConcessionRequestDetailDto>> UpdateAsync(
		Guid id, Guid academicYearId, UpdateConcessionRequestDto input)
	{
		using var uow = unitOfWorkManager.Begin(isTransactional: true);
		var replayId = await FindCommandReplayAsync("update_draft", input.IdempotencyKey, input);
		if (replayId.HasValue)
		{
			if (replayId.Value != id) throw new BusinessException(FinanceConcessionErrorCodes.IdempotencyKeyConflict);
			var replay = await concessionRequestRepository.GetWithDetailsAsync(id);
			await financeScopeAccessService.EnsureStudentAccessibleAsync(replay.StudentId);
			return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(await MapDetailAsync(replay));
		}
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		EnsureRequestOwner(request);
		EnsureConcurrencyStamp(request, input.ConcurrencyStamp);
		request.EnsureDraft();

		if (request.AcademicYearId != academicYearId
			|| request.StudentId != input.StudentId
			|| request.ConcessionTypeId != input.ConcessionTypeId)
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidTransition);

		await concessionRequestManager.ReplaceFeeHeadMappingsAsync(
			request, input.ConcessionPolicyId, input.ConcessionPolicyVersionId,
			MapFeeHeadInputs(input.FeeHeadConcessions), allowIncomplete: true,
			dueFeeConcessions: MapDueFeeInputs(input.DueFeeConcessions));
		request.UpdateRemarks(input.Remarks);
		var updatedDocuments = await ValidateAndBuildDocumentsAsync(request, input.Documents);
		var replacementDocuments = await PreserveHiddenDocumentsAsync(request, updatedDocuments);
		await ReleaseRemovedDocumentUploadsAsync(request, replacementDocuments);
		request.ReplaceDocuments(replacementDocuments);
		await concessionRequestRepository.UpdateAsync(request, autoSave: false);
		await RecordCommandReceiptAsync("update_draft", input.IdempotencyKey, input, request.Id);
		await WriteRequestAuditAsync(request, "update", "Concession request draft updated.");
		await uow.CompleteAsync();
		return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(await MapDetailAsync(request));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Delete)]
	public async Task DeleteAsync(Guid id, DeleteConcessionRequestDto input)
	{
		using var uow = unitOfWorkManager.Begin(isTransactional: true);
		var replayId = await FindCommandReplayAsync("delete_draft", input.IdempotencyKey, input);
		if (replayId.HasValue)
		{
			if (replayId.Value != id) throw new BusinessException(FinanceConcessionErrorCodes.IdempotencyKeyConflict);
			return;
		}
		var request = await concessionRequestRepository.GetAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		EnsureRequestOwner(request);
		EnsureConcurrencyStamp(request, input.RequestConcurrencyStamp);
		request.EnsureCanDelete();
		await RecordCommandReceiptAsync("delete_draft", input.IdempotencyKey, input, request.Id);
		await WriteRequestAuditAsync(request, "delete", "Concession request draft deleted.");
		await concessionRequestRepository.DeleteAsync(request, autoSave: false);
		await uow.CompleteAsync();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.UploadDocument)]
	public async Task UploadDocumentsAsync(Guid id, UploadConcessionDocumentsDto input)
	{
		using var uow = unitOfWorkManager.Begin(isTransactional: true);
		var replayId = await FindCommandReplayAsync("replace_documents", input.IdempotencyKey, input);
		if (replayId.HasValue)
		{
			if (replayId.Value != id) throw new BusinessException(FinanceConcessionErrorCodes.IdempotencyKeyConflict);
			var replay = await concessionRequestRepository.GetWithDetailsAsync(id);
			await financeScopeAccessService.EnsureStudentAccessibleAsync(replay.StudentId);
			return;
		}
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		EnsureRequestOwner(request);
		EnsureConcurrencyStamp(request, input.ConcurrencyStamp);
		request.EnsureDraft();
		var type = await concessionTypeRepository.GetAsync(request.ConcessionTypeId);
		var allowedDocumentIds = type.ConcessionTypeDocuments.Select(document => document.Id).ToHashSet();
		if (input.Documents.Select(document => document.DocumentId).Distinct().Count() != input.Documents.Count ||
			input.Documents.Any(document =>
				!allowedDocumentIds.Contains(document.DocumentId) ||
				string.IsNullOrWhiteSpace(document.Url) ||
				(!TryParseStagedDocumentAccessUrl(document.Url, out _) &&
				 !string.Equals(document.Url, BuildDocumentAccessUrl(request.Id, document.DocumentId),
					 StringComparison.Ordinal))))
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);
		}

		var docs = await ValidateAndBuildDocumentsAsync(request, input.Documents);
		var replacementDocuments = await PreserveHiddenDocumentsAsync(request, docs);
		await ReleaseRemovedDocumentUploadsAsync(request, replacementDocuments);
		request.ReplaceDocuments(replacementDocuments);
		await concessionRequestRepository.UpdateAsync(request, autoSave: false);
		await RecordCommandReceiptAsync("replace_documents", input.IdempotencyKey, input, request.Id);
		await WriteRequestAuditAsync(
			request,
			"update-documents",
			$"Concession request evidence updated with {request.ConcessionRequestDocuments.Count} document(s).");
		await uow.CompleteAsync();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.UploadDocument)]
	public async Task<ConcessionDocumentFileUploadResponseDto> UploadDocumentFileAsync(
		Guid concessionTypeId,
		Guid documentId,
		UploadConcessionDocumentFileDto input)
	{
		var type = await concessionTypeRepository.GetAsync(concessionTypeId);
		var requirement = type.ConcessionTypeDocuments.FirstOrDefault(document => document.Id == documentId)
			?? throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);

		if (input.Content.Length == 0 || input.Content.Length > requirement.MaxFileSizeBytes)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.FileTooLarge)
				.WithData("MaxFileSizeBytes", requirement.MaxFileSizeBytes);
		}

		var safeFileName = Path.GetFileName(input.FileName);
		if (!string.Equals(safeFileName, input.FileName, StringComparison.Ordinal))
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);
		}

		var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
		var documentType = ConcessionTypeConsts.GetDocumentType(extension);
		var acceptedTypes = requirement.AcceptedFormats.Select(format => format.DocumentType).ToHashSet();
		if (!documentType.HasValue ||
			!acceptedTypes.Contains(documentType.Value) ||
			!FileValidationService.IsMatchingDocumentContentType(extension, input.ContentType))
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidFileFormat)
				.WithData("AcceptedFormats", string.Join(", ", acceptedTypes.Select(ConcessionTypeConsts.GetExtension)));
		}

		try
		{
			FileValidationService.ValidateDocumentFileSignature(input.Content, extension);
		}
		catch (BusinessException)
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidFileFormat);
		}

		var storedFileName = await fileStorageAppService.UploadAsync(safeFileName, input.Content);
		var fileUrl = $"/api/files/{Uri.EscapeDataString(storedFileName)}";
		var uploadId = guidGenerator.Create();
		try
		{
			await concessionDocumentUploadRepository.InsertAsync(new ConcessionDocumentUpload(
				uploadId, CurrentTenant.Id, concessionTypeId, documentId,
				currentUser.GetId(), fileUrl,
				Clock.Now.AddHours(ConcessionDocumentUpload.StagingLifetimeHours)), autoSave: true);
		}
		catch
		{
			try
			{
				await fileStorageAppService.DeleteAsync(storedFileName);
			}
			catch (Exception cleanupException)
			{
				Logger.LogWarning(
					cleanupException,
					"Failed to remove concession file {StoredFileName} after staging receipt creation failed.",
					storedFileName);
			}
			throw;
		}
		return new ConcessionDocumentFileUploadResponseDto
		{
			Url = BuildStagedDocumentAccessUrl(uploadId)
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.UploadDocument)]
	public async Task<FileDownloadResult> DownloadStagedDocumentAsync(Guid uploadId)
	{
		var upload = await concessionDocumentUploadRepository.FindAsync(uploadId)
			?? throw new BusinessException(FinanceConcessionErrorCodes.DocumentOwnershipMismatch);
		if (upload.UploadedBy != currentUser.GetId() || upload.ConcessionRequestId.HasValue)
			throw new BusinessException(FinanceConcessionErrorCodes.DocumentOwnershipMismatch);
		if (Clock.Now >= upload.ExpiresOn)
			throw new BusinessException(FinanceConcessionErrorCodes.StagedDocumentExpired);

		const string fileUrlPrefix = "/api/files/";
		if (!upload.Url.StartsWith(fileUrlPrefix, StringComparison.Ordinal) ||
			upload.Url.Length == fileUrlPrefix.Length)
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);
		var storedFileName = Uri.UnescapeDataString(upload.Url[fileUrlPrefix.Length..]);
		return await fileStorageAppService.GetAsync(storedFileName);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Approve)]
	public async Task<ConcessionApprovalPreviewDto> PreviewApprovalAsync(
		Guid id, PreviewConcessionApprovalDto input)
	{
		if (input.FeeHeadApprovals is { Count: > 0 })
			throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		EnsureConcurrencyStamp(request, input.RequestConcurrencyStamp);
		EnsurePendingDecisionStatus(request);
		EnsureRequiredDocumentsPresent(request);

		var evaluation = await concessionRequestManager.EvaluateSubmittedAsync(request);
		var dueFees = evaluation.DueFees.ToList();
		var fingerprint = ComputePreviewFingerprint(
			request.ConcessionPolicyVersionId, dueFees, evaluation.Mappings);
		if (!string.Equals(input.PreviewFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
			throw new BusinessException(FinanceConcessionErrorCodes.PreviewChanged);

		var allocations = BuildSubmittedApprovalAllocations(request, dueFees, evaluation.Policy, evaluation.Mappings);
		if (allocations.Count == 0 || allocations.Sum(x => x.ConcessionAmount) <= 0m)
			throw new BusinessException(FinanceConcessionErrorCodes.ApprovalAllocationsRequired);

		return new ConcessionApprovalPreviewDto
		{
			ConcessionRequestId = request.Id,
			RequestConcurrencyStamp = request.ConcurrencyStamp,
			PreviewFingerprint = fingerprint,
			TotalApprovedAmount = allocations.Sum(x => x.ConcessionAmount),
			RefundRequestAmount = allocations.Sum(x =>
				Math.Max(0m, x.ConcessionAmount - dueFees.First(d => d.Id == x.DueFeeId).OutstandingAmount)),
			DueFeeAllocations = allocations.Select(allocation =>
			{
				var dueFee = dueFees.First(x => x.Id == allocation.DueFeeId);
				return new ApprovedDueFeeAllocationDto
				{
					DueFeeId = allocation.DueFeeId,
					FeeHeadId = allocation.FeeHeadId,
					Month = (int)dueFee.Month,
					Year = dueFee.Year,
					ConcessionAmount = allocation.ConcessionAmount,
				};
			}).ToList(),
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.ViewDocument)]
	public async Task<FileDownloadResult> DownloadDocumentAsync(Guid id, Guid documentId)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		var document = request.ConcessionRequestDocuments.FirstOrDefault(x =>
			x.DocumentId == documentId && x.Status == 1 && !string.IsNullOrWhiteSpace(x.Url))
			?? throw new BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);
		const string fileUrlPrefix = "/api/files/";
		if (!document.Url.StartsWith(fileUrlPrefix, StringComparison.Ordinal))
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidDocumentSubmission);

		var storedFileName = Uri.UnescapeDataString(document.Url[fileUrlPrefix.Length..]);
		return await fileStorageAppService.GetAsync(storedFileName);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Approve)]
	public async Task<ConcessionApprovalResponseDto> ApproveAsync(Guid id, ApproveConcessionRequestDto input)
	{
		if (input.FeeHeadApprovals is { Count: > 0 })
			throw new BusinessException(ConcessionPolicyErrorCodes.InputNotAllowed);
		using var uow = unitOfWorkManager.Begin(isTransactional: true);
		var replayId = await FindCommandReplayAsync("approve", input.IdempotencyKey, input);
		if (replayId.HasValue)
		{
			if (replayId.Value != id) throw new BusinessException(FinanceConcessionErrorCodes.IdempotencyKeyConflict);
			var replay = await concessionRequestRepository.GetWithDetailsAsync(id);
			await financeScopeAccessService.EnsureStudentAccessibleAsync(replay.StudentId);
			return await BuildApprovalResponseAsync(replay);
		}
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		EnsureConcurrencyStamp(request, input.RequestConcurrencyStamp);
		await PromoteDraftToPendingAsync(request);
		var evaluation = await concessionRequestManager.EvaluateSubmittedAsync(request);
		var dueFees = evaluation.DueFees.ToList();
		var fingerprint = ComputePreviewFingerprint(
			request.ConcessionPolicyVersionId, dueFees, evaluation.Mappings);
		if (!string.Equals(input.PreviewFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
			throw new BusinessException(FinanceConcessionErrorCodes.PreviewChanged);

		EnsureRequiredDocumentsPresent(request);

		var allocations = BuildSubmittedApprovalAllocations(request, dueFees, evaluation.Policy, evaluation.Mappings);
		if (allocations.Count == 0 || allocations.Sum(x => x.ConcessionAmount) <= 0m)
			throw new BusinessException(FinanceConcessionErrorCodes.ApprovalAllocationsRequired);
		var approvedTotal = allocations.Sum(x => x.ConcessionAmount);

		var approvedAt = Clock.Now;
		request.Approve(currentUser.Id!.Value, input.Message, allocations, approvedAt);

		var affectedDueFees = allocations.Select(a => dueFees.First(df => df.Id == a.DueFeeId))
			.DistinctBy(x => x.Id).ToList();
		var previousOverpayment = affectedDueFees.ToDictionary(x => x.Id, x => x.OverpaymentAmount);
		foreach (var allocation in allocations)
		{
			var dueFee = affectedDueFees.First(df => df.Id == allocation.DueFeeId);
			dueFee.ApplyConcession(allocation.ConcessionAmount);
		}
		var newOverpayment = affectedDueFees.ToDictionary(x => x.Id,
			x => Math.Max(0m, x.OverpaymentAmount - previousOverpayment[x.Id]));
		var refundRequests = await CreateConcessionRefundRequestsAsync(request,
			newOverpayment, affectedDueFees.ToDictionary(x => x.Id));

		await dueFeeRepository.UpdateManyAsync(affectedDueFees, autoSave: false);
		await concessionRequestRepository.UpdateAsync(request, autoSave: false);
		await RecordCommandReceiptAsync("approve", input.IdempotencyKey, input, request.Id);
		await WriteRequestAuditAsync(
			request,
			"approve",
			$"Concession request approved for {approvedTotal:0.00}.",
			input.Message,
			new { Requested = request.GetTotalRequestedConcession(), Approved = approvedTotal,
				Allocations = allocations.Count, RefundRequestIds = refundRequests.Select(x => x.Id).ToList() });
		try
		{
			await uow.CompleteAsync();
		}
		catch (AbpDbConcurrencyException)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.ConcurrencyConflict);
		}

		await eventBus.PublishAsync(new Finance.Events.ConcessionApprovedEto
		{
			ConcessionRequestId   = request.Id,
			StudentId             = request.StudentId,
			ConcessionTypeId      = request.ConcessionTypeId,
			TotalConcessionAmount = allocations.Sum(a => a.ConcessionAmount),
			TenantId              = CurrentTenant.Id,
			ApprovedAt            = approvedAt,
		});

		return new ConcessionApprovalResponseDto
		{
			ConcessionRequestId             = request.Id,
			StudentConcessionTypeRequestId  = request.Id,
			Status                          = request.Status,
			StatusName                      = request.Status.ToString(),
			ApprovedAt                      = request.GetApprovedOn() ?? Clock.Now,
			ApproverId                      = currentUser.Id!.Value,
			ApproverMessage                 = input.Message,
			TotalApprovedAmount             = approvedTotal,
			RefundRequests = refundRequests.Select(x => new ConcessionRefundReferenceDto
			{
				RefundRequestId = x.Id, VoucherNumber = x.VoucherNumber,
				OriginalReceiptId = x.OriginalReceiptId!.Value,
				Amount = x.CalculatedAmount, Status = x.Status.ToString()
			}).ToList(),
			DueFeeAllocations = allocations.Select(a =>
			{
				var df = dueFees.First(x => x.Id == a.DueFeeId);
				return new ApprovedDueFeeAllocationDto
				{
					DueFeeId = a.DueFeeId, FeeHeadId = a.FeeHeadId,
					Month = (int)df.Month, Year = df.Year, ConcessionAmount = a.ConcessionAmount,
				};
			}).ToList(),
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Reject)]
	public async Task<ConcessionRejectionResponseDto> RejectAsync(Guid id, RejectConcessionRequestDto input)
	{
		using var uow = unitOfWorkManager.Begin(isTransactional: true);
		var replayId = await FindCommandReplayAsync("reject", input.IdempotencyKey, input);
		if (replayId.HasValue)
		{
			if (replayId.Value != id) throw new BusinessException(FinanceConcessionErrorCodes.IdempotencyKeyConflict);
			var replay = await concessionRequestRepository.GetWithDetailsAsync(id);
			await financeScopeAccessService.EnsureStudentAccessibleAsync(replay.StudentId);
			return BuildRejectionResponse(replay);
		}
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		EnsureConcurrencyStamp(request, input.RequestConcurrencyStamp);

		await PromoteDraftToPendingAsync(request);
		request.Reject(currentUser.Id!.Value, input.Message, Clock.Now);
		await concessionRequestRepository.UpdateAsync(request, autoSave: false);
		await RecordCommandReceiptAsync("reject", input.IdempotencyKey, input, request.Id);
		await WriteRequestAuditAsync(request, "reject", "Concession request rejected.", input.Message);
		await uow.CompleteAsync();

		return BuildRejectionResponse(request);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Cancel)]
	public async Task<ConcessionCancellationResponseDto> CancelAsync(Guid id, CancelConcessionRequestDto input)
	{
		using var uow = unitOfWorkManager.Begin(isTransactional: true);
		var replayId = await FindCommandReplayAsync("cancel", input.IdempotencyKey, input);
		if (replayId.HasValue)
		{
			if (replayId.Value != id) throw new BusinessException(FinanceConcessionErrorCodes.IdempotencyKeyConflict);
			var replay = await concessionRequestRepository.GetWithDetailsAsync(id);
			await financeScopeAccessService.EnsureStudentAccessibleAsync(replay.StudentId);
			return BuildCancellationResponse(replay);
		}
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		EnsureConcurrencyStamp(request, input.RequestConcurrencyStamp);
		EnsureRequestOwner(request);
		request.Cancel(currentUser.Id!.Value, input.Reason, Clock.Now);
		await concessionRequestRepository.UpdateAsync(request, autoSave: false);
		await RecordCommandReceiptAsync("cancel", input.IdempotencyKey, input, request.Id);
		await WriteRequestAuditAsync(request, "cancel", "Concession request cancelled.", input.Reason);
		await uow.CompleteAsync();

		return BuildCancellationResponse(request);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Reverse)]
	public async Task<ConcessionReversalPreviewDto> PreviewReversalAsync(
		Guid id, PreviewConcessionReversalDto input)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		await EnsureConcessionRefundReversalAllowedAsync(request, input.Lines.Sum(x => x.Amount));
		EnsureConcurrencyStamp(request, input.RequestConcurrencyStamp);
		if (request.Status is not (ConcessionRequestStatus.Approved or ConcessionRequestStatus.PartiallyReversed))
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidReversal)
				.WithData("Status", request.Status.ToString());

		var allocationMap = request.ConcessionDueFeeAllocations.ToDictionary(x => x.Id);
		var lines = input.Lines.Select(line =>
		{
			if (!allocationMap.TryGetValue(line.ConcessionDueFeeAllocationId, out var allocation))
				throw new BusinessException(FinanceConcessionErrorCodes.InvalidReversal);
			var alreadyReversed = request.ConcessionReversals
				.Where(x => x.ConcessionDueFeeAllocationId == allocation.Id).Sum(x => x.Amount);
			var remaining = allocation.ConcessionAmount - alreadyReversed;
			if (line.Amount <= 0m || line.Amount > remaining)
				throw new BusinessException(FinanceConcessionErrorCodes.ReversalExceedsConcessionBalance)
					.WithData("AllocationId", allocation.Id)
					.WithData("Requested", line.Amount)
					.WithData("Available", remaining);
			return (Input: line, Allocation: allocation);
		}).ToList();
		var dueIds = lines.Select(x => x.Allocation.DueFeeId).ToHashSet();
		var dueFees = await dueFeeRepository.GetListAsync(x =>
			dueIds.Contains(x.Id) && x.StudentId == request.StudentId &&
			x.StudentFeeStructureId == request.StudentFeeStructureId);
		if (dueFees.Count != dueIds.Count)
			throw new BusinessException(FinanceConcessionErrorCodes.DueFeeNotFound);
		var dueFeeMap = dueFees.ToDictionary(x => x.Id);
		foreach (var line in lines)
		{
			var dueFee = dueFeeMap[line.Allocation.DueFeeId];
			if (!string.Equals(dueFee.ConcurrencyStamp, line.Input.ConcurrencyStamp, StringComparison.Ordinal))
				throw new BusinessException(FinanceConcessionErrorCodes.ConcurrencyConflict)
					.WithData("DueFeeId", dueFee.Id);
		}

		var reversalTotal = lines.Sum(x => x.Input.Amount);
		return new ConcessionReversalPreviewDto
		{
			ConcessionRequestId = request.Id,
			RequestConcurrencyStamp = request.ConcurrencyStamp,
			TotalApprovedAmount = request.GetTotalApprovedConcession(),
			TotalPreviouslyReversedAmount = request.GetTotalReversedConcession(),
			TotalReversalAmount = reversalTotal,
			NetApprovedAfterReversal = request.GetNetApprovedConcession() - reversalTotal,
			Lines = lines.Select(line =>
			{
				var dueFee = dueFeeMap[line.Allocation.DueFeeId];
				return new ConcessionReversalPreviewLineDto
				{
					ConcessionDueFeeAllocationId = line.Allocation.Id,
					DueFeeId = dueFee.Id,
					FeeHeadId = line.Allocation.FeeHeadId,
					ReversalAmount = line.Input.Amount,
					OutstandingBefore = dueFee.OutstandingAmount,
					OutstandingAfter = Math.Max(0m, dueFee.OriginalAmount - dueFee.NetAmount +
						line.Input.Amount - dueFee.PaidAmount + dueFee.RefundedAmount),
				};
			}).ToList(),
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Reverse)]
	public async Task<ConcessionReversalResponseDto> ReverseAsync(
		Guid id, ReverseConcessionRequestDto input)
	{
		using var uow = unitOfWorkManager.Begin(isTransactional: true);
		var replayId = await FindCommandReplayAsync("reverse", input.IdempotencyKey, input);
		if (replayId.HasValue)
		{
			if (replayId.Value != id) throw new BusinessException(FinanceConcessionErrorCodes.IdempotencyKeyConflict);
			var replay = await concessionRequestRepository.GetWithDetailsAsync(id);
			await financeScopeAccessService.EnsureStudentAccessibleAsync(replay.StudentId);
			var replayActor = currentUser.Id ?? Guid.Empty;
			return BuildReversalResponse(
				replay,
				replay.ConcessionReversals.MaxBy(x => x.ReversedAt)?.ReversedAt ?? Clock.Now,
				replayActor,
				replay.ConcessionReversals);
		}
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
		EnsureConcurrencyStamp(request, input.RequestConcurrencyStamp);
		await EnsureConcessionRefundReversalAllowedAsync(request, input.Lines.Sum(x => x.Amount));

		if (request.Status is not (ConcessionRequestStatus.Approved or ConcessionRequestStatus.PartiallyReversed)
			|| input.Lines.Count == 0
			|| input.Lines.Select(x => x.ConcessionDueFeeAllocationId).Distinct().Count() != input.Lines.Count)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.InvalidReversal)
				.WithData("Status", request.Status.ToString());
		}

		var actorId = currentUser.Id
			?? throw new BusinessException(FinanceConcessionErrorCodes.InvalidReversal);
		var allocationMap = request.ConcessionDueFeeAllocations.ToDictionary(x => x.Id);
		var dueFeeIds = new HashSet<Guid>();
		var now = Clock.Now;
		var reversals = input.Lines.Select(line =>
		{
			if (!allocationMap.TryGetValue(line.ConcessionDueFeeAllocationId, out var allocation)
				|| line.Amount <= 0m)
				throw new BusinessException(FinanceConcessionErrorCodes.InvalidReversal);

			var alreadyReversed = request.ConcessionReversals
				.Where(x => x.ConcessionDueFeeAllocationId == allocation.Id)
				.Sum(x => x.Amount);
			var remaining = allocation.ConcessionAmount - alreadyReversed;
			if (line.Amount > remaining)
				throw new BusinessException(FinanceConcessionErrorCodes.ReversalExceedsConcessionBalance)
					.WithData("AllocationId", allocation.Id)
					.WithData("Requested", line.Amount)
					.WithData("Available", remaining);

			dueFeeIds.Add(allocation.DueFeeId);
			return new ConcessionReversal(
				guidGenerator.Create(), CurrentTenant.Id, request.Id, allocation.Id,
				allocation.DueFeeId, allocation.FeeHeadId, line.Amount, input.Reason, actorId, now);
		}).ToList();

		var dueFees = await dueFeeRepository.GetListAsync(x =>
			dueFeeIds.Contains(x.Id) &&
			x.StudentId == request.StudentId &&
			x.StudentFeeStructureId == request.StudentFeeStructureId);
		if (dueFees.Count != dueFeeIds.Count)
			throw new BusinessException(FinanceConcessionErrorCodes.DueFeeNotFound);

		var dueFeeMap = dueFees.ToDictionary(x => x.Id);
		var inputByAllocation = input.Lines.ToDictionary(x => x.ConcessionDueFeeAllocationId);
		foreach (var allocationId in inputByAllocation.Keys)
		{
			var dueFee = dueFeeMap[allocationMap[allocationId].DueFeeId];
			if (!string.Equals(
				dueFee.ConcurrencyStamp,
				inputByAllocation[allocationId].ConcurrencyStamp,
				StringComparison.Ordinal))
				throw new BusinessException(FinanceConcessionErrorCodes.ConcurrencyConflict)
					.WithData("DueFeeId", dueFee.Id);
		}
		foreach (var reversal in reversals)
			dueFeeMap[reversal.DueFeeId].ReverseConcession(reversal.Amount);

		request.RecordReversals(reversals);
		await dueFeeRepository.UpdateManyAsync(dueFees, autoSave: false);
		await concessionRequestRepository.UpdateAsync(request, autoSave: false);
		await RecordCommandReceiptAsync("reverse", input.IdempotencyKey, input, request.Id);
		await WriteRequestAuditAsync(
			request,
			"reverse",
			$"Reversed {reversals.Sum(x => x.Amount):0.00} of an approved concession.",
			input.Reason,
			new { Lines = reversals.Count, request.Status });
		try
		{
			await uow.CompleteAsync();
		}
		catch (AbpDbConcurrencyException)
		{
			throw new BusinessException(FinanceConcessionErrorCodes.ConcurrencyConflict);
		}

		return BuildReversalResponse(request, now, actorId, reversals);
	}
}
