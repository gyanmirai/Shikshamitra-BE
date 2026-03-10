using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.Finance.ConcessionRequests;

[RemoteService(false)]
[Authorize]
public class ConcessionRequestAppService : ShikshaMitraAppService, IConcessionRequestAppService
{
    private readonly IConcessionRequestRepository _concessionrequestRepository;

    public ConcessionRequestAppService(IConcessionRequestRepository concessionrequestRepository)
    {
        _concessionrequestRepository = concessionrequestRepository;
    }

    public virtual async Task<ConcessionRequestDto> GetAsync(Guid id)
    {
        var entity = await _concessionrequestRepository.GetAsync(id);
        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(entity);
    }

    public virtual async Task<PagedResultDto<ConcessionRequestDto>> GetListAsync(GetConcessionRequestListRequestDto input)
    {
        var queryable = await _concessionrequestRepository.GetQueryableAsync();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            // queryable = queryable.Where(x => x.Name.Contains(input.Filter)); // Example filter
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);

        var list = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(input.Sorting ?? "Id")
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
        );

        return new PagedResultDto<ConcessionRequestDto>(
            totalCount,
            ObjectMapper.Map<List<ConcessionRequest>, List<ConcessionRequestDto>>(list)
        );
    }

    public virtual async Task<ConcessionRequestDto> CreateAsync(CreateUpdateConcessionRequestDto input)
    {
        var entity = ObjectMapper.Map<CreateUpdateConcessionRequestDto, ConcessionRequest>(input);
        entity = await _concessionrequestRepository.InsertAsync(entity);
        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(entity);
    }

    public virtual async Task<ConcessionRequestDto> UpdateAsync(Guid id, CreateUpdateConcessionRequestDto input)
    {
        var entity = await _concessionrequestRepository.GetAsync(id);
        ObjectMapper.Map(input, entity);
        entity = await _concessionrequestRepository.UpdateAsync(entity);
        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(entity);
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        await _concessionrequestRepository.DeleteAsync(id);
    }
}
