using ShikshaMitra.Common;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Validates employment status transitions and remark requirements.
/// Centralize rule changes here (and mirror summary in employees PRD).
/// </summary>
public class EmployeeStatusTransitionValidator : DomainService
{
	public void Validate(EmployeeStatusType? from, EmployeeStatusType to, string? remarks)
	{
		if (from == to)
		{
			return;
		}

		if (to == EmployeeStatusType.Left || to == EmployeeStatusType.Retired)
		{
			if (string.IsNullOrWhiteSpace(remarks))
			{
				throw new BusinessException(HRErrorCodes.EmployeeStatusRemarksRequired);
			}
		}

		var fromTerminal = from is EmployeeStatusType.Left or EmployeeStatusType.Retired;
		if (fromTerminal && to == EmployeeStatusType.Active)
		{
			throw new BusinessException(HRErrorCodes.EmployeeStatusInvalidTransition);
		}
	}
}
