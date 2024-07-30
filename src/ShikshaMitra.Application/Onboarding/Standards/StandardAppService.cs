using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Onboarding.Standards;

public class StandardAppService : CrudAppService<
    Standard,
    StandardDto,
    Guid,
    PagedAndSortedResultRequestDto,
    CreateUpdateStandardDto>, IStandardAppService
{
    public StandardAppService(IRepository<Standard, Guid> repository) : base(repository)
    {
    }
}