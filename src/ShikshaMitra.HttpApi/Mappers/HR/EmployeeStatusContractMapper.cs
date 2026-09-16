using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.Status;

namespace ShikshaMitra.HR.Employees;

public static class EmployeeStatusContractMapper
{
	public static ChangeEmployeeStatusDto ToChangeDto(EmployeeStatusUpdateRequest request)
	{
		return new ChangeEmployeeStatusDto
		{
			EmployeeStatusType = (EmployeeStatusType)request.EmployeeStatusType,
			Remarks = request.Remarks,
			EffectiveDate = request.EffectiveDate
		};
	}

	public static EmployeeStatusUpdateResponse ToResponse(EmployeeStatusResponseDto dto)
	{
		return new EmployeeStatusUpdateResponse
		{
			EmployeeId = dto.EmployeeId,
			PreviousStatus = dto.PreviousStatus.HasValue ? (int)dto.PreviousStatus.Value : null,
			CurrentStatus = (int)dto.CurrentStatus,
			UpdatedAt = dto.UpdatedAt,
			EffectiveDate = dto.EffectiveDate
		};
	}

	public static EmployeeStatusDeactivationCheckResponse ToResponse(EmployeeStatusDeactivationCheckDto dto)
	{
		return new EmployeeStatusDeactivationCheckResponse
		{
			CanDeactivate = dto.CanDeactivate,
			ActiveDirectReportCount = dto.ActiveDirectReportCount
		};
	}

	public static EmployeeActivationReadinessResponse ToResponse(EmployeeActivationReadinessDto dto)
	{
		return new EmployeeActivationReadinessResponse
		{
			CanActivate = dto.CanActivate,
			RequiredMonth = dto.RequiredMonth,
			CompensationRevisionId = dto.CompensationRevisionId,
			BlockerCodes = dto.BlockerCodes
		};
	}
}
