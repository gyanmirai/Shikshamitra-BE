using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>Canonical Admission-owned joining date for assignments without captured date evidence.</summary>
public interface ILateFeeAdmissionDateReader
{
	Task<DateTime?> ReadAsync(Guid? studentId, Guid? applicationId, Guid academicYearId);
}
