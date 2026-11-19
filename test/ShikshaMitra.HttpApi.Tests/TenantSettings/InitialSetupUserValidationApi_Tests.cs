using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.InitialSetup;
using Shouldly;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public partial class InitialSetupApi_Tests
{
	[Theory]
	[InlineData("ab", "staff@example.test", "Synthetic-Staff-39!", "UserName")]
	[InlineData("staff member", "staff@example.test", "Synthetic-Staff-39!", "UserName")]
	[InlineData("valid.staff", "invalid-email", "Synthetic-Staff-39!", "Email")]
	[InlineData("valid.staff", "staff@example.test", "12345", "Password")]
	[InlineData("valid.staff", "staff@example.test", "Abc12!", "Password")]
	public async Task Invalid_user_credentials_are_rejected_without_changing_setup(
		string userName, string email, string password, string expectedField)
	{
		using var school = await NewSchoolAsync();
		var progress = await PrepareUsersStepAsync(school);
		var response = await school.PostAsJsonAsync(Url + "/users", new CreateSetupUserInput {
			ConcurrencyStamp = progress.ConcurrencyStamp, RequestId = Guid.NewGuid(),
			UserName = userName, Email = email, Password = password, RoleNames = [] });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.ShouldContain(expectedField, Case.Insensitive);
		body.ShouldNotContain(password);
		var after = await GetAsync(school);
		after.CreatedUsers.ShouldBeEmpty();
		after.ConcurrencyStamp.ShouldBe(progress.ConcurrencyStamp);
	}

	[Theory]
	[InlineData("taken.staff", "another@example.test", "DuplicateUserName")]
	[InlineData("another.staff", "taken@example.test", "DuplicateEmail")]
	public async Task Duplicate_user_credentials_return_an_actionable_identity_code(
		string userName, string email, string expectedCode)
	{
		using var school = await NewSchoolAsync();
		var progress = await PrepareUsersStepAsync(school);
		var first = await school.PostAsJsonAsync(Url + "/users", new CreateSetupUserInput {
			ConcurrencyStamp = progress.ConcurrencyStamp, RequestId = Guid.NewGuid(), UserName = "taken.staff",
			Email = "taken@example.test", Password = "Synthetic-Staff-39!", RoleNames = [] });
		first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
		progress = (await first.Content.ReadFromJsonAsync<InitialSetupDto>())!;
		var duplicate = await school.PostAsJsonAsync(Url + "/users", new CreateSetupUserInput {
			ConcurrencyStamp = progress.ConcurrencyStamp, RequestId = Guid.NewGuid(), UserName = userName,
			Email = email, Password = "Synthetic-Staff-39!", RoleNames = [] });
		duplicate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await duplicate.Content.ReadAsStringAsync()).ShouldContain(expectedCode);
		var after = await GetAsync(school);
		after.CreatedUsers.Count.ShouldBe(1);
		after.ConcurrencyStamp.ShouldBe(progress.ConcurrencyStamp);
	}

	private async Task<InitialSetupDto> PrepareUsersStepAsync(System.Net.Http.HttpClient school)
	{
		var progress = await PrepareYearStepAsync(school);
		progress = await PutAsync(school, "academic-year", new SaveSetupYearInput {
			ConcurrencyStamp = progress.ConcurrencyStamp, Name = "2026-2027",
			StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
			EndDate = new DateTime(2027, 5, 31, 0, 0, 0, DateTimeKind.Utc) });
		var draft = progress.Draft;
		draft.BoardType = AcademicSetups.EducationBoards.EducationBoardType.CBSE;
		draft.Medium = "English"; draft.Stage = "Primary"; draft.ClassName = "Class 1";
		draft.AgeLowerLimit = 6; draft.DivisionName = "A"; draft.Strength = 30;
		return await PutAsync(school, "structure", new SaveSetupStructureInput {
			ConcurrencyStamp = progress.ConcurrencyStamp, Draft = draft });
	}
}
