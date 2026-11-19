using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Transaction-scoped locks shared with room reservation writers.</summary>
public interface IAdmissionAssessmentResourceLock
{
	Task AcquireAsync(Guid? tenantId, IEnumerable<Guid> resourceIds);
}
