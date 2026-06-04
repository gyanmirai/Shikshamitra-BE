using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Settings.ChargeCategories;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Settings;

[RemoteService(false)]
public class ChargeCategoryAppService(
	IRepository<ChargeCategory, Guid> chargeCategoryRepository,
	IRepository<Charge, Guid> chargeRepository
) : ApplicationService, IChargeCategoryAppService
{
    public async Task<ChargeCategoryResponse> CreateAsync(Guid academicYearId, CreateChargeCategoryRequest input)
    {
		// Check for duplicate name within the same academic year
		var existingCategory = await chargeCategoryRepository.AnyAsync(x => x.AcademicYearId == academicYearId && x.Name == input.Name);

		if (existingCategory)
		{
			throw new BusinessException("A charge category with the same name already exists for this academic year.");
		}

		var chargeCategory = new ChargeCategory(
            GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
            input.Name,
            input.IsApprovalRequired,
            input.Description
        );

        await chargeCategoryRepository.InsertAsync(chargeCategory);

        return ObjectMapper.Map<ChargeCategory, ChargeCategoryResponse>(chargeCategory);
    }

    public async Task<PaginatedChargeCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null)
    {
        var query = await chargeCategoryRepository.GetQueryableAsync();

        query = query.Where(x => x.AcademicYearId == academicYearId);

        if (!search.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.Name.Contains(search));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);

        var items = await AsyncExecuter.ToListAsync(
            query
            .OrderByDescending(x => x.CreationTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
        );

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedChargeCategoryResponse
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Items = ObjectMapper.Map<List<ChargeCategory>, List<ChargeCategoryResponse>>(items)
        };
    }

    public async Task<ChargeCategoryResponse> GetAsync(Guid id)
    {
        var chargeCategory = await chargeCategoryRepository.GetAsync(id);
        return ObjectMapper.Map<ChargeCategory, ChargeCategoryResponse>(chargeCategory);
    }

    public async Task<ChargeCategoryResponse> UpdateAsync(Guid id, UpdateChargeCategoryRequest input)
    {
        var chargeCategory = await chargeCategoryRepository.GetAsync(id);

        chargeCategory.Name = input.Name;
        if (input.IsApprovalRequired.HasValue)
        {
            chargeCategory.IsApprovalRequired = input.IsApprovalRequired.Value;
        }
        chargeCategory.Description = input.Description;

		// validate for duplicate name within the same academic year
		var existingCategory = await chargeCategoryRepository.AnyAsync(x => x.AcademicYearId == chargeCategory.AcademicYearId && x.Name == input.Name && x.Id != id);

		if (existingCategory)
		{
			throw new BusinessException("A charge category with the same name already exists for this academic year.");
		}

		await chargeCategoryRepository.UpdateAsync(chargeCategory);

        return ObjectMapper.Map<ChargeCategory, ChargeCategoryResponse>(chargeCategory);
    }

    public async Task DeleteAsync(Guid id)
    {
		var existingCharge = await chargeRepository.AnyAsync(x => x.ChargeCategoryId == id);

		if (existingCharge)
		{
			throw new BusinessException("Cannot delete charge category as it is being used in existing charges.");
		}

		await chargeCategoryRepository.DeleteAsync(id);
    }
}
