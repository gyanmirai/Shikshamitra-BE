using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Enrollment;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra;

/// <summary>
/// Base class for in-process API tests. Provides a pre-configured <see cref="HttpClient"/>
/// that targets the test server with test authentication enabled.
/// </summary>
public abstract class ShikshaMitraApiTestBase
{
	protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	protected ShikshaMitraApiTestFactory Factory { get; }
	protected HttpClient Client { get; }

	protected ShikshaMitraApiTestBase(ShikshaMitraApiTestFactory factory)
	{
		Factory = factory;
		Client = factory.CreateClient();

		EmployeeTestHelper.DefaultRoleId = factory.SeededRoleId;
		EmployeeTestHelper.DefaultShiftId = factory.SeededShiftId;
		EmployeeTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
		EmployeeTestHelper.DefaultMediumStageId = factory.SeededMediumStageId;

		StudentEnrollmentTestHelper.DefaultAcademicMediumId = factory.SeededMediumId;
		StudentEnrollmentTestHelper.DefaultStandardId = factory.SeededStandardId;
		StudentEnrollmentTestHelper.DefaultDivisionId = factory.SeededDivisionId;
	}

	/// <summary>
	/// Creates an HttpClient that sends requests without authentication.
	/// Useful for testing 401/403 behavior.
	/// </summary>
	protected HttpClient CreateAnonymousClient()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
		return client;
	}

	/// <summary>
	/// Creates an HttpClient authenticated as the specified user ID.
	/// Uses the <c>X-Test-UserId</c> header supported by <see cref="Infrastructure.Auth.TestAuthHandler"/>.
	/// Useful for maker-checker tests where a second identity must approve/reject a record
	/// created by the default test user.
	/// </summary>
	protected HttpClient CreateClientAs(Guid userId)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
		return client;
	}

	/// <summary>
	/// POST + deserialize shortcut that asserts a success status code.
	/// </summary>
	protected async Task<TResponse> PostAndReadAsync<TResponse>(string url, object body)
	{
		var response = await Client.PostAsJsonAsync(url, body, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions)
			?? throw new InvalidOperationException($"Null body from POST {url}");
	}

	/// <summary>
	/// GET + deserialize shortcut that asserts a success status code.
	/// </summary>
	protected async Task<TResponse> GetAndReadAsync<TResponse>(string url)
	{
		var response = await Client.GetAsync(url);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions)
			?? throw new InvalidOperationException($"Null body from GET {url}");
	}

	protected async Task EnsureDefaultTestUserHasTenantWideScopeAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var scopeRepository = scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userScopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var userId = Guid.Parse(TestAuthHandler.TestUserId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var query = await scopeRepository.GetQueryableAsync();
			if (!query.Any(x =>
				    x.UserId == userId &&
				    x.AcademicYearId == null &&
				    x.EducationBoardId == null &&
				    x.EducationMediumId == null &&
				    x.EducationMediumStageId == null &&
				    x.StandardId == null &&
				    x.DivisionId == null &&
				    x.IsActive))
			{
				await scopeRepository.InsertAsync(
					new UserScopeAssignment(
						Guid.NewGuid(),
						tenantId,
						userId: userId,
						description: "API regression test tenant-wide HR scope"),
					autoSave: false);
			}

			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}
	}
}
