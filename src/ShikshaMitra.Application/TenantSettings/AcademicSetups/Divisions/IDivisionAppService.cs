using Microsoft.AspNetCore.Authorization;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Onboarding.Divisions;

[Authorize]
public class DivisionAppService : CrudAppService<
        Division,
        DivisionDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateDivisionDto>,
    IDivisionAppService
{
    public DivisionAppService(IRepository<Division, Guid> repository)
        : base(repository)
    {
    }
}