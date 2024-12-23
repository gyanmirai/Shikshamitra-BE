using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public interface ILeavePolicyRepository : IRepository<LeavePolicy,Guid>
{
	// if we are using some additional method then we have to write it here
}