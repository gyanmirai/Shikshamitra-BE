using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.ChargeCategory.Dtos;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Setting.ChargeCategory;

[RemoteService(false)]
public class LibraryChargeCategoryAppService(IRepository<LibraryChargeCategory, Guid> _repository)
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

	public async Task<GenericCreatedResponse> CreateAsync(Guid academicYearId, LibraryChargeCategoryCreateRequest input)
	{
		var query = await _repository.GetQueryableAsync();
		var exists = query.Any(x => x.ChargeType == input.ChargeType && x.AcademicYearId == academicYearId);
		if (exists)
		{
			throw new UserFriendlyException("Charge type already exists.", code: "409");
		}

		var entity = new LibraryChargeCategory(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.ChargeType,
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

	public async Task<GenericUpdatedResponse> UpdateAsync(Guid categoryId, LibraryChargeCategoryUpdateRequest input)
	{
		var entity = await _repository.GetAsync(categoryId);

		entity.ChargeType = input.ChargeType;
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

	public async Task DeleteAsync(Guid categoryId)
	{
		await _repository.DeleteAsync(categoryId, autoSave: true);
	}
}
