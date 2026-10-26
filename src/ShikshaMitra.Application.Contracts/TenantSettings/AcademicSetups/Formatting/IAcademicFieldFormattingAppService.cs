using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Formatting;

public interface IAcademicFieldFormattingAppService : IApplicationService
{
	Task<AcademicFieldFormattingDto> GetAsync();
	Task<AcademicNamePreviewDto> PreviewAsync(AcademicNamePreviewInput input);
	Task<AcademicFieldFormattingDto> UpdateAsync(AcademicFieldFormattingDto input);
}
