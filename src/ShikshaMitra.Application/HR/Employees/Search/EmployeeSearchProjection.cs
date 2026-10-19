using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees.Search;

public static class EmployeeSearchProjection
{
	// Project scalars before materialization so EF does not load owned documents,
	// work experience, bank details, or auto-included entity graphs for a list row.
	public static Expression<Func<Employee, EmployeeSummary>> Summary => employee => new EmployeeSummary
		{
			Id = employee.Id,
			UserId = employee.UserId,
			RoleName = employee.Role != null ? employee.Role.Name : null,
			FullName = JoinNameParts(employee.FirstName, employee.LastName),
			FirstName = employee.FirstName,
			MiddleName = employee.MiddleName,
			LastName = employee.LastName,
			ThumbnailPicture = employee.ProfilePictureBlobName,
			EmployeeIdentifier = employee.EmployeeCode,
			Dob = employee.DateOfBirth.HasValue ? employee.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue) : null,
			Gender = employee.GenderType.HasValue ? employee.GenderType.Value.ToString() : null,
			Doj = employee.DateOfJoining.HasValue ? employee.DateOfJoining.Value.ToDateTime(TimeOnly.MinValue) : null,
			Email = employee.Email,
			MobileNo = employee.MobileNo,
			Department = employee.Department,
			Designation = employee.Designation,
			BloodGroup = employee.BloodGroup,
			Address = JoinAddressParts(employee.Address1, employee.Address2, employee.Address3),
			IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
			Shift = employee.Shift != null ? new ShiftInfo
			{
				Id = employee.Shift.Id,
				Name = employee.Shift.ShiftName ?? string.Empty,
				StartTime = employee.Shift.StartTime.ToString("HH:mm:ss"),
				EndTime = employee.Shift.EndTime.ToString("HH:mm:ss")
			} : null,
			Status = (int)(employee.EmployeeStatusType ?? EmployeeStatusType.Active)
		};

	public static void ApplyOrganizations(
		EmployeeSummary employee,
		IReadOnlyDictionary<Guid, List<EmployeeOrganizationDto>> organizationsByUserId)
	{
		employee.Organizations = employee.UserId.HasValue &&
			organizationsByUserId.TryGetValue(employee.UserId.Value, out var organizations)
				? organizations
				: [];
		employee.OrganizationNames = employee.Organizations
			.Select(x => x.Name).Distinct().OrderBy(x => x).ToList();
	}

	private static string JoinNameParts(string? firstName, string? lastName) =>
		string.Join(" ", new[] { firstName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x)));

	private static string JoinAddressParts(string? address1, string? address2, string? address3) =>
		string.Join(", ", new[] { address1, address2, address3 }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
