using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EmployeeApplicationPublicApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/employee-applications";
	private const string PublicBaseUrl = "/api/public/employee-applications";

	public EmployeeApplicationPublicApi_Tests(ShikshaMitraApiTestFactory factory)
		: base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Public_Submit_Should_Return_400_For_Reported_Bad_Multipart_Payload()
	{
		var invite = await CreateInviteAsync("bad-payload");
		using var content = BuildMultipart(BadPayload(), includeValidFiles: true);

		var response = await CreateAnonymousClient()
			.PostAsync($"{PublicBaseUrl}/{invite.Token}/submit", content);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await ReadProblemDetailsAsync(response);
		ShouldContainValidationError(
			problem,
			"personalInfo.mobileNo",
			"10 digits starting with 6-9");
		ShouldContainValidationError(
			problem,
			"personalInfo.dateOfBirth",
			"Date of birth must be in the past");
		ShouldContainValidationError(
			problem,
			"personalInfo.state.isoCode",
			"ISO code");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Public_Submit_Should_Return_400_For_Missing_Family_Caste_And_Future_Work_Dates()
	{
		var invite = await CreateInviteAsync("future-work");
		var payload = JsonNode.Parse(JsonSerializer.Serialize(ValidPayload("future-work"), JsonOptions))
			?? throw new InvalidOperationException("Failed to build public application payload.");
		payload["personalInfo"]!["familyCaste"] = " ";
		payload["workExperiences"]![0]!["startDate"] = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd");
		payload["workExperiences"]![0]!["endDate"] = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd");
		using var content = BuildMultipart(payload, includeValidFiles: true);

		var response = await CreateAnonymousClient()
			.PostAsync($"{PublicBaseUrl}/{invite.Token}/submit", content);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await ReadProblemDetailsAsync(response);
		ShouldContainValidationError(
			problem,
			"personalInfo.familyCaste",
			"Family caste is required");
		ShouldContainValidationError(
			problem,
			"workExperiences[0].startDate",
			"Start date cannot be in the future");
		ShouldContainValidationError(
			problem,
			"workExperiences[0].endDate",
			"End date cannot be in the future");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Public_Submit_Should_Accept_Valid_Multipart_And_Reject_Duplicate_As_4xx()
	{
		var invite = await CreateInviteAsync("valid-submit");
		using var content = BuildMultipart(ValidPayload("valid-submit"), includeValidFiles: true);

		var response = await CreateAnonymousClient()
			.PostAsync($"{PublicBaseUrl}/{invite.Token}/submit", content);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var duplicateContent = BuildMultipart(ValidPayload("valid-submit-duplicate"));
		var duplicateResponse = await CreateAnonymousClient()
			.PostAsync($"{PublicBaseUrl}/{invite.Token}/submit", duplicateContent);

		((int)duplicateResponse.StatusCode).ShouldBeGreaterThanOrEqualTo(400);
		((int)duplicateResponse.StatusCode).ShouldBeLessThan(500);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Public_Submit_Should_Return_4xx_For_Invalid_Upload()
	{
		var invite = await CreateInviteAsync("bad-upload");
		using var content = BuildMultipart(ValidPayload("bad-upload"), includeInvalidResume: true);

		var response = await CreateAnonymousClient()
			.PostAsync($"{PublicBaseUrl}/{invite.Token}/submit", content);

		((int)response.StatusCode).ShouldBeGreaterThanOrEqualTo(400);
		((int)response.StatusCode).ShouldBeLessThan(500);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Public_Submit_Should_Reject_Oversized_Resume_Without_Submitting()
	{
		var invite = await CreateInviteAsync("oversized-resume");
		using var content = BuildMultipart(
			ValidPayload("oversized-resume"),
			includeOversizedResume: true);
		var client = CreateAnonymousClient();

		var response = await client.PostAsync($"{PublicBaseUrl}/{invite.Token}/submit", content);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).ShouldContain("10MB");
		var application = await client.GetFromJsonAsync<PublicEmployeeApplicationDto>(
			$"{PublicBaseUrl}/{invite.Token}",
			JsonOptions);
		application.ShouldNotBeNull();
		application!.IsSubmitted.ShouldBeFalse();
		application.CanSubmit.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Public_Submit_Should_Reject_Partial_Family_And_Work_Rows()
	{
		var invite = await CreateInviteAsync("partial-rows");
		var payload = JsonNode.Parse(JsonSerializer.Serialize(ValidPayload("partial-rows"), JsonOptions))
			?? throw new InvalidOperationException("Failed to build public application payload.");
		payload["familyInfo"]!["fatherDetails"]!["name"] = null;
		payload["workExperiences"]![0]!["mediumStageName"] = "";
		payload["workExperiences"]![0]!["designation"] = "";
		payload["workExperiences"]![0]!["startDate"] = null;
		using var content = BuildMultipart(payload, includeValidFiles: true);

		var response = await CreateAnonymousClient()
			.PostAsync($"{PublicBaseUrl}/{invite.Token}/submit", content);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await ReadProblemDetailsAsync(response);
		ShouldContainValidationError(
			problem,
			"familyInfo.fatherDetails.name",
			"Father name is required");
		ShouldContainValidationError(
			problem,
			"workExperiences[0].mediumStageName",
			"Organization is required");
		ShouldContainValidationError(
			problem,
			"workExperiences[0].designation",
			"Designation is required");
		ShouldContainValidationError(
			problem,
			"workExperiences[0].startDate",
			"Start date is required");
	}

	[Theory]
	[InlineData("mismatched-resume-mime")]
	[InlineData("spoofed-pdf")]
	[InlineData("svg-photo")]
	[InlineData("unsafe-filename")]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Public_Submit_Should_Reject_Unsafe_Upload_Metadata_And_Content(string scenario)
	{
		var invite = await CreateInviteAsync($"unsafe-{scenario}");
		using var content = BuildMultipart(
			ValidPayload($"unsafe-{scenario}"),
			securityUploadScenario: scenario);

		var response = await CreateAnonymousClient()
			.PostAsync($"{PublicBaseUrl}/{invite.Token}/submit", content);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Public_Submit_Should_Store_And_Serve_Canonical_Photo_Content_Type()
	{
		var invite = await CreateInviteAsync("canonical-photo-mime");
		using var content = BuildMultipart(
			ValidPayload("canonical-photo-mime"),
			includeValidResume: true,
			includeJpegAliasPhoto: true);

		var response = await CreateAnonymousClient()
			.PostAsync($"{PublicBaseUrl}/{invite.Token}/submit", content);

		var submitBody = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.OK, submitBody);
		var photoResponse = await Client.GetAsync(
			$"{BaseUrl}/{invite.Application.Id}/photo");
		photoResponse.EnsureSuccessStatusCode();
		photoResponse.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
	}

	private async Task<EmployeeApplicationInviteDto> CreateInviteAsync(string prefix)
	{
		var response = await Client.PostAsJsonAsync(
			BaseUrl,
			new CreateEmployeeApplicationInviteDto
			{
				FirstName = "Public",
				MiddleName = "Api",
				LastName = prefix,
				Email = $"onboarding.{prefix}.{Guid.NewGuid():N}@test.com",
				MobileNo = "9876543210",
				PositionAppliedFor = "Mathematics Teacher",
				DepartmentAppliedFor = "Academics"
			},
			JsonOptions);

		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<EmployeeApplicationInviteDto>(JsonOptions)
			?? throw new InvalidOperationException("Invite response was empty.");
	}

	private static MultipartFormDataContent BuildMultipart(
		object payload,
		bool includeValidFiles = false,
		bool includeInvalidResume = false,
		bool includeOversizedResume = false,
		string? securityUploadScenario = null,
		bool includeJpegAliasPhoto = false,
		bool includeValidResume = false)
	{
		var content = new MultipartFormDataContent();
		content.Add(
			new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
			"data");

		if (includeValidFiles || includeValidResume)
		{
			var resume = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF"));
			resume.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
			content.Add(resume, "resume", "resume.pdf");
		}

		if (includeValidFiles)
		{
			var photo = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/lN8mxQAAAABJRU5ErkJggg=="));
			photo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
			content.Add(photo, "photo", "photo.png");
		}

		if (includeInvalidResume)
		{
			var resume = new ByteArrayContent(Encoding.UTF8.GetBytes("not a supported document"));
			resume.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
			content.Add(resume, "resume", "unsupported.exe");
		}

		if (includeOversizedResume)
		{
			var resume = new ByteArrayContent(new byte[10 * 1024 * 1024 + 1]);
			resume.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
			content.Add(resume, "resume", "oversized.pdf");
		}

		switch (securityUploadScenario)
		{
			case "mismatched-resume-mime":
			{
				var resume = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4\n%%EOF"));
				resume.Headers.ContentType = new MediaTypeHeaderValue("text/html");
				content.Add(resume, "resume", "resume.pdf");
				break;
			}
			case "spoofed-pdf":
			{
				var resume = new ByteArrayContent(Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>"));
				resume.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
				content.Add(resume, "resume", "resume.pdf");
				break;
			}
			case "svg-photo":
			{
				var photo = new ByteArrayContent(Encoding.UTF8.GetBytes("<svg onload=\"alert(1)\"></svg>"));
				photo.Headers.ContentType = new MediaTypeHeaderValue("image/svg+xml");
				content.Add(photo, "photo", "photo.svg");
				break;
			}
			case "unsafe-filename":
			{
				var resume = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4\n%%EOF"));
				resume.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
				content.Add(resume, "resume", "../resume.pdf");
				break;
			}
		}

		if (includeJpegAliasPhoto)
		{
			var photo = new ByteArrayContent(EmployeeTestHelper.BuildSyntheticJpeg());
			photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpg");
			content.Add(photo, "photo", "photo.jpg");
		}

		return content;
	}

	private static async Task<JsonObject> ReadProblemDetailsAsync(HttpResponseMessage response)
	{
		var body = await response.Content.ReadAsStringAsync();
		var problem = JsonNode.Parse(body) as JsonObject;
		problem.ShouldNotBeNull($"Expected an RFC 7807 response, but received: {body}");
		return problem!;
	}

	private static void ShouldContainValidationError(
		JsonObject problem,
		string memberName,
		string messageFragment)
	{
		var serializedProblem = problem.ToJsonString();
		(problem["errorCode"]?.GetValue<string>() == "VALIDATION_ERROR")
			.ShouldBeTrue($"Expected VALIDATION_ERROR in: {serializedProblem}");
		var errors = problem["errors"] as JsonObject;
		errors.ShouldNotBeNull($"Expected structured validation errors in: {serializedProblem}");
		var messages = errors![memberName] as JsonArray;
		messages.ShouldNotBeNull(
			$"Expected validation member '{memberName}' in: {serializedProblem}");

		var hasExpectedMessage = false;
		foreach (var message in messages!)
		{
			if (message?.GetValue<string>().Contains(
				    messageFragment,
				    StringComparison.OrdinalIgnoreCase) == true)
			{
				hasExpectedMessage = true;
				break;
			}
		}

		hasExpectedMessage.ShouldBeTrue(
			$"Expected '{messageFragment}' for '{memberName}' in: {serializedProblem}");
	}

	private static object BadPayload() => new
	{
		personalInfo = new
		{
			firstName = "Yagnik",
			middleName = "sddlj",
			lastName = "slja;lasdj",
			email = "yvadher@gyanmirai.in",
			mobileNo = "20938740982304",
			secondaryMobileNo = "322342342",
			dateOfBirth = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"),
			gender = 1,
			religion = "hindu",
			casteType = 0,
			familyCaste = "fasdf",
			maritalStatus = 1,
			department = "08243502384",
			designation = "2340820",
			qualification = "dfadsfasdf",
			address1 = "sdfas",
			address2 = "asdfs",
			country = new { name = "India", isoCode = "IN" },
			state = new { name = "guyjuart", isoCode = "32423" },
			city = "surat",
			pincode = "395010"
		},
		workExperiences = Array.Empty<object>()
	};

	private static object ValidPayload(string prefix) => new
	{
		personalInfo = new
		{
			firstName = "Public",
			middleName = "Api",
			lastName = prefix,
			email = $"public.{prefix}.{Guid.NewGuid():N}@test.com",
			mobileNo = "9876543210",
			secondaryMobileNo = "8876543210",
			dateOfBirth = "1990-01-01",
			gender = 1,
			religion = "Hindu",
			casteType = 0,
			familyCaste = "General",
			maritalStatus = 0,
			department = "Academics",
			designation = "Mathematics Teacher",
			qualification = "M.Sc Mathematics with B.Ed",
			identificationMark1 = "Small mark on left hand",
			address1 = "House 1",
			address2 = "Automation Road",
			country = new { name = "India", isoCode = "IN" },
			state = new { name = "Gujarat", isoCode = "GJ" },
			city = "Ahmedabad",
			pincode = "380001"
		},
		familyInfo = new
		{
			fatherDetails = new
			{
				name = "Father Api",
				occupation = "Teacher",
				mobileNo = "7876543210"
			}
		},
		additionalInfo = new
		{
			personWithDisability = false,
			secondLanguage = "Gujarati",
			isLibraryMember = true,
			isTransportMember = false
		},
		workExperiences = new[]
		{
			new
			{
				mediumStageName = "Previous School",
				designation = "Math Faculty",
				department = "Academics",
				startDate = "2020-04-01",
				endDate = "2023-03-31"
			}
		}
	};
}
