using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups.AcademicOrganizationOptions;

public interface IAcademicOrganizationOptionsAppService : IApplicationService
{
	Task<ListResultDto<AcademicOrganizationOptionDto>> GetListAsync(
		AcademicOrganizationOptionsInput input);
}
