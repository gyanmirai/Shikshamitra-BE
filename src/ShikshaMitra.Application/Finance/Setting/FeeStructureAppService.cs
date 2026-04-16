using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeStructure;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeStructure.Dtos;
using ShikshaMitra.Finance.Setting.FeeStructures;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

using FeeStructureMetadataStatus = ShikshaMitra.Finance.Setting.FeeStructure.FeeStructureMetadataStatus;

namespace ShikshaMitra.Finance.Setting;

[Authorize]
[RemoteService(false)]
public class FeeStructureAppService(
	IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
	IRepository<ReceiptBook, Guid> receiptBookRepository,
	IAcademicYearRepository academicYearRepository,
	IEducationBoardRepository educationBoardRepository,
	IEducationMediumRepository educationMediumRepository,
	IGuidGenerator guidGenerator,
	FeeStructureDomainService feeStructureDomainService
) : ApplicationService, IFeeStructureAppService
{
	// ── Create ────────────────────────────────────────────────────────────────

	public async Task<FeeStructureDetailsResponse> CreateAsync(
		Guid academicYearId,
		FeeStructureCreateRequest input)
	{
		// validate that FeeStructure Metadata is not already created for the same academic year + board + medium combination
		var existingMetadata = await feeStructureMetadataRepository.FirstOrDefaultAsync(x => x.AcademicMediumId == input.MediumId && x.AcademicYearId == academicYearId && x.AcademicBoardId == input.BoardId);
		if (existingMetadata != null)
		{
			throw new Exception(message: "Fee structure metadata already exists for the specified academic year, board, and medium combination.");
		}

		var metadata = new FeeStructureMetadata(
			guidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.BoardId,
			input.MediumId,
			input.ReceiptBookId,
			input.Name);

		var result = await feeStructureMetadataRepository.InsertAsync(metadata, autoSave: true);

		return result.ToDetailsResponse();
	}

	// ── List ──────────────────────────────────────────────────────────────────

	public async Task<FeeStructureListResponse> GetListAsync(
		Guid academicYearId,
		int page = 1,
		int pageSize = 10,
		string? search = null)
	{
		var query = await feeStructureMetadataRepository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId);

		if (!string.IsNullOrWhiteSpace(search))
			query = query.Where(x => x.Name != null && x.Name.Contains(search));

		var totalCount = await AsyncExecuter.CountAsync(query);

		query = query
			.OrderByDescending(x => x.CreationTime)
			.Skip((page - 1) * pageSize)
			.Take(pageSize);

		var items = await AsyncExecuter.ToListAsync(query);

		if (!items.Any())
			return new FeeStructureListResponse
			{
				TotalCount = totalCount,
				CurrentPage = page,
				PageSize = pageSize,
				TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
				Items = new List<FeeStructureDetailsResponse>()
			};

		var responseItems = items.ToDetailsResponseList();

		return new FeeStructureListResponse
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			Items = responseItems
		};
	}

	// ── Update status ─────────────────────────────────────────────────────────

	public async Task UpdateStatusAsync(
		Guid feeStructureId,
		FeeStructureStatusUpdateRequest input)
	{
		if (!Enum.IsDefined(typeof(FeeStructureMetadataStatus), input.Status))
			throw new BusinessException(message: $"Invalid fee structure status value: {input.Status}");

		var metadata = await feeStructureMetadataRepository.GetAsync(feeStructureId);

		if (input.Status == FeeStructureMetadataStatus.Locked)
			metadata.Lock();
		else
			metadata.Unlock();

		await feeStructureMetadataRepository.UpdateAsync(metadata);
	}

	// ── Update metadata ───────────────────────────────────────────────────────

	public async Task<MessageResponse> UpdateAsync(
		Guid feeStructureId,
		FeeStructureUpdateRequest input)
	{
		var metadata = await feeStructureMetadataRepository.GetAsync(feeStructureId);
		metadata.Update(input.Name, input.BoardId, input.MediumId, input.ReceiptBookId);

		await feeStructureMetadataRepository.UpdateAsync(metadata);

		return new MessageResponse { Message = "Fee structure updated successfully." };
	}

	// ── Save per-standard fee structures ──────────────────────────────────────

	public async Task SaveStandardFeeStructuresAsync(
		Guid feeStructureId,
		StandardFeeStructureSaveRequest input)
	{
		var metadata = await feeStructureMetadataRepository.GetAsync(feeStructureId);

		var plans = input.StandardFeeStructures?
			.Select(s => new StandardFeeStructurePlan(
				s.Name,
				s.FeeHeads?
					.Select(fh => new FeeHeadEntry(fh.FeeHeadId, (double)fh.Amount, fh.IsOneTime, fh.IsOptional))
					.ToList() ?? []))
			.ToList() ?? [];

		await feeStructureDomainService.SaveStandardFeeStructuresAsync(
			metadata,
			input.StandardId,
			CurrentTenant.Id,
			plans);
	}

	// ── Get standard fee structure ────────────────────────────────────────────

	public async Task<StandardFeeStructureResponse> GetStandardFeeStructureAsync(Guid standardId) =>
		(await feeStructureDomainService.GetStandardFeeStructuresAsync(standardId)).ToResponse();
}
