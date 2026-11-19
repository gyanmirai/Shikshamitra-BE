using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionOfferingConfigurationLock
{
	Task AcquireAndRefreshAsync(AdmissionOffering offering);
}
