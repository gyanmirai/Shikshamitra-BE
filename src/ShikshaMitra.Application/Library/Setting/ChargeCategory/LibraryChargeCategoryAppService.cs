using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory.Dtos;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Setting.ChargeCategory;

[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_ChargesCategories.View)]
[RemoteService(false)]
public class LibraryChargeCategoryAppService(
	IRepository<LibraryChargeCategory, Guid> _repository,
	IRepository<LibraryCharge, Guid> _chargeRepository)
	: ApplicationService, ILibraryChargeCategoryAppService
{
	public async Task<PaginatedLibraryChargeCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10)
	{
		var query = await _repository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderByDescending(x => x.CreationTime)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
		);

		return new PaginatedLibraryChargeCategoryResponse
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = ObjectMapper.Map<List<LibraryChargeCategory>, List<LibraryChargeCategoryDto>>(items)
		};
	}

	public async Task<LibraryChargeCategoryDto> GetAsync(Guid categoryId)
	{
		var entity = await _repository.GetAsync(categoryId);
		return ObjectMapper.Map<LibraryChargeCategory, LibraryChargeCategoryDto>(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_ChargesCategories.Add)]
	public async Task<GenericCreatedResponse> CreateAsync(Guid academicYearId, LibraryChargeCategoryCreateRequest input)
	{
		var query = await _repository.GetQueryableAsync();
		var normalizedChargeType = input.ChargeType.Trim().ToLower();
		var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.ChargeType.ToLower() == normalizedChargeType && x.AcademicYearId == academicYearId));
		if (exists)
		{
			throw LibraryException(LibraryErrorCodes.ChargeCategoryAlreadyExists, "Charge type already exists.");
		}

		var entity = new LibraryChargeCategory(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.ChargeType.Trim(),
			input.Amount,
			input.IsFixed,
			input.Description
		);

		await _repository.InsertAsync(entity, autoSave: true);

		return new GenericCreatedResponse
		{
			Id = entity.Id.ToString(),
			Message = "Charge category created successfully."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_ChargesCategories.Add)]
	public async Task<GenericUpdatedResponse> UpdateAsync(Guid categoryId, LibraryChargeCategoryUpdateRequest input)
	{
		var entity = await _repository.GetAsync(categoryId);
		var query = await _repository.GetQueryableAsync();
		var normalizedChargeType = input.ChargeType.Trim().ToLower();
		var exists = await AsyncExecuter.AnyAsync(query.Where(x =>
			x.Id != categoryId &&
			x.AcademicYearId == entity.AcademicYearId &&
			x.ChargeType.ToLower() == normalizedChargeType));
		if (exists)
		{
			throw LibraryException(LibraryErrorCodes.ChargeCategoryAlreadyExists, "Charge type already exists.");
		}

		entity.ChargeType = input.ChargeType.Trim();
		entity.Amount = input.Amount;
		entity.IsFixed = input.IsFixed;
		entity.Description = input.Description;

		await _repository.UpdateAsync(entity, autoSave: true);

		return new GenericUpdatedResponse
		{
			Id = entity.Id.ToString(),
			Message = "Charge category updated successfully."
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_ChargesCategories.Delete)]
	public async Task DeleteAsync(Guid categoryId)
	{
		var chargeQuery = await _chargeRepository.GetQueryableAsync();
		var isInUse = await AsyncExecuter.AnyAsync(chargeQuery.Where(x => x.ChargeCategoryId == categoryId));
		if (isInUse)
		{
			throw LibraryException(LibraryErrorCodes.ChargeCategoryInUse, "Cannot delete a charge category that is used by charges.");
		}

		await _repository.DeleteAsync(categoryId, autoSave: true);
	}

	private static BusinessException LibraryException(string code, string message)
	{
		return new BusinessException(code: code, message: message);
	}
}
