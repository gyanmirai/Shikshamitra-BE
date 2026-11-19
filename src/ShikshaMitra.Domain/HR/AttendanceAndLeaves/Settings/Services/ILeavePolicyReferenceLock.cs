using System;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>Serializes archive and assignment-reference creation until the current transaction ends.</summary>
public interface ILeavePolicyReferenceLock
{
	Task AcquireAsync(Guid policyId, Guid? tenantId);
}