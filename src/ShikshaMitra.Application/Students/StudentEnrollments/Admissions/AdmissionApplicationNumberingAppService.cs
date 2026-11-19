using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Uow;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.Admissions;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationNumberingAppService(AdmissionApplicationNumberingService service)
	: ApplicationService, IAdmissionApplicationNumberingAppService
{
	[Authorize(Permissions.View)]
	public Task<AdmissionApplicationNumberingDto> GetAsync() => service.GetAsync();
	[Authorize(Permissions.Add)]
	[UnitOfWork(isTransactional: true)]
	public virtual Task<AdmissionApplicationNumberingDto> SaveAsync(SaveAdmissionApplicationNumberingDto input) => service.SaveAsync(input);
	[Authorize(Permissions.View)]
	[UnitOfWork(isTransactional: true)]
	public virtual Task<AdmissionApplicationNumberPreviewDto> PreviewAsync(PreviewAdmissionApplicationNumberingDto input) => service.PreviewAsync(input);
}
