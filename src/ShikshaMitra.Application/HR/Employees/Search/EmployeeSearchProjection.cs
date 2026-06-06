using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees.Search;

public static class EmployeeSearchProjection
{
	public static EmployeeSummary ToSummary(
		Employee employee,
		IReadOnlyDictionary<Guid, List<EmployeeOrganizationDto>> organizationsByUserId)
	{
		var organizations = employee.UserId.HasValue &&
			organizationsByUserId.TryGetValue(employee.UserId.Value, out var scopedOrganizations)
				? scopedOrganizations
				: [];

		return new EmployeeSummary
		{
			Id = employee.Id,
			UserId = employee.UserId,
			RoleName = employee.Role?.Name,
			FullName = employee.EmployeeName,
			FirstName = employee.FirstName,
			MiddleName = employee.MiddleName,
			LastName = employee.LastName,
			ThumbnailPicture = employee.ProfilePictureBlobName,
			EmployeeIdentifier = employee.EmployeeCode,
			Dob = employee.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
			Gender = employee.GenderType?.ToString(),
			Doj = employee.DateOfJoining?.ToDateTime(TimeOnly.MinValue),
			Email = employee.Email,
			MobileNo = employee.MobileNo,
			Department = employee.Department,
			Designation = employee.Designation,
			BloodGroup = employee.BloodGroup,
			Address = string.Join(", ", new[] { employee.Address1, employee.Address2, employee.Address3 }.Where(x => !string.IsNullOrWhiteSpace(x))),
			IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
			Shift = employee.Shift != null ? new ShiftInfo
			{
				Id = employee.Shift.Id,
				Name = employee.Shift.ShiftName ?? string.Empty,
				StartTime = employee.Shift.StartTime.ToString("HH:mm:ss"),
				EndTime = employee.Shift.EndTime.ToString("HH:mm:ss")
			} : null,
			Organizations = organizations,
			OrganizationNames = organizations
				.Select(x => x.Name)
				.Distinct()
				.OrderBy(x => x)
				.ToList(),
			Status = (int)(employee.EmployeeStatusType ?? EmployeeStatusType.Active)
		};
	}
}
