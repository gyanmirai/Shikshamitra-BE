using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionCycleNameLock
{
	Task AcquireAsync(Guid? tenantId, string normalizedName);
}
