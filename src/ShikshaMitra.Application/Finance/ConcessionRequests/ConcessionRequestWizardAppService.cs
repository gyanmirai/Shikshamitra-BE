using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;
using ShikshaMitra.Finance.Setting;
using ShikshaMitra.Finance.Setting.ConcessionType;

namespace ShikshaMitra.Finance.ConcessionRequests;

/// <summary>
/// Handles the six-step Draft wizard for a concession request batch.
/// Each step corresponds to a Save &amp; Continue operation that builds up
/// the Draft before it is submitted.
/// </summary>
[RemoteService(false)]
public class ConcessionRequestWizardAppService(
	IConcessionRequestRepository concessionRequestRepository,
	IRepository<ConcessionType, Guid> concessionTypeRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	ConcessionRequestManager concessionRequestManager)
	: ShikshaMitraAppService, IConcessionRequestWizardAppService
{
	// -------------------------------------------------------------------------
	// Step 3 — Fee Heads
	// -------------------------------------------------------------------------

	/// <summary>
	/// Step 3 (Edit / Resume) — Returns saved fee-head selections for the full
	/// concession-type batch identified by the anchor request.
	/// Types with no mappings return an empty list, leaving those checkboxes
	/// unchecked on resume.
	/// </summary>
	public virtual async Task<ApiDataResponseDto<ConcessionRequestFeeHeadsDto>> GetFeeHeadsAsync(
		Guid concessionRequestId)
	{
		var anchor = await concessionRequestRepository.GetAsync(concessionRequestId);

		var batchRequests = await concessionRequestRepository
			.GetBatchWithMappingsAsync(anchor.StudentId, anchor.Status);

		var concessionTypeIds = batchRequests.Select(r => r.ConcessionTypeId).ToList();
		var concessionTypes = await concessionTypeRepository
			.GetListAsync(ct => concessionTypeIds.Contains(ct.Id));

		var concessionTypeById = concessionTypes.ToDictionary(ct => ct.Id);

		var typeEntries = batchRequests
			.Select(r =>
			{
				concessionTypeById.TryGetValue(r.ConcessionTypeId, out var ct);
				return new FeeHeadSelectionByTypeDto
				{
					ConcessionTypeId = r.ConcessionTypeId,
					ConcessionName   = ct?.Name ?? string.Empty,
					DiscountType     = MapDiscountType(ct?.DiscountType),
					FeeHeadIds       = r.ConcessionFeeHeadMappings
					                    .Select(m => m.FeeHeadId)
					                    .ToList(),
				};
			})
			.ToList();

		var dto = new ConcessionRequestFeeHeadsDto
		{
			ConcessionRequestId = anchor.Id,
			StudentId           = anchor.StudentId,
			ConcessionTypes     = typeEntries,
		};

		return ApiDataResponseDto<ConcessionRequestFeeHeadsDto>.SuccessResult(dto);
	}

	/// <summary>
	/// Step 3 — Saves fee-head selections for all concession types in the student's
	/// Draft batch. Full replacement per concession type.
	/// </summary>
	public async Task SaveFeeHeadsDataAsync(Guid concessionRequestId, SaveFeeHeadsDataDto input)
	{
		var anchor = await concessionRequestRepository.GetAsync(concessionRequestId);

		if (anchor.Status != ConcessionRequestStatus.Draft)
		{
			throw new BusinessException(ConcessionRequestErrorCodes.CannotUpdateNonDraft)
				.WithData("RequestId", concessionRequestId)
				.WithData("Status", anchor.Status.ToString());
		}

		foreach (var item in input.FeeHeadsData)
		{
			var duplicateId = item.SelectedFeeHeadIds
				.GroupBy(id => id)
				.FirstOrDefault(g => g.Count() > 1)?.Key;

			if (duplicateId.HasValue)
			{
				throw new BusinessException(ConcessionRequestErrorCodes.DuplicateFeeHeadInConcessionType)
					.WithData("ConcessionTypeId", item.ConcessionTypeId)
					.WithData("FeeHeadId", duplicateId.Value);
			}
		}

		var distinctFeeHeadIds = input.FeeHeadsData
			.SelectMany(i => i.SelectedFeeHeadIds)
			.Distinct();

		foreach (var feeHeadId in distinctFeeHeadIds)
		{
			if (!await feeHeadRepository.AnyAsync(f => f.Id == feeHeadId))
			{
				throw new BusinessException(ConcessionRequestErrorCodes.FeeHeadNotFound)
					.WithData("FeeHeadId", feeHeadId);
			}
		}

		var domainItems = input.FeeHeadsData
			.Select(dto => new ConcessionTypeFeeHeadsInput(
				dto.ConcessionTypeId,
				dto.SelectedFeeHeadIds))
			.ToList();

		await concessionRequestManager.SaveFeeHeadsDataAsync(
			CurrentTenant.Id,
			anchor.StudentId,
			domainItems);
	}

	// -------------------------------------------------------------------------
	// Step 4 — Value Entry
	// -------------------------------------------------------------------------

	/// <summary>
	/// Step 4 (Edit / Resume) — Returns the previously saved discount values
	/// per concession type per fee head.
	/// </summary>
	public virtual async Task<ApiDataResponseDto<ConcessionRequestValueEntriesDto>> GetValueEntriesAsync(
		Guid concessionRequestId)
	{
		var anchor = await concessionRequestRepository.GetAsync(concessionRequestId);

		var batchRequests = await concessionRequestRepository
			.GetBatchWithMappingsAsync(anchor.StudentId, anchor.Status);

		var concessionTypeIds = batchRequests.Select(r => r.ConcessionTypeId).ToList();
		var concessionTypes = await concessionTypeRepository
			.GetListAsync(ct => concessionTypeIds.Contains(ct.Id));

		var concessionTypeById = concessionTypes.ToDictionary(ct => ct.Id);

		var typeEntries = batchRequests
			.Select(r =>
			{
				concessionTypeById.TryGetValue(r.ConcessionTypeId, out var ct);

				return new SavedValueEntryByTypeDto
				{
					ConcessionTypeId = r.ConcessionTypeId,
					ConcessionName   = ct?.Name ?? string.Empty,
					FeeHeadValues    = r.ConcessionFeeHeadMappings
						.Select(m => new SavedFeeHeadValueDto
						{
							FeeHeadId = m.FeeHeadId,
							BasedOn   = MapToBasedOnInt(m.UserSpecifiedConcessionAmountType),
							Amount    = m.UserSpecifiedConcessionAmountValue,
							NetAmount = m.PostConcessionFeeHeadAmount,
						})
						.ToList(),
				};
			})
			.ToList();

		var dto = new ConcessionRequestValueEntriesDto
		{
			ConcessionRequestId  = anchor.Id,
			StudentId            = anchor.StudentId,
			ConcessionTypeValues = typeEntries,
		};

		return ApiDataResponseDto<ConcessionRequestValueEntriesDto>.SuccessResult(dto);
	}

	/// <summary>
	/// Step 4 — Persists discount values for every fee head in the student's Draft batch.
	/// </summary>
	public async Task SaveValueEntryAsync(Guid concessionRequestId, ValueEntryDto input)
	{
		var anchor = await concessionRequestRepository.GetAsync(concessionRequestId);

		if (anchor.Status != ConcessionRequestStatus.Draft)
		{
			throw new BusinessException(ConcessionRequestErrorCodes.CannotUpdateNonDraft)
				.WithData("RequestId", concessionRequestId)
				.WithData("Status", anchor.Status.ToString());
		}

		foreach (var block in input.ConcessionTypeValues)
		{
			var duplicateId = block.FeeHeadValues
				.GroupBy(fv => fv.FeeHeadId)
				.FirstOrDefault(g => g.Count() > 1)?.Key;

			if (duplicateId.HasValue)
			{
				throw new BusinessException(ConcessionRequestErrorCodes.DuplicateFeeHeadInConcessionType)
					.WithData("ConcessionTypeId", block.ConcessionTypeId)
					.WithData("FeeHeadId", duplicateId.Value);
			}
		}

		var duplicateConcessionTypeId = input.ConcessionTypeValues
			.GroupBy(b => b.ConcessionTypeId)
			.FirstOrDefault(g => g.Count() > 1)?.Key;

		if (duplicateConcessionTypeId.HasValue)
		{
			throw new BusinessException(ConcessionRequestErrorCodes.DuplicateFeeHeadInConcessionType)
				.WithData("ConcessionTypeId", duplicateConcessionTypeId.Value);
		}

		foreach (var block in input.ConcessionTypeValues)
		{
			foreach (var fv in block.FeeHeadValues)
			{
				var amountType = MapBasedOn(fv.BasedOn);

				if (amountType == AmountType.Percentage && fv.Amount > 100m)
				{
					throw new BusinessException(ConcessionRequestErrorCodes.InvalidPercentageAmount)
						.WithData("FeeHeadId", fv.FeeHeadId)
						.WithData("ConcessionTypeId", block.ConcessionTypeId)
						.WithData("Amount", fv.Amount);
				}

				if (amountType == AmountType.Flat && fv.Amount > fv.AnnualBaseAmount)
				{
					throw new BusinessException(ConcessionRequestErrorCodes.ExcessiveDiscountAmount)
						.WithData("FeeHeadId", fv.FeeHeadId)
						.WithData("ConcessionTypeId", block.ConcessionTypeId)
						.WithData("Amount", fv.Amount)
						.WithData("AnnualBaseAmount", fv.AnnualBaseAmount);
				}
			}
		}

		var batchRequests = await concessionRequestRepository
			.GetBatchWithMappingsAsync(anchor.StudentId, anchor.Status);

		var inputTypeIds = input.ConcessionTypeValues
			.Select(b => b.ConcessionTypeId)
			.ToHashSet();

		foreach (var request in batchRequests)
		{
			if (!inputTypeIds.Contains(request.ConcessionTypeId))
			{
				throw new BusinessException(ConcessionRequestErrorCodes.ConcessionTypeMissingFromValueEntry)
					.WithData("ConcessionTypeId", request.ConcessionTypeId);
			}
		}

		var domainItems = input.ConcessionTypeValues
			.Select(block => new ConcessionTypeValueEntryInput(
				block.ConcessionTypeId,
				block.FeeHeadValues
					.Select(fv => new FeeHeadValueEntryInput(
						fv.FeeHeadId,
						fv.AnnualBaseAmount,
						fv.Amount,
						MapBasedOn(fv.BasedOn)))
					.ToList()))
			.ToList();

		await concessionRequestManager.SaveValueEntryAsync(
			anchor.StudentId,
			anchor.Status,
			domainItems);
	}

	// -------------------------------------------------------------------------
	// Step 5 — Document Upload
	// -------------------------------------------------------------------------

	/// <summary>
	/// Step 5 (Edit / Resume) — Returns all document template slots per concession type,
	/// merged with previously saved upload data.
	/// </summary>
	public virtual async Task<ApiDataResponseDto<ConcessionRequestDocumentsDto>> GetDocumentUploadsAsync(
		Guid concessionRequestId)
	{
		var anchor = await concessionRequestRepository.GetAsync(concessionRequestId);

		var batchRequests = await concessionRequestRepository
			.GetBatchWithDocumentsAsync(anchor.StudentId, anchor.Status);

		var concessionTypeIds = batchRequests.Select(r => r.ConcessionTypeId).ToList();
		var concessionTypes = await concessionTypeRepository
			.GetListAsync(ct => concessionTypeIds.Contains(ct.Id));

		var concessionTypeById = concessionTypes.ToDictionary(ct => ct.Id);

		var typeDocuments = batchRequests
			.Select(r =>
			{
				concessionTypeById.TryGetValue(r.ConcessionTypeId, out var ct);

				var uploadByDocumentId = r.ConcessionRequestDocuments
					.ToDictionary(d => d.DocumentId);

				var slots = (ct?.ConcessionTypeDocuments ?? Enumerable.Empty<ConcessionTypeDocument>())
					.Select(template =>
					{
						uploadByDocumentId.TryGetValue(template.Id, out var upload);

						return new DocumentSlotDto
						{
							DocumentId     = template.Id,
							DocumentName   = template.DocumentName,
							AcceptedFormat = template.AcceptedFormats
							                         .Select(f => (int)f.DocumentType)
							                         .ToList(),
							Url            = upload?.Url,
							Status         = upload is not null ? (int)upload.Status : 0,
							UploadedAt     = upload is not null
							                 ? upload.LastModificationTime ?? upload.CreationTime
							                 : null,
						};
					})
					.ToList();

				return new DocumentsByTypeDto
				{
					ConcessionTypeId   = r.ConcessionTypeId,
					ConcessionTypeName = ct?.Name ?? string.Empty,
					Documents          = slots,
				};
			})
			.ToList();

		var dto = new ConcessionRequestDocumentsDto
		{
			ConcessionRequestId     = anchor.Id,
			StudentId               = anchor.StudentId,
			ConcessionTypeDocuments = typeDocuments,
		};

		return ApiDataResponseDto<ConcessionRequestDocumentsDto>.SuccessResult(dto);
	}

	/// <summary>
	/// Step 5 — Persists document uploads for every concession type in the student's
	/// Draft batch. Full replacement per concession type.
	/// </summary>
	public async Task SaveDocumentUploadAsync(Guid concessionRequestId, UploadDocumentsDto input)
	{
		var anchor = await concessionRequestRepository.GetAsync(concessionRequestId);

		if (anchor.Status != ConcessionRequestStatus.Draft)
		{
			throw new BusinessException(ConcessionRequestErrorCodes.CannotUploadDocumentsForNonDraft)
				.WithData("RequestId", concessionRequestId)
				.WithData("Status", anchor.Status.ToString());
		}

		foreach (var block in input.ConcessionTypeDocuments)
		{
			var duplicateId = block.Documents
				.GroupBy(d => d.DocumentId)
				.FirstOrDefault(g => g.Count() > 1)?.Key;

			if (duplicateId.HasValue)
			{
				throw new BusinessException(ConcessionRequestErrorCodes.DuplicateDocumentInConcessionType)
					.WithData("ConcessionTypeId", block.ConcessionTypeId)
					.WithData("DocumentId", duplicateId.Value);
			}
		}

		var batchRequests = await concessionRequestRepository
			.GetBatchWithDocumentsAsync(anchor.StudentId, anchor.Status);

		var inputTypeIds = input.ConcessionTypeDocuments
			.Select(b => b.ConcessionTypeId)
			.ToHashSet();

		foreach (var request in batchRequests)
		{
			if (!inputTypeIds.Contains(request.ConcessionTypeId))
			{
				throw new BusinessException(ConcessionRequestErrorCodes.ConcessionTypeMissingFromDocumentUpload)
					.WithData("ConcessionTypeId", request.ConcessionTypeId);
			}
		}

		var domainItems = input.ConcessionTypeDocuments
			.Select(block => new ConcessionTypeDocumentUploadInput(
				block.ConcessionTypeId,
				block.Documents
					.Select(d => new DocumentUploadInput(
						d.DocumentId,
						d.Url,
						(DocumentUploadStatus)d.Status))
					.ToList()))
			.ToList();

		await concessionRequestManager.SaveDocumentUploadAsync(
			CurrentTenant.Id,
			anchor.StudentId,
			anchor.Status,
			domainItems);
	}

	// -------------------------------------------------------------------------
	// Step 6 — Description
	// -------------------------------------------------------------------------

	/// <summary>
	/// Step 6 — Saves an optional free-text description against a Draft request.
	/// </summary>
	public async Task SaveDescriptionAsync(Guid concessionRequestId, SaveDescriptionDto input)
	{
		var entity = await concessionRequestRepository.GetAsync(concessionRequestId);
		entity.SetDescription(input.Description);
		await concessionRequestRepository.UpdateAsync(entity);
	}

	// -------------------------------------------------------------------------
	// Private helpers
	// -------------------------------------------------------------------------

	/// <summary>
	/// Maps the domain <see cref="AmountType"/> to the API string discriminator
	/// ("PERCENTAGE" / "FLAT" / <c>null</c>).
	/// </summary>
	private static string? MapDiscountType(AmountType? discountType) => discountType switch
	{
		AmountType.Percentage => "PERCENTAGE",
		AmountType.Flat       => "FLAT",
		_                     => null,
	};

	/// <summary>
	/// Maps the Step 4 <c>basedOn</c> integer to the domain <see cref="AmountType"/>.
	/// 0 = Flat (amount-based), 1 = Percentage.
	/// </summary>
	private static AmountType MapBasedOn(int basedOn) => basedOn switch
	{
		0 => AmountType.Flat,
		1 => AmountType.Percentage,
		_ => throw new BusinessException(ConcessionRequestErrorCodes.InvalidBasedOnValue)
		         .WithData("BasedOn", basedOn),
	};

	/// <summary>
	/// Inverse of <see cref="MapBasedOn"/> — converts domain <see cref="AmountType"/>
	/// back to the Step 4 GET integer discriminator.
	/// </summary>
	private static int MapToBasedOnInt(AmountType amountType) => amountType switch
	{
		AmountType.Flat       => 0,
		AmountType.Percentage => 1,
		_                     => 0,
	};
}
