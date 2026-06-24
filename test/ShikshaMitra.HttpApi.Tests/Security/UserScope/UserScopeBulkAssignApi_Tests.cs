using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Security.UserScope;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class UserScopeBulkAssignApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/app/user-scope/bulk-assign";

    public UserScopeBulkAssignApi_Tests(ShikshaMitraApiTestFactory factory)
        : base(factory)
    {
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task UserScopeBulkAssign_Should_Return_Meaningful_InvalidHierarchy_Detail()
    {
        Factory.SeededAcademicYearId.ShouldNotBe(Guid.Empty);
        Factory.SeededBoardId.ShouldNotBe(Guid.Empty);
        var mismatchedMediumId = await GetMediumFromDifferentBoardAsync(Factory.SeededBoardId);

        var response = await Client.PostAsJsonAsync(
            BaseUrl,
            new
            {
                academicYearId = Factory.SeededAcademicYearId,
                userIds = new[] { Guid.Parse(TestAuthHandler.TestUserId) },
                scopes = new[]
                {
                    new
                    {
                        educationBoardId = Factory.SeededBoardId,
                        educationMediumId = mismatchedMediumId
                    }
                },
                clearExisting = false
            },
            JsonOptions);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("errorCode").GetString()
            .ShouldBe(UserScopeErrorCodes.InvalidHierarchy);

        var detail = document.RootElement.GetProperty("detail").GetString();
        detail.ShouldNotBeNullOrWhiteSpace();
        detail.ShouldContain("valid hierarchy path");
        detail.ShouldNotBe("One or more business rules were violated.");
    }

    private async Task<Guid> GetMediumFromDifferentBoardAsync(Guid boardId)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var boardRepository = scope.ServiceProvider.GetRequiredService<IRepository<EducationBoard, Guid>>();
        var mediumRepository = scope.ServiceProvider.GetRequiredService<IRepository<EducationMedium, Guid>>();

        using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var boards = await boardRepository.GetListAsync(x =>
                x.AcademicYearId == Factory.SeededAcademicYearId && x.Id != boardId);
            var otherBoard = boards.FirstOrDefault();
            otherBoard.ShouldNotBeNull(
                "Seed data must include at least two boards in the same academic year for the invalid hierarchy test.");

            var otherBoardMedium = (await mediumRepository.GetListAsync(x =>
                    x.EducationBoardId == otherBoard.Id))
                .FirstOrDefault();
            otherBoardMedium.ShouldNotBeNull(
                "Seed data must include a medium under the second board for the invalid hierarchy test.");

            await uow.CompleteAsync();
            return otherBoardMedium.Id;
        }
    }
}
