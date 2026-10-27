using System;
using ShikshaMitra.HR.Employees;

namespace ShikshaMitra.HR.Policies;

/// <summary>Pure synthetic employee construction shared with the in-memory API tests.</summary>
public static class HrSetupReviewTestData
{
	public static Employee Employee(Guid id, Guid tenantId, Guid roleId) => new(id, tenantId, id, "SYNTHETIC-REVIEW",
		new EmployeeBasicInfo(ProfilePictureBlobName: null, FirstName: "Synthetic", LastName: "Reviewer", MiddleName: null,
			ShortName: "SR", EmployeeStatusType: null, DateOfBirth: null, FamilyCast: null, CastType: null, GenderType: null,
			Religion: null, MaritalStatus: null, BloodGroup: null, MobileNo: null, SecondaryMobileNo: null, Email: "synthetic@example.test",
			PanNo: "ABCDE1234F", AdharNo: "000000000000", Address1: null, Address2: null, Address3: null, Department: null, Designation: null,
			DateOfJoining: new DateOnly(2026, 6, 1), EmployeeGroup: null, Qualification: null, RoleId: roleId,
			IdentificationMark1: null, City: null, State: null, Country: null, PinCode: null, ShiftId: null), null, null);
}
