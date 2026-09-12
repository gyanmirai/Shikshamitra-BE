using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;
using ShikshaMitra.Finance.Fees.Collections;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Infrastructure.Auth;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.FeeStructureAssignment;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class FeeStructureApi_Tests : ShikshaMitraApiTestBase
{
    private const string FeeStructuresUrl = "/api/fee-structures";
    private const string MetadataUrl      = "/api/fee-structures-metadata";

    public FeeStructureApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
    {
        ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
        ConcessionRequestTestHelper.DefaultMediumId       = factory.SeededMediumId;
        ConcessionRequestTestHelper.DefaultStandardId     = factory.SeededStandardId;
        ConcessionRequestTestHelper.DefaultDivisionId     = factory.SeededDivisionId;
        FeeCollectionTestHelper.DefaultAcademicYearId     = factory.SeededAcademicYearId;
        FeeCollectionTestHelper.DefaultMediumId           = factory.SeededMediumId;
        FeeCollectionTestHelper.DefaultStandardId         = factory.SeededStandardId;
        FeeCollectionTestHelper.DefaultDivisionId         = factory.SeededDivisionId;
    }

    private async Task<Guid> GetOrCreateFeeStructureMetadataIdAsync()
    {
        var academicYearId = Factory.SeededAcademicYearId;

        var listResponse = await GetAndReadAsync<FeeStructureMetadataListResponse>(
            $"{MetadataUrl}?academicYearId={academicYearId}&pageSize=100");

        var matching = listResponse.Items?.SingleOrDefault(x => x.MediumId == Factory.SeededMediumId && x.BoardId == Factory.SeededBoardId);
        if (matching != null)
            return matching.Id;

        var generateResponse = await Client.PostAsync(
            $"{MetadataUrl}/generate?academicYearId={academicYearId}", null);
        generateResponse.EnsureSuccessStatusCode();

        var listAfter = await GetAndReadAsync<FeeStructureMetadataListResponse>(
            $"{MetadataUrl}?academicYearId={academicYearId}&pageSize=100");

        listAfter.Items.ShouldNotBeEmpty("Fee structure metadata should have been generated.");
        return listAfter.Items.Single(x => x.MediumId == Factory.SeededMediumId && x.BoardId == Factory.SeededBoardId).Id;
    }

    private async Task<Guid> SeedMappedFeeHeadAsync()
    {
        // Keep this fixture local: collection helpers predate mandatory accounting mappings.
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
        using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        var yearId = Factory.SeededAcademicYearId;
        var account = new AccountHead(Guid.NewGuid(), tenantId, yearId, $"Fee income {Guid.NewGuid():N}", AccountHeadType.Income, null, null);
        await services.GetRequiredService<IRepository<AccountHead, Guid>>().InsertAsync(account, autoSave: true);
        var income = new TransactionCategory(Guid.NewGuid(), tenantId, yearId, TransactionType.Income,
            $"Fee income {Guid.NewGuid():N}", null, defaultAccountHeadId: account.Id);
        await services.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income, autoSave: true);
        var category = new FeeCategory(Guid.NewGuid(), tenantId, yearId, $"Fees {Guid.NewGuid():N}", null);
        await services.GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category, autoSave: true);
        var head = new FeeHead(Guid.NewGuid(), tenantId, yearId, category.Id, $"Tuition {Guid.NewGuid():N}",
            FeeHeadModule.Finance, true, null, income.Id);
        await services.GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(head, autoSave: true);
        await uow.CompleteAsync();
        return head.Id;
    }

    [Fact]
    public async Task Workspace_Endpoints_Bind_The_Candidate_And_Reject_Incomplete_Publication()
    {
        var metadata = await GetOrCreateFeeStructureMetadataIdAsync();
        var settingsResponse = await Client.GetAsync($"{FeeStructuresUrl}/context?feeStructureMetadataId={metadata}&standardId={Factory.SeededStandardId}");
        settingsResponse.IsSuccessStatusCode.ShouldBeTrue(await settingsResponse.Content.ReadAsStringAsync());
        var settings = (await settingsResponse.Content.ReadFromJsonAsync<FeeStructureContextDto>(JsonOptions))!;
        settings.ClassName.ShouldNotBeNullOrWhiteSpace();
        settings.Permissions.ShouldContainKey("publish");
        var candidate = new StandardFeeStructureRequest { Name = $"Draft {Guid.NewGuid():N}", FeeHeads = [] };
        var createResponse = await Client.PostAsJsonAsync($"{FeeStructuresUrl}?feeStructureMetadataId={metadata}",
            new StandardFeeStructureSaveRequest { RequestId = Guid.NewGuid(), StandardId = Factory.SeededStandardId, StandardFeeStructures = candidate }, JsonOptions);
        createResponse.IsSuccessStatusCode.ShouldBeTrue(await createResponse.Content.ReadAsStringAsync());
        var created = (await createResponse.Content.ReadFromJsonAsync<FeeStructureCreateResponse>(JsonOptions))!;
        created.ConcurrencyStamp.ShouldNotBeNullOrWhiteSpace();
        var patchResponse = await Client.PatchAsJsonAsync($"{FeeStructuresUrl}/{created.Id}", new FeeStructurePatchRequest
        {
            RequestId = Guid.NewGuid(), ConcurrencyStamp = created.ConcurrencyStamp,
            Changes = System.Text.Json.JsonSerializer.SerializeToElement(new { name = candidate.Name + " updated" })
        }, JsonOptions);
        patchResponse.IsSuccessStatusCode.ShouldBeTrue(await patchResponse.Content.ReadAsStringAsync());
        created = (await patchResponse.Content.ReadFromJsonAsync<FeeStructureCreateResponse>(JsonOptions))!;
        created.Name.ShouldBe(candidate.Name + " updated");
        candidate.Name = created.Name;
        var previewResponse = await Client.PostAsJsonAsync($"{FeeStructuresUrl}/preview", new FeeStructurePreviewRequest
        {
            FeeStructureMetadataId = metadata, StandardId = Factory.SeededStandardId, StructureId = created.Id, StandardFeeStructures = candidate
        }, JsonOptions);
        previewResponse.EnsureSuccessStatusCode();
        var preview = (await previewResponse.Content.ReadFromJsonAsync<FeeStructurePreviewDto>(JsonOptions))!;
        preview.Candidate.Name.ShouldBe(candidate.Name);
        preview.IsReady.ShouldBeFalse();
        preview.Errors.ShouldNotContain(x => x.Contains("Receipt Book"));
        var publish = await Client.PostAsJsonAsync($"{FeeStructuresUrl}/{created.Id}/publish", new FeeStructurePublishRequest { ConcurrencyStamp = created.ConcurrencyStamp }, JsonOptions);
        publish.IsSuccessStatusCode.ShouldBeFalse();
        (await publish.Content.ReadAsStringAsync()).ShouldContain("Finance:FeeStructure:InvalidConfiguration");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Enforce_Null_PayableMonth_When_IsOneTime_Is_False()
    {
        // Arrange
        var feeHeadId      = await SeedMappedFeeHeadAsync();
        var metadataId     = await GetOrCreateFeeStructureMetadataIdAsync();

        var request = new StandardFeeStructureSaveRequest
        {
            StandardId = Factory.SeededStandardId,
            StandardFeeStructures = new StandardFeeStructureRequest
            {
                Name = $"Test Enforce Null {Guid.NewGuid():N}"[..24],
                FeeHeads =
                [
                    new FeeHeadAssignmentRequest
                    {
                        FeeHeadId    = feeHeadId,
                        Amount       = 12_000m,
                        IsOneTime    = false, // Recurring fee
                        IsOptional   = false,
                        PayableMonth = MonthEnum.April, // Attempt to set PayableMonth
                        PayableYear  = 2026            // Attempt to set PayableYear
                    },
                ],
            },
        };

        // Act - Create the fee structure
        var response = await Client.PostAsJsonAsync(
            $"{FeeStructuresUrl}?feeStructureMetadataId={metadataId}",
            request, JsonOptions);
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());

        // Act - Fetch the fee structure details
        var getResponse = await GetAndReadAsync<StandardFeeStructureResponse>(
            $"{FeeStructuresUrl}?standardId={Factory.SeededStandardId}");

        // Assert
        getResponse.ShouldNotBeNull();
        var structureItem = getResponse.StandardFeeStructures.FirstOrDefault(x => x.Name == request.StandardFeeStructures.Name);
        structureItem.ShouldNotBeNull();
        structureItem.FeeHeads.ShouldNotBeEmpty();

        var feeHead = structureItem.FeeHeads.FirstOrDefault(fh => fh.FeeHeadId == feeHeadId);
        feeHead.ShouldNotBeNull();
        feeHead.IsOneTime.ShouldBeFalse();
        feeHead.PayableMonth.ShouldBeNull();
        feeHead.PayableYear.ShouldBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Keep_PayableMonth_When_IsOneTime_Is_True()
    {
        // Arrange
        var feeHeadId      = await SeedMappedFeeHeadAsync();
        var metadataId     = await GetOrCreateFeeStructureMetadataIdAsync();

        var request = new StandardFeeStructureSaveRequest
        {
            StandardId = Factory.SeededStandardId,
            StandardFeeStructures = new StandardFeeStructureRequest
            {
                Name = $"Test Keep Month {Guid.NewGuid():N}"[..24],
                FeeHeads =
                [
                    new FeeHeadAssignmentRequest
                    {
                        FeeHeadId    = feeHeadId,
                        Amount       = 3_000m,
                        IsOneTime    = true, // One-time fee
                        IsOptional   = false,
                        PayableMonth = MonthEnum.June,
                        PayableYear  = 2026
                    },
                ],
            },
        };

        // Act - Create the fee structure
        var response = await Client.PostAsJsonAsync(
            $"{FeeStructuresUrl}?feeStructureMetadataId={metadataId}",
            request, JsonOptions);
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());

        // Act - Fetch the fee structure details
        var getResponse = await GetAndReadAsync<StandardFeeStructureResponse>(
            $"{FeeStructuresUrl}?standardId={Factory.SeededStandardId}");

        // Assert
        getResponse.ShouldNotBeNull();
        var structureItem = getResponse.StandardFeeStructures.FirstOrDefault(x => x.Name == request.StandardFeeStructures.Name);
        structureItem.ShouldNotBeNull();
        structureItem.FeeHeads.ShouldNotBeEmpty();

        var feeHead = structureItem.FeeHeads.FirstOrDefault(fh => fh.FeeHeadId == feeHeadId);
        feeHead.ShouldNotBeNull();
        feeHead.IsOneTime.ShouldBeTrue();
        feeHead.PayableMonth.ShouldBe(MonthEnum.June);
        feeHead.PayableYear.ShouldBe(2026);
    }
}
