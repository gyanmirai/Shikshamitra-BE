using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using Volo.Abp;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.Concessions;

public partial class ConcessionRequestAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
	public async Task<ApiDataResponseDto<ConcessionRequestDetailDto>> CreateAsync(
		Guid academicYearId, CreateConcessionRequestDto input)
	{
		var request = await concessionRequestManager.CreatePendingAsync(
			CurrentTenant.Id, academicYearId, input.StudentId, input.ConcessionTypeId,
			input.Remarks, MapFeeHeadInputs(input.FeeHeadConcessions));
		return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(await MapDetailAsync(request));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
	public async Task<ApiDataResponseDto<ConcessionRequestDetailDto>> UpdateAsync(
		Guid id, Guid academicYearId, UpdateConcessionRequestDto input)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		request.EnsurePending();

		if (request.StudentId != input.StudentId || request.ConcessionTypeId != input.ConcessionTypeId)
			throw new BusinessException(FinanceConcessionErrorCodes.NotPending);

		await concessionRequestManager.ReplaceFeeHeadMappingsAsync(request, MapFeeHeadInputs(input.FeeHeadConcessions));
		request.UpdateRemarks(input.Remarks);
		await concessionRequestRepository.UpdateAsync(request, autoSave: true);
		return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(await MapDetailAsync(request));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Delete)]
	public async Task DeleteAsync(Guid id)
	{
		var request = await concessionRequestRepository.GetAsync(id);
		request.EnsureCanDelete();
		await concessionRequestRepository.DeleteAsync(request);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
	public async Task UploadDocumentsAsync(Guid id, UploadConcessionDocumentsDto input)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		request.EnsurePending();
		var type = await concessionTypeRepository.GetAsync(request.ConcessionTypeId);
		var allowedDocumentIds = type.ConcessionTypeDocuments.Select(document => document.Id).ToHashSet();
		if (input.Documents.Select(document => document.DocumentId).Distinct().Count() != input.Documents.Count ||
			input.Documents.Any(document =>
				!allowedDocumentIds.Contains(document.DocumentId) ||
				string.IsNullOrWhiteSpace(document.Url) ||
				!document.Url.StartsWith("/api/files/", StringComparison.Ordinal)))
		{
			throw new BusinessException(ConcessionTypeErrorCodes.InvalidDocument);
		}

		var docs = input.Documents.Select(d => new ConcessionRequestDocument(
			guidGenerator.Create(), CurrentTenant.Id, id, d.DocumentId, d.Url, status: 1)).ToList();

		request.ReplaceDocuments(docs);
		await concessionRequestRepository.UpdateAsync(request, autoSave: true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
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
		return new ConcessionDocumentFileUploadResponseDto
		{
			Url = $"/api/files/{Uri.EscapeDataString(storedFileName)}"
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
	public async Task<ConcessionApprovalResponseDto> ApproveAsync(Guid id, ApproveConcessionRequestDto input)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		var dueFees = await dueFeeRepository.GetListAsync(df => df.StudentId == request.StudentId);

		if (currentUser.Id == request.CreatorId && !IsPrivilegedUser())
			throw new BusinessException(FinanceConcessionErrorCodes.ApproverCannotBeRequestor);

		// validate that if documents specified in the concession type are not uploaded, then the request cannot be approved
		var type = await concessionTypeRepository.GetAsync(request.ConcessionTypeId);
		if (type != null)
		{
			var requiredDocuments = type.ConcessionTypeDocuments.Where(document => document.IsRequired);
			foreach (var doc in requiredDocuments)
			{
				if (!request.ConcessionRequestDocuments.Any(d =>
					d.DocumentId == doc.Id &&
					d.Status == 1 &&
					!string.IsNullOrWhiteSpace(d.Url)))
				{
					throw new BusinessException(FinanceConcessionErrorCodes.MissingRequiredDocuments);
				}
			}
		}

		var approvals = input.FeeHeadApprovals?.Select(a => new FeeHeadApprovalInput(
			a.FeeHeadId, a.ApprovedConcessionAmount,
			a.DueFeeAllocations.Select(d => new DueFeeAllocationInput(d.DueFeeId, d.ConcessionAmount)).ToList()
		)).ToList() ?? concessionRequestManager.BuildAutoApproveAllocations(request, dueFees);

		foreach (var group in approvals)
		{
			var mapping = request.ConcessionFeeHeadMappings.FirstOrDefault(m => m.FeeHeadId == group.FeeHeadId)
				?? throw new BusinessException(FinanceConcessionErrorCodes.FeeHeadNotInStructure);

			var sum = group.DueFeeAllocations.Sum(a => a.ConcessionAmount);
			if (sum != group.ApprovedConcessionAmount && group.DueFeeAllocations.Count > 0)
				throw new BusinessException(FinanceConcessionErrorCodes.AllocationSumMismatch);
		}

		var allocations = concessionRequestManager.BuildDueFeeAllocations(
			CurrentTenant.Id, request.Id, approvals, dueFees);

		request.Approve(currentUser.Id!.Value, input.Message, allocations);

		using var uow = unitOfWorkManager.Begin(isTransactional: true);
		var affectedDueFees = allocations.Select(a => dueFees.First(df => df.Id == a.DueFeeId)).ToList();
		foreach (var allocation in allocations)
		{
			var dueFee = affectedDueFees.First(df => df.Id == allocation.DueFeeId);
			dueFee.ApplyConcession((double)allocation.ConcessionAmount);
		}

		await dueFeeRepository.UpdateManyAsync(affectedDueFees, autoSave: false);
		await concessionRequestRepository.UpdateAsync(request, autoSave: false);
		await uow.CompleteAsync();

		await eventBus.PublishAsync(new Finance.Events.ConcessionApprovedEto
		{
			ConcessionRequestId   = request.Id,
			StudentId             = request.StudentId,
			ConcessionTypeId      = request.ConcessionTypeId,
			TotalConcessionAmount = allocations.Sum(a => a.ConcessionAmount),
			TenantId              = CurrentTenant.Id,
			ApprovedAt            = Clock.Now,
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
			TotalApprovedAmount             = allocations.Sum(a => a.ConcessionAmount),
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

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add)]
	public async Task<ConcessionRejectionResponseDto> RejectAsync(Guid id, RejectConcessionRequestDto input)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);

		if (currentUser.Id == request.CreatorId && !IsPrivilegedUser())
			throw new BusinessException(FinanceConcessionErrorCodes.ApproverCannotBeRequestor);

		request.Reject(currentUser.Id!.Value, input.Message);
		await concessionRequestRepository.UpdateAsync(request, autoSave: true);

		return new ConcessionRejectionResponseDto
		{
			StudentConcessionTypeRequestId = request.Id,
			Status        = request.Status,
			StatusName    = request.Status.ToString(),
			RejectedAt    = Clock.Now,
			RejectionReason = request.RejectionReason ?? input.Message,
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Delete)]
	public async Task<ConcessionCancellationResponseDto> CancelAsync(Guid id)
	{
		var request = await concessionRequestRepository.GetWithDetailsAsync(id);
		request.Cancel();
		await concessionRequestRepository.UpdateAsync(request, autoSave: true);

		return new ConcessionCancellationResponseDto
		{
			StudentConcessionTypeRequestId = request.Id,
			Status      = request.Status,
			StatusName  = request.Status.ToString(),
			CancelledAt = Clock.Now,
		};
	}
}
