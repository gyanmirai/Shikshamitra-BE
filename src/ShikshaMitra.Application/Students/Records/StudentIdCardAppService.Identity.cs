using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.SchoolIdentity;

namespace ShikshaMitra.Students.Records;

public partial class StudentIdCardAppService
{
	private async Task<StudentIdCardTemplateDto> CaptureIdentityTemplateAsync(StudentIdCardTemplateDto template, StudentDocumentSnapshot student)
	{
		var frozen = JsonSerializer.Deserialize<StudentIdCardTemplateDto>(JsonSerializer.Serialize(template, JsonOptions), JsonOptions)!;
		frozen.SchoolIdentity = await schoolIdentity.CaptureAsync(student.BoardId, student.MediumId, student.AcademicYearId, signature: false);
		SchoolIdentityTemplateAssetService.Apply(frozen.SchoolIdentity, frozen.Logo, SchoolIdentityAssetKind.Logo);
		frozen.SchoolIdentity.BannerAssetId = null;
		return frozen;
	}
}
