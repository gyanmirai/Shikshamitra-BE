using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HttpApi.Controllers.Students.Records;
using ShikshaMitra.Students.Records;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Records;

public class StudentRecordProfileApiContract_Tests
{
	[Fact]
	public void Student_record_profile_is_an_explicit_read_with_record_view_at_both_boundaries()
	{
		foreach (var boundary in new[] { typeof(StudentRecordProfileAppService), typeof(StudentRecordProfileController) })
			boundary.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		typeof(StudentRecordProfileController).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe("api/students/records/{studentId:guid}/profile");
		var method = typeof(StudentRecordProfileController).GetMethod(nameof(StudentRecordProfileController.GetAsync))!;
		method.GetCustomAttribute<HttpGetAttribute>().ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task<Result<StudentRecordProfileDto>>));
		typeof(IStudentRecordProfileAppService).GetMethods().Select(item => item.Name).ShouldBe(["GetAsync"]);
	}

	[Fact]
	public void Student_record_profile_uses_guid_identity_and_only_the_approved_profile_projection()
	{
		typeof(StudentRecordProfileDto).GetProperty("Id")!.PropertyType.ShouldBe(typeof(Guid));
		typeof(StudentRecordProfileDto).GetProperty("StudentId")!.PropertyType.ShouldBe(typeof(Guid));
		typeof(StudentRecordProfileDto).GetProperties().Select(item => item.Name).ShouldBe(
		[
			"Id", "StudentId", "FirstName", "MiddleName", "LastName", "GrNo", "GrNumber", "RollNo",
			"StandardId", "StandardName", "DivisionId", "DivisionName", "AcademicMediumId", "AcademicMediumName",
			"Status", "DateOfBirth", "Gender", "RecordsAccess", "EntrySource", "PlacementEditable", "MaskedFields", "EnrollmentHistory", "AcademicYearId", "AdmissionOnboarding"
		], ignoreOrder: true);
	}
}
