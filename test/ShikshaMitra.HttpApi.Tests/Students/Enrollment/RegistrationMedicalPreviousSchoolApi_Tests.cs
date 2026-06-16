using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.Enrollment;
using ShikshaMitra.Students.Enrollments.Registrations;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class RegistrationMedicalPreviousSchoolApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/student/enrollment/registrations";

	public RegistrationMedicalPreviousSchoolApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_RoundTrip_Medical_Disability_Fields()
	{
		var studentId = await StudentEnrollmentTestHelper
			.CreateRegisteredStudentAndGetIdAsync(Client, "MedDis");

		var update = new UpdateMedicalRecordsDto
		{
			WeightKg = 31.5m,
			HeightCm = 141.2m,
			BloodGroup = "B+",
			HaveDisability = true,
			DisabilityDetails = "Requires front-row seating support"
		};

		var response = await Client.PutAsJsonAsync(
			$"{BaseUrl}/{studentId}/medical-records", update, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<Result<StudentMedicalRecordsDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Data.ShouldNotBeNull();
		body.Data.WeightKg.ShouldBe(31.5m);
		body.Data.HeightCm.ShouldBe(141.2m);
		body.Data.BloodGroup.ShouldBe("B+");
		body.Data.HaveDisability.ShouldBeTrue();
		body.Data.DisabilityDetails.ShouldBe("Requires front-row seating support");

		var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/additional-details");
		var additionalBody = await getResponse.Content.ReadFromJsonAsync<Result<StudentAdditionalDetailsDto>>(JsonOptions);
		additionalBody.ShouldNotBeNull();
		additionalBody.Data.ShouldNotBeNull();
		additionalBody.Data.HaveDisability.ShouldBeTrue();
		additionalBody.Data.DisabilityDetails.ShouldBe("Requires front-row seating support");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_RoundTrip_Previous_School_Summary_And_Index_Number()
	{
		var studentId = await StudentEnrollmentTestHelper
			.CreateRegisteredStudentAndGetIdAsync(Client, "PrevIdx");

		var update = new UpdatePreviousSchoolDetailsDto
		{
			SchoolName = "Sardar Patel Vidyalaya",
			PreviousSchool = "Sardar Patel Vidyalaya",
			SchoolAddress = "Ahmedabad",
			Board = "GSEB",
			Medium = "Gujarati",
			LastClassAttended = "5",
			LastClassPassed = "5",
			SchoolIndexNumber = "SCH-2025-1234",
			TCNumber = "TC-9988",
			PercentageObtained = 87.25m,
			AcademicYear = "2025-2026"
		};

		var response = await Client.PutAsJsonAsync(
			$"{BaseUrl}/{studentId}/previous-school-details", update, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<Result<StudentPreviousSchoolDetailsDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Data.ShouldNotBeNull();
		body.Data.SchoolName.ShouldBe("Sardar Patel Vidyalaya");
		body.Data.PreviousSchool.ShouldBe("Sardar Patel Vidyalaya");
		body.Data.LastClassAttended.ShouldBe("5");
		body.Data.LastClassPassed.ShouldBe("5");
		body.Data.SchoolIndexNumber.ShouldBe("SCH-2025-1234");

		var getResponse = await Client.GetAsync($"{BaseUrl}/{studentId}/previous-school-details");
		var getBody = await getResponse.Content.ReadFromJsonAsync<Result<StudentPreviousSchoolDetailsDto>>(JsonOptions);
		getBody.ShouldNotBeNull();
		getBody.Data.ShouldNotBeNull();
		getBody.Data.PreviousSchool.ShouldBe("Sardar Patel Vidyalaya");
		getBody.Data.LastClassPassed.ShouldBe("5");
		getBody.Data.SchoolIndexNumber.ShouldBe("SCH-2025-1234");
	}
}
