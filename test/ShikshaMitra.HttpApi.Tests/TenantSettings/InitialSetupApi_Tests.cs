using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HostTenants;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.InitialSetup;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Collection(InitialSetupTestCollection.Name), Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
public class InitialSetupApi_Tests(InitialSetupTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Url = "/api/institute-setup/initial";
	[Fact]
	public async Task New_school_resumes_drafts_and_validates_atomic_steps_before_completion()
	{
		using var school = await NewSchoolAsync();
		var context = (await school.GetFromJsonAsync<HostSessionContextDto>("/api/host/session-context"))!;
		context.SetupCompleted.ShouldBeFalse(); context.CanManageSetup.ShouldBeTrue();
		var initial = await GetAsync(school); initial.CurrentStep.ShouldBe(0);
		(await school.PutAsJsonAsync(Url + "/complete", new SetupConcurrencyInput { ConcurrencyStamp = initial.ConcurrencyStamp })).IsSuccessStatusCode.ShouldBeFalse();
		var next = await PutAsync(school, "advance", new AdvanceSetupInput { Step = 0, ConcurrencyStamp = initial.ConcurrencyStamp });
		next.Draft.Profile.City = "Draft city";
		var saved = await PutAsync(school, "draft", new SaveInitialSetupDraftInput { CurrentStep = 1, Draft = next.Draft, ConcurrencyStamp = next.ConcurrencyStamp });
		(await GetAsync(school)).Draft.Profile.City.ShouldBe("Draft city");
		(await school.PutAsJsonAsync(Url + "/draft", new SaveInitialSetupDraftInput { CurrentStep = 1, Draft = next.Draft, ConcurrencyStamp = next.ConcurrencyStamp })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var profile = saved.Draft.Profile;
		saved = await PutAsync(school, "profile", new SaveSetupProfileInput {
			ConcurrencyStamp = saved.ConcurrencyStamp, InstituteName = profile.InstituteName, Email = profile.Email,
			ContactNumber = profile.ContactNumber, Address1 = profile.Address1, City = profile.City,
			State = profile.State, Country = profile.Country, PostalCode = profile.PostalCode });
		saved.CurrentStep.ShouldBe(2);
		saved = await PutAsync(school, "academic-year", new SaveSetupYearInput { ConcurrencyStamp = saved.ConcurrencyStamp,
			Name = "2026-2027", StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2027, 5, 31, 0, 0, 0, DateTimeKind.Utc) });
		saved.CurrentStep.ShouldBe(3); saved.AcademicYearId.ShouldNotBeNull();
		var draft = saved.Draft; draft.BoardType = AcademicSetups.EducationBoards.EducationBoardType.CBSE;
		draft.Medium = "English"; draft.Stage = "Primary"; draft.ClassName = "Class 1";
		draft.AgeLowerLimit = 6; draft.DivisionName = "A"; draft.Strength = 30;
		saved = await PutAsync(school, "structure", new SaveSetupStructureInput { ConcurrencyStamp = saved.ConcurrencyStamp, Draft = draft });
		saved.CurrentStep.ShouldBe(4); saved.DivisionId.ShouldNotBeNull();
		var request = new CreateSetupUserInput { ConcurrencyStamp = saved.ConcurrencyStamp, RequestId = Guid.NewGuid(),
			UserName = "setup.staff", Email = "staff@example.test", Password = "Synthetic-Staff-39!", IsActive = true, RoleNames = [] };
		var userResponse = await school.PostAsJsonAsync(Url + "/users", request);
		userResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await userResponse.Content.ReadAsStringAsync());
		saved = (await userResponse.Content.ReadFromJsonAsync<InitialSetupDto>())!;
		saved.CreatedUsers.Count.ShouldBe(1);
		var retryResponse = await school.PostAsJsonAsync(Url + "/users", request);
		retryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await retryResponse.Content.ReadAsStringAsync()).ShouldNotContain("Synthetic-Staff-39!");
		(await retryResponse.Content.ReadFromJsonAsync<InitialSetupDto>())!.CreatedUsers.Count.ShouldBe(1);
		saved = await PutAsync(school, "advance", new AdvanceSetupInput { Step = 4, ConcurrencyStamp = saved.ConcurrencyStamp });
		// A saved draft is not a committed profile; reviewing an edit cannot silently finish with old data.
		saved.Draft.Profile.City = "Changed after profile save";
		saved = await PutAsync(school, "draft", new SaveInitialSetupDraftInput { CurrentStep = 5, Draft = saved.Draft, ConcurrencyStamp = saved.ConcurrencyStamp });
		saved.PendingSteps.ShouldContain(1);
		(await school.PutAsJsonAsync(Url + "/complete", new SetupConcurrencyInput { ConcurrencyStamp = saved.ConcurrencyStamp })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		profile = saved.Draft.Profile;
		saved = await PutAsync(school, "profile", new SaveSetupProfileInput {
			ConcurrencyStamp = saved.ConcurrencyStamp, InstituteName = profile.InstituteName, Email = profile.Email,
			ContactNumber = profile.ContactNumber, Address1 = profile.Address1, City = profile.City,
			State = profile.State, Country = profile.Country, PostalCode = profile.PostalCode });
		saved.FurthestStep.ShouldBe(5); saved.AcademicYearId.ShouldNotBeNull(); saved.DivisionId.ShouldNotBeNull();
		saved.PendingSteps.ShouldBeEmpty();
		saved = await PutAsync(school, "draft", new SaveInitialSetupDraftInput { CurrentStep = 5, Draft = saved.Draft, ConcurrencyStamp = saved.ConcurrencyStamp });
		var finished = await PutAsync(school, "complete", new SetupConcurrencyInput { ConcurrencyStamp = saved.ConcurrencyStamp });
		finished.IsCompleted.ShouldBeTrue(); finished.CompletedAt.ShouldNotBeNull();
		(await PutAsync(school, "complete", new SetupConcurrencyInput { ConcurrencyStamp = saved.ConcurrencyStamp })).IsCompleted.ShouldBeTrue();
		(await school.GetFromJsonAsync<HostSessionContextDto>("/api/host/session-context"))!.SetupCompleted.ShouldBeTrue();
	}
	[Fact]
	public async Task Setup_permissions_do_not_grant_user_creation()
	{
		using var school = await NewSchoolAsync(); var progress = await GetAsync(school);
		school.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			"InstituteSetup.BasicInformation.Edit,InstituteSetup.AcademicSetup.Add");
		(await GetAsync(school)).CanManageSetup.ShouldBeTrue();
		var response = await school.PostAsJsonAsync(Url + "/users", new CreateSetupUserInput {
			ConcurrencyStamp = progress.ConcurrencyStamp, RequestId = Guid.NewGuid(), UserName = "denied.staff",
			Email = "denied@example.test", Password = "Synthetic-Denied-39!", RoleNames = [] });
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await GetAsync(school)).CreatedUsers.ShouldBeEmpty();
	}
	[Fact]
	public async Task Another_schools_year_cannot_be_selected()
	{
		using var first = await NewSchoolAsync(); using var second = await NewSchoolAsync();
		var firstProgress = await PrepareYearStepAsync(first); var secondProgress = await PrepareYearStepAsync(second);
		secondProgress = await PutAsync(second, "academic-year", new SaveSetupYearInput {
			ConcurrencyStamp = secondProgress.ConcurrencyStamp, Name = "2026-2027",
			StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2027, 5, 31, 0, 0, 0, DateTimeKind.Utc) });
		var response = await first.PutAsJsonAsync(Url + "/academic-year", new SaveSetupYearInput {
			ConcurrencyStamp = firstProgress.ConcurrencyStamp, ExistingYearId = secondProgress.AcademicYearId, Name = "2026-2027",
			StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2027, 5, 31, 0, 0, 0, DateTimeKind.Utc) });
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await GetAsync(first)).AcademicYearId.ShouldBeNull();
		(await GetAsync(first)).CurrentStep.ShouldBe(2);
	}
	private async Task<InitialSetupDto> PrepareYearStepAsync(HttpClient school)
	{
		var progress = await GetAsync(school);
		progress = await PutAsync(school, "advance", new AdvanceSetupInput { Step = 0, ConcurrencyStamp = progress.ConcurrencyStamp });
		var profile = progress.Draft.Profile;
		return await PutAsync(school, "profile", new SaveSetupProfileInput {
			ConcurrencyStamp = progress.ConcurrencyStamp, InstituteName = profile.InstituteName, Email = profile.Email,
			ContactNumber = profile.ContactNumber, Address1 = profile.Address1, City = profile.City,
			State = profile.State, Country = profile.Country, PostalCode = profile.PostalCode });
	}
	[Fact]
	public async Task Denied_users_can_read_status_but_cannot_write_and_host_cannot_enter_school_setup()
	{
		using var school = await NewSchoolAsync();
		var current = await GetAsync(school);
		school.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
		var denied = await GetAsync(school); denied.CanManageSetup.ShouldBeFalse(); denied.Draft.Profile.InstituteName.ShouldBeEmpty();
		(await school.PutAsJsonAsync(Url + "/advance", new AdvanceSetupInput { Step = 0, ConcurrencyStamp = current.ConcurrencyStamp })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var host = Factory.CreateClient(); host.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		(await host.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var anonymous = CreateAnonymousClient();
		(await anonymous.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}
	private async Task<InitialSetupDto> GetAsync(HttpClient school) => (await school.GetFromJsonAsync<InitialSetupDto>(Url))!;
	private async Task<InitialSetupDto> PutAsync(HttpClient school, string step, object body)
	{
		var response = await school.PutAsJsonAsync(Url + "/" + step, body);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<InitialSetupDto>())!;
	}
	private async Task<HttpClient> NewSchoolAsync()
	{
		var hostId = Guid.NewGuid(); var code = "setup" + Guid.NewGuid().ToString("N");
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		using (currentTenant.Change(null))
		using (var unit = uow.Begin(requiresNew: true, isTransactional: true))
		{
			await scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(hostId, "host" + hostId.ToString("N"), "host@example.test"));
			await unit.CompleteAsync();
		}
		using var host = Factory.CreateClient();
		host.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		host.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, hostId.ToString());
		var response = await host.PostAsJsonAsync("/api/host/tenants", new CreateHostTenantInput { Name = code,
			InstituteName = "Synthetic Setup School", Email = "office@example.test", ContactNumber = "9876543210",
			Address1 = "Test Road", City = "Surat", State = "Gujarat", Country = "India", PostalCode = "395001",
			AdminEmailAddress = "admin@example.test", AdminPassword = "Synthetic-Setup-29!" });
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var created = (await response.Content.ReadFromJsonAsync<HostTenantDto>())!;
		Guid userId;
		using (currentTenant.Change(created.Id))
		using (var unit = uow.Begin(requiresNew: true))
		{
			userId = (await scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>().FindByNormalizedUserNameAsync("ADMIN"))!.Id;
			await unit.CompleteAsync();
		}
		var school = Factory.CreateClient();
		school.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, created.Id.ToString());
		school.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, userId.ToString());
		school.DefaultRequestHeaders.Add("__tenant", created.Id.ToString());
		return school;
	}
}
