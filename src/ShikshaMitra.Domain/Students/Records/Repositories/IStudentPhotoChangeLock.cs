using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.Records;

public interface IStudentPhotoChangeLock
{
	Task AcquireAsync(Guid? tenantId, Guid studentId);
}
