using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Security.Roles;

public interface IRoleDeletionLock
{
	Task AcquireAsync(Guid roleId);
}
