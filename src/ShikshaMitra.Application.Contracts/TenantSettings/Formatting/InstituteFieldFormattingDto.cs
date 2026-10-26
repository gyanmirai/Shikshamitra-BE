using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.Formatting;

public class InstituteFieldFormattingDto
{
	public Dictionary<string, AcademicTextCase> Fields { get; set; } = new();
}

public class InstituteNamePreviewInput
{
	public const int MaxValueLength = 2000;
	[Required(AllowEmptyStrings = true), StringLength(MaxValueLength)]
	public string Value { get; set; } = "";
	[EnumDataType(typeof(AcademicTextCase))]
	public AcademicTextCase TextCase { get; set; }
}

public interface IInstituteFieldFormattingAppService : IApplicationService
{
	Task<InstituteFieldFormattingDto> GetAsync(string scope);
	Task<InstituteFieldFormattingDto> UpdateAsync(string scope, InstituteFieldFormattingDto input);
	Task<AcademicNamePreviewDto> PreviewAsync(string scope, InstituteNamePreviewInput input);
}
