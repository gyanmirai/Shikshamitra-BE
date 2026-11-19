using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Portal;

public interface IStudentPortalEmailChangeAppService : IApplicationService
{
	Task ConfirmAsync(ConfirmStudentPortalEmailDto input);
}
