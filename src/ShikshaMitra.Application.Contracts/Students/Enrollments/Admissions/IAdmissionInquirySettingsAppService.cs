using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionInquirySettingsAppService : IApplicationService
{
	Task<List<AdmissionInquiryCounsellorDto>> GetCounsellorsAsync(string? filter = null);
}
