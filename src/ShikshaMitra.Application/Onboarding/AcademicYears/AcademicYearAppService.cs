using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Onboarding.AcademicYears;

public class AcademicYearAppService :
    CrudAppService<AcademicYear, AcademicYearDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateAcademicYearDto>, IAcademicYearsAppService
{
    public AcademicYearAppService(IRepository<AcademicYear, Guid> repository) : base(repository)
    {
    }
}
