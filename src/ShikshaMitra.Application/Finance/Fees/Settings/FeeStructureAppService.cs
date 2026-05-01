using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Setting;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeStructureAppService(
	IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
	IRepository<ReceiptBook, Guid> receiptBookRepository,
	IAcademicYearRepository academicYearRepository,
	IEducationBoardRepository educationBoardRepository,
	IEducationMediumRepository educationMediumRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IGuidGenerator guidGenerator
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
		Guid feeStructureMetadataId,
		StandardFeeStructureSaveRequest input)
	{
		var metadata = await feeStructureMetadataRepository.GetAsync(feeStructureMetadataId) ?? throw new BusinessException(message: "Fee structure metadata not found.");

		// validate that standard's medium is same as fee structure metadata's medium
		var standard = await standardRepository.GetAsync(input.StandardId) ?? throw new BusinessException(message: "Standard not found.");

		if (standard.EducationMediumStage.EducationMediumId != metadata.AcademicMediumId)
			throw new BusinessException(message: "Standard's medium does not match the fee structure metadata's medium.");

		// delete existing fee structures for the standard + fee structure metadata combination
		var existingFeeStructures = await feeStructureRepository.GetListAsync(x => x.StandardId == input.StandardId && x.FeeStructureMetadataId == feeStructureMetadataId);
		if (existingFeeStructures.Any())
		{
			foreach (var existingFeeStructure in existingFeeStructures)
			{
				await feeStructureRepository.DeleteAsync(existingFeeStructure);
			}
		}

		// create object for each standard fee structure and save
		foreach (var standardFeeStructure in input.StandardFeeStructures)
		{
			var feeStructure = new FeeStructure(
				guidGenerator.Create(),
				CurrentTenant.Id,
				metadata.Id,
				input.StandardId,
				standardFeeStructure.Name);

			foreach (var feeComponent in standardFeeStructure.FeeHeads)
			{
				feeStructure.AddDetail(new FeeStructureDetail(
					guidGenerator.Create(),
					CurrentTenant.Id,
					feeStructure.Id,
					feeComponent.FeeHeadId,
					feeComponent.Amount,
					feeComponent.IsOneTime,
					feeComponent.IsOptional));
			}

			await feeStructureRepository.InsertAsync(feeStructure);
		}
	}

	// ── Get standard fee structure ────────────────────────────────────────────

	public async Task<StandardFeeStructureResponse> GetStandardFeeStructureAsync(Guid standardId)
	{
		var standard = await standardRepository.GetAsync(standardId) ?? throw new BusinessException(message: "Standard not found.");

		var feeStructures = await feeStructureRepository.GetListAsync(x => x.StandardId == standardId);

		var response = new StandardFeeStructureResponse
		{
			StandardId = standardId,
			StandardName = standard.Name,
			StandardFeeStructures = feeStructures.Select(fs => new StandardFeeStructureResponseItem
			{
				Id = fs.Id,
				Name = fs.Name,
				FeeHeads = fs.FeeStructureDetails.Select(fd => new FeeHeadAssignmentResponse
				{
					FeeHeadId = fd.FeeHeadId,
					FeeHeadName = fd.FeeHead.Name,
					CategoryName = fd.FeeHead.FeeCategory.Name,
					Amount = fd.Amount,
					IsOneTime = fd.IsOneTime,
					IsOptional = fd.IsOptional
				}).ToList()
			}).ToList()
		};

		return response;
	}

	public async Task GenerateFeeStructuresAsync(Guid academicYearId)
	{
		// validate that academic year exists
		var academicYear = await academicYearRepository.GetAsync(academicYearId);

		// for each board + medium combination, generate fee structure if not already generated
		var educationBoards = await educationBoardRepository.GetListAsync(x => x.AcademicYearId == academicYearId);

		foreach (var board in educationBoards)
		{
			foreach (var medium in board.EducationMediums)
			{
				var existingMetadata = await feeStructureMetadataRepository.FirstOrDefaultAsync(x => x.AcademicMediumId == medium.Id && x.AcademicYearId == academicYearId && x.AcademicBoardId == board.Id);
				if (existingMetadata != null)
					continue;
				var metadata = new FeeStructureMetadata(
					guidGenerator.Create(),
					CurrentTenant.Id,
					academicYearId,
					board.Id,
					medium.Id,
					null,
					name: $"{academicYear.Name} - {board.Type.ToString()} - {medium.Name}");
				await feeStructureMetadataRepository.InsertAsync(metadata, autoSave: true);
			}
		}
	}
}
