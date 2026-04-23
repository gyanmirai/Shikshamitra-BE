using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.ChargeCategories;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Finance.Setting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Settings;
[RemoteService(false)]
public class ChargeCategoryAppService : ApplicationService, IChargeCategoryAppService
{
    private readonly IRepository<ChargeCategory, Guid> _chargeCategoryRepository;

    public ChargeCategoryAppService(IRepository<ChargeCategory, Guid> chargeCategoryRepository)
    {
        _chargeCategoryRepository = chargeCategoryRepository;
    }

    public async Task<ChargeCategoryResponse> CreateAsync(Guid academicYearId, CreateChargeCategoryRequest input)
    {
        var chargeCategory = new ChargeCategory(
            GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
            input.Name,
            input.IsApprovalRequired,
            input.Description
        );

        await _chargeCategoryRepository.InsertAsync(chargeCategory);

        return ObjectMapper.Map<ChargeCategory, ChargeCategoryResponse>(chargeCategory);
    }

    public async Task<PaginatedChargeCategoryResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null)
    {
        var query = await _chargeCategoryRepository.GetQueryableAsync();

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
        var chargeCategory = await _chargeCategoryRepository.GetAsync(id);
        return ObjectMapper.Map<ChargeCategory, ChargeCategoryResponse>(chargeCategory);
    }

    public async Task<ChargeCategoryResponse> UpdateAsync(Guid id, UpdateChargeCategoryRequest input)
    {
        var chargeCategory = await _chargeCategoryRepository.GetAsync(id);

        chargeCategory.Name = input.Name;
        if (input.IsApprovalRequired.HasValue)
        {
            chargeCategory.IsApprovalRequired = input.IsApprovalRequired.Value;
        }
        chargeCategory.Description = input.Description;

        await _chargeCategoryRepository.UpdateAsync(chargeCategory);

        return ObjectMapper.Map<ChargeCategory, ChargeCategoryResponse>(chargeCategory);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _chargeCategoryRepository.DeleteAsync(id);
    }
}
