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
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Standards;

namespace ShikshaMitra.Finance.Fees.Settings;

[Authorize]
[RemoteService(false)]
public class FeeStructureMetadataAppService(
	IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IAcademicYearRepository academicYearRepository,
	IEducationBoardRepository educationBoardRepository,
	IStandardRepository standardRepository,
	IGuidGenerator guidGenerator,
	IUserScopeAccessService scopeAccess
) : ApplicationService, IFeeStructureMetadataAppService
{
	private sealed record MetadataScope(Guid? AcademicYearId, Guid? EducationBoardId, Guid? EducationMediumId,
		Guid? EducationMediumStageId, Guid? StandardId, Guid? DivisionId = null) : IUserScopedEntity;

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.View)]
	public async Task<FeeStructureMetadataListResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null)
	{
		if (page < 1 || pageSize < 1 || pageSize > 100) throw new BusinessException(FeeStructureErrorCodes.InvalidConfiguration);
		var scope = await scopeAccess.GetCurrentUserScopeAsync();
		var query = await feeStructureMetadataRepository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId);

		if (!string.IsNullOrWhiteSpace(search))
			query = query.Where(x => x.Name != null && x.Name.Contains(search));

		// A metadata row is a parent container. Reveal it only when at least one child class is accessible.
		// Project scalars instead of materializing the recursively auto-included academic graph.
		var all = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).Select(x => new FeeStructureDetailsResponse
		{
			Id = x.Id, Name = x.Name, AcademicYearId = x.AcademicYearId!.Value, AcademicYearName = x.AcademicYear.Name,
			BoardId = x.AcademicBoardId!.Value, BoardName = x.AcademicBoard.Type.ToString(),
			MediumId = x.AcademicMediumId!.Value, MediumName = x.AcademicMedium.Name,
			ReceiptBookId = x.ReceiptBookId, ReceiptBookName = x.ReceiptBook == null ? null : x.ReceiptBook.Name
		}));
		var mediumIds = all.Select(x => x.MediumId).Distinct().ToList();
		var classRows = await AsyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync())
			.Where(x => mediumIds.Contains(x.EducationMediumStage.EducationMediumId)).Select(standard => new
			{
				MediumId = standard.EducationMediumStage.EducationMediumId,
				StageId = standard.EducationMediumStageId, StandardId = standard.Id, standard.Name, standard.AgeLowerLimit
			}));
		var accessible = all.ToDictionary(x => x.Id, x => StandardAcademicOrder.Sort(
			classRows.Where(c => c.MediumId == x.MediumId &&
				UserScopeQueryExtensions.HasAccessToEntity(new MetadataScope(x.AcademicYearId, x.BoardId, x.MediumId, c.StageId, c.StandardId), scope)),
			c => c.Name, c => c.AgeLowerLimit, c => c.StandardId)
			.Select(c => new FeeStructureClassOptionDto { Id = c.StandardId, Name = c.Name }).ToList());
		var visible = all.Where(x => accessible[x.Id].Count > 0).ToList();
		var totalCount = visible.Count;
		var items = visible.Skip((page - 1) * pageSize).Take(pageSize).ToList();

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
			(await feeStructureRepository.GetQueryableAsync())
				.Where(x => metadataIds.Contains(x.FeeStructureMetadataId) && x.FeeStructureDetails.Any())
				.Select(x => new { x.FeeStructureMetadataId, x.StandardId }));

		var responseItems = new List<FeeStructureDetailsResponse>();

		// I want to calculate total classes and configured classes for each metadata
		foreach (var metadata in items)
		{
			var structuresForMetadata = feeStructures.Where(x => x.FeeStructureMetadataId == metadata.Id).ToList();
			var classes = accessible[metadata.Id];
			var configuredClasses = structuresForMetadata.Where(x => classes.Any(c => c.Id == x.StandardId)).Select(x => x.StandardId).Distinct().Count();
			var item = metadata;
			item.TotalClasses = classes.Count;
			item.ConfiguredClasses = configuredClasses;
			item.Classes = classes;
			responseItems.Add(item);
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

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add)]
	public async Task GenerateFeeStructuresAsync(Guid academicYearId)
	{
		var scope = await scopeAccess.GetCurrentUserScopeAsync();
		scopeAccess.EnsureHasAnyAccess(scope);
		// validate that academic year exists
		var academicYear = await academicYearRepository.GetAsync(academicYearId);

		// for each board + medium combination, generate fee structure if not already generated
		var educationBoards = await AsyncExecuter.ToListAsync((await educationBoardRepository.GetQueryableAsync())
			.Where(x => x.AcademicYearId == academicYearId).SelectMany(board => board.EducationMediums, (board, medium) => new
			{
				BoardId = board.Id, board.Type, MediumId = medium.Id, MediumName = medium.Name
			}));

		foreach (var board in educationBoards)
		{
				if (!UserScopeQueryExtensions.HasAccessToEntity(new MetadataScope(academicYearId, board.BoardId, board.MediumId, null, null), scope)) continue;
				var exists = await feeStructureMetadataRepository.AnyAsync(x => x.AcademicMediumId == board.MediumId && x.AcademicYearId == academicYearId && x.AcademicBoardId == board.BoardId);
				if (exists)
					continue;
				var metadata = new FeeStructureMetadata(
					guidGenerator.Create(),
					CurrentTenant.Id,
					academicYearId,
					board.BoardId,
					board.MediumId,
					null,
					name: $"{academicYear.Name} - {board.Type.ToString()} - {board.MediumName}");
				await feeStructureMetadataRepository.InsertAsync(metadata, autoSave: true);
		}
	}
}
