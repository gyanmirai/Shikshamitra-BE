using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.Infrastructure.Data;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Shared helper that creates a fully valid employee via the API.
/// Used as the foundation dependency for Payroll, Leaves, Attendance, and Reports tests.
/// </summary>
public static class EmployeeTestHelper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	/// <summary>
	/// Creates an employee via POST /api/employees/personal-info (multipart/form-data)
	/// and returns the response containing the employee ID.
	/// </summary>
	public static async Task<PersonalInfoResponse> CreateEmployeeAsync(
		HttpClient client,
		string? firstNameSuffix = null)
	{
		var personalInfo = BuildPersonalInfoRequest(firstNameSuffix);
		var jsonData = JsonSerializer.Serialize(personalInfo, JsonOptions);

		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(jsonData), "data");

		var response = await client.PostAsync("/api/employees/personal-info", content);
		response.EnsureSuccessStatusCode();

		return await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create test employee");
	}

	/// <summary>
	/// Creates an employee and returns just the ID — convenience shorthand.
	/// </summary>
	public static async Task<Guid> CreateEmployeeAndGetIdAsync(
		HttpClient client,
		string? firstNameSuffix = null)
	{
		var result = await CreateEmployeeAsync(client, firstNameSuffix);
		return result.Id;
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
			RoleId = Guid.NewGuid(),
			ShiftId = Guid.NewGuid(),
			Email = TestDataHelper.UniqueEmail("emp"),
			Gender = 1,
		};
	}
}
