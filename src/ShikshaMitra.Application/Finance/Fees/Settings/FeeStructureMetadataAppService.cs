using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
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

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeStructureMetadataAppService(
	IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IAcademicYearRepository academicYearRepository,
	IEducationBoardRepository educationBoardRepository,
	IGuidGenerator guidGenerator
) : ApplicationService, IFeeStructureMetadataAppService
{
	public async Task<FeeStructureMetadataListResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null)
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
			return new FeeStructureMetadataListResponse
			{
				TotalCount = totalCount,
				CurrentPage = page,
				PageSize = pageSize,
				TotalPages = (int) Math.Ceiling(totalCount / (double) pageSize),
				Items = new List<FeeStructureDetailsResponse>()
			};

		var metadataIds = items.Select(x => x.Id).ToList();

		var feeStructures = await AsyncExecuter.ToListAsync(
			(await feeStructureRepository.WithDetailsAsync(x => x.FeeStructureDetails))
				.Where(x => metadataIds.Contains(x.FeeStructureMetadataId)));

		var responseItems = new List<FeeStructureDetailsResponse>();

		// I want to calculate total classes and configured classes for each metadata
		foreach (var metadata in items)
		{
			var structuresForMetadata = feeStructures.Where(x => x.FeeStructureMetadataId == metadata.Id).ToList();
			var totalClasses = metadata.AcademicMedium.EducationMediumStages.Sum(x => x.Standards.Count);
			var configuredClasses = structuresForMetadata.Where(x => x.FeeStructureDetails.Any()).Select(x => x.StandardId).Distinct().Count();

			responseItems.Add(metadata.ToDetailsResponse(totalClasses, configuredClasses));
		}

		return new FeeStructureMetadataListResponse
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = (int) Math.Ceiling(totalCount / (double) pageSize),
			Items = responseItems
		};
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
