using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.ConcessionRequests;

public abstract class ConcessionRequestApiTestsBase : ShikshaMitraApiTestBase
{
	protected const string BaseUrl = "/api/concession-request";
	protected const string ConcessionRequestsBaseUrl = "/api/concession-requests";

	protected ConcessionRequestApiTestsBase(ShikshaMitraApiTestFactory factory) : base(factory)
	{
		ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
		ConcessionRequestTestHelper.DefaultMediumId = factory.SeededMediumId;
		ConcessionRequestTestHelper.DefaultStandardId = factory.SeededStandardId;
		ConcessionRequestTestHelper.DefaultDivisionId = factory.SeededDivisionId;
	}

	protected async Task<Guid> CreatePendingRequestWithFeeHeadAsync()
	{
		var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var feeCategoryId = await ConcessionRequestTestHelper.CreateFeeCategoryAsync(Client);
		var feeHeadId = await ConcessionRequestTestHelper.CreateFeeHeadAsync(Client, feeCategoryId);

		return await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, typeId, new[] { feeHeadId });
	}

	protected async Task<HttpClient> CreateScopedCheckerClientAsync()
	{
		var checkerId = Guid.NewGuid();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenant = services.GetRequiredService<ICurrentTenant>();
		var uowManager = services.GetRequiredService<IUnitOfWorkManager>();
		var dbContext = services.GetRequiredService<ShikshaMitraDbContext>();
		var assignments = services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		using (tenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var userName = $"concession-checker-{checkerId:N}";
			await dbContext.Set<IdentityUser>().AddAsync(new IdentityUser(
				checkerId, userName, $"{userName}@example.test", tenantId));
			await assignments.InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), tenantId, checkerId, description: "Concession API checker scope"), autoSave: false);
			await uow.CompleteAsync();
		}
		await services.GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(checkerId);
		return CreateClientAs(checkerId);
	}
}
