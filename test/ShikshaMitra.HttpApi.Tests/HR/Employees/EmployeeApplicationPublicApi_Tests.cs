using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
		using var content = BuildMultipart(BadPayload());

		var response = await CreateAnonymousClient()
			.PostAsync($"{PublicBaseUrl}/{invite.Token}/submit", content);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var body = (await response.Content.ReadAsStringAsync()).ToLowerInvariant();
		body.ShouldContain("mobile");
		body.ShouldContain("date of birth");
		body.ShouldContain("iso");
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
		bool includeInvalidResume = false)
	{
		var content = new MultipartFormDataContent();
		content.Add(
			new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
			"data");

		if (includeValidFiles)
		{
			var resume = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF"));
			resume.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
			content.Add(resume, "resume", "resume.pdf");

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

		return content;
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
			dateOfBirth = "2026-06-13",
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
