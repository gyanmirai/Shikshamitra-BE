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
public partial class FeeStructureMetadataAppService(
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
	public async Task<FeeStructureMetadataListResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null, string? filters = null)
	{
		var tableQuery = ListFilter.Parse(filters);
		if (page < 1 || pageSize < 1 || pageSize > 100) throw new BusinessException(FeeStructureErrorCodes.InvalidConfiguration);
		var scope = await scopeAccess.GetCurrentUserScopeAsync();
		var query = await feeStructureMetadataRepository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId);

		if (!string.IsNullOrWhiteSpace(search))
			query = query.Where(x => x.Name != null && x.Name.Contains(search));
		query = tableQuery.Apply(query, MetadataSqlFields, column => column == "name");
		return await GetVisiblePageAsync(query, page, pageSize, scope, tableQuery);
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
