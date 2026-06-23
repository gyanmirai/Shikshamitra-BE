using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.Infrastructure.Data;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Shared helper that creates a fully valid employee via the API.
/// Used as the foundation dependency for Payroll, Leaves, Attendance, and Reports tests.
/// </summary>
public static class EmployeeTestHelper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static Guid DefaultRoleId { get; set; }
	public static Guid DefaultShiftId { get; set; }
	public static Guid DefaultAcademicYearId { get; set; }
	public static Guid DefaultBoardId { get; set; }
	public static Guid DefaultMediumId { get; set; }
	public static Guid DefaultMediumStageId { get; set; }

	/// <summary>
	/// Creates an employee via POST /api/employees/personal-info (multipart/form-data)
	/// and returns the response containing the employee ID.
	/// </summary>
	public static async Task<PersonalInfoResponse> CreateEmployeeAsync(
		HttpClient client,
		string? firstNameSuffix = null,
		bool isTeaching = true,
		DateTime? dateOfJoining = null)
	{
		var personalInfo = BuildPersonalInfoRequest(firstNameSuffix);
		personalInfo.IsTeaching = isTeaching;
		if (dateOfJoining.HasValue)
		{
			personalInfo.Doj = dateOfJoining.Value;
		}
		var jsonData = JsonSerializer.Serialize(personalInfo, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var response = await client.PostAsync("/api/employees/personal-info", content);
		if (!response.IsSuccessStatusCode)
		{
			var payload = await response.Content.ReadAsStringAsync();
			throw new HttpRequestException(
				$"Employee create failed with {(int)response.StatusCode} {response.StatusCode}: {payload}",
				null,
				response.StatusCode);
		}

		var employee = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create test employee");
		await AssignDefaultAcademicPermissionAsync(client, employee);
		return employee;
	}

	/// <summary>
	/// Creates an employee with a profile picture attached as multipart/form-data.
	/// Sends both the JSON data field and a synthetic JPEG image.
	/// </summary>
	public static async Task<PersonalInfoResponse> CreateEmployeeWithPictureAsync(
		HttpClient client,
		string? firstNameSuffix = null)
	{
		var personalInfo = BuildPersonalInfoRequest(firstNameSuffix);
		var jsonData = JsonSerializer.Serialize(personalInfo, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var imageBytes = BuildSyntheticJpeg();
		var imageContent = new ByteArrayContent(imageBytes);
		imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
		content.Add(imageContent, "profilePicture", "profile.jpg");

		var response = await client.PostAsync("/api/employees/personal-info", content);
		if (!response.IsSuccessStatusCode)
		{
			var payload = await response.Content.ReadAsStringAsync();
			throw new HttpRequestException(
				$"Employee create with picture failed with {(int)response.StatusCode} {response.StatusCode}: {payload}",
				null,
				response.StatusCode);
		}

		var employee = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create test employee with picture");
		await AssignDefaultAcademicPermissionAsync(client, employee);
		return employee;
	}

	/// <summary>
	/// Creates an employee and returns just the ID -- convenience shorthand.
	/// </summary>
	public static async Task<Guid> CreateEmployeeAndGetIdAsync(
		HttpClient client,
		string? firstNameSuffix = null,
		DateTime? dateOfJoining = null)
	{
		var result = await CreateEmployeeAsync(client, firstNameSuffix, dateOfJoining: dateOfJoining);
		return result.Id;
	}

	/// <summary>
	/// Creates a document configuration via POST /api/employees/documents
	/// and returns the response containing the config ID.
	/// </summary>
	public static async Task<DocumentCreateResponse> CreateDocumentConfigAsync(HttpClient client)
	{
		// Title must match ^[A-Za-z ]+; suffix must stay alphabetic (shared in-memory DB across tests).
		var suffix = Guid.NewGuid().ToString("N")[..6]
			.Aggregate("", (s, c) => s + (char.IsDigit(c) ? (char)('a' + (c - '0')) : c));
		var request = new DocumentRequest
		{
			Title = $"Test Document {suffix}",
			DocumentSize = 5,
			DocumentTypes = new List<int> { 1, 2, 4 },
		};

		var response = await client.PostAsJsonAsync("/api/employees/documents", request, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<DocumentCreateResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create document config");
	}

	public static PersonalInfoRequest BuildPersonalInfoRequest(string? firstNameSuffix = null)
	{
		return new PersonalInfoRequest
		{
			FirstName = TestDataHelper.UniqueName(firstNameSuffix ?? "Emp"),
			LastName = "TestDoe",
			MobileNo = TestDataHelper.UniquePhone(),
			Doj = DateTime.UtcNow.AddMonths(-6),
			IsTeaching = true,
			RoleId = DefaultRoleId,
			ShiftId = DefaultShiftId,
			Email = TestDataHelper.UniqueEmail("emp"),
			PanNo = TestDataHelper.UniquePan(),
			AdharNo = TestDataHelper.UniqueAdhar(),
			Gender = 1,
		};
	}

	public static PersonalInfoRequest BuildFullPersonalInfoRequest(string? firstNameSuffix = null)
	{
		return new PersonalInfoRequest
		{
			FirstName = TestDataHelper.UniqueName(firstNameSuffix ?? "Full"),
			MiddleName = "MiddleTest",
			LastName = "FullDoe",
			ShortName = "FD",
			Dob = new DateTime(1990, 5, 15, 0, 0, 0, DateTimeKind.Utc),
			Gender = 1,
			Religion = "Hindu",
			CasteType = 0,
			FamilyCaste = "Brahmin",
			MaritalStatus = 1,
			BloodGroup = 3,
			Email = TestDataHelper.UniqueEmail("full"),
			PanNo = TestDataHelper.UniquePan(),
			AdharNo = TestDataHelper.UniqueAdhar(),
			MobileNo = TestDataHelper.UniquePhone(),
			SecondaryMobileNo = TestDataHelper.UniquePhone(),
			Doj = DateTime.UtcNow.AddMonths(-12),
			IsTeaching = true,
			Department = "Science",
			RoleId = DefaultRoleId,
			ShiftId = DefaultShiftId,
			Address1 = "123 Test Street",
			Address2 = "Block A",
			Address3 = "Near Park",
			Country = new CountryInfo { Name = "India", IsoCode = "IN" },
			State = new StateInfo { Name = "Maharashtra", IsoCode = "MH" },
			City = "Mumbai",
			PinCode = "400001",
		};
	}

	/// <summary>
	/// Builds a minimal synthetic JPEG byte array (valid JPEG header + minimal data).
	/// </summary>
	public static byte[] BuildSyntheticJpeg()
	{
		return
		[
			0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
			0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9
		];
	}

	private static async Task AssignDefaultAcademicPermissionAsync(HttpClient client, PersonalInfoResponse employee)
	{
		if (!employee.UserId.HasValue ||
			DefaultAcademicYearId == Guid.Empty ||
			DefaultBoardId == Guid.Empty ||
			DefaultMediumId == Guid.Empty ||
			DefaultMediumStageId == Guid.Empty)
		{
			return;
		}

		var response = await client.PostAsJsonAsync(
			"/api/app/user-scope/bulk-assign",
			new
			{
				userIds = new[] { employee.UserId.Value },
				academicYearId = DefaultAcademicYearId,
				scopes = new[]
				{
					new
					{
						educationBoardId = DefaultBoardId,
						educationMediumId = DefaultMediumId,
						educationMediumStageId = DefaultMediumStageId
					}
				},
				description = "HTTP API test default employee academic permission"
			},
			JsonOptions);

		if (!response.IsSuccessStatusCode)
		{
			var payload = await response.Content.ReadAsStringAsync();
			throw new HttpRequestException(
				$"Employee default academic permission failed with {(int)response.StatusCode} {response.StatusCode}: {payload}",
				null,
				response.StatusCode);
		}
	}
}
