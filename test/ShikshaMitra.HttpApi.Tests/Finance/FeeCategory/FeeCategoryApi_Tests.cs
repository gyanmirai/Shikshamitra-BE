using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.FeeCategories;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class FeeCategoryApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/fee-categories";

    private readonly Guid _academicYearId;

    public FeeCategoryApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
    {
        _academicYearId = factory.SeededAcademicYearId;
    }

    private FeeCategoryCreateRequest BuildCreateRequest(string? name = null) => new()
    {
        Name = name ?? $"Category-{Guid.NewGuid():N}",
        Description = "Created by test",
        FeeHeads = new List<FeeHeadItemRequest>
        {
            new()
            {
                Name = $"Head-{Guid.NewGuid():N}".Substring(0, 20),
                Module = FeeHeadModule.Academics,
                IsConcessionApplied = true,
                Description = "Head description"
            }
        }
    };

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Create_FeeCategory_With_Nested_FeeHeads()
    {
        var input = BuildCreateRequest();

        var response = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
        body.ShouldNotBeNull();
        body!.Id.ShouldNotBe(Guid.Empty);
        body.Name.ShouldBe(input.Name);
        body.IsActive.ShouldBeTrue();
        body.FeeHeads.Count.ShouldBe(1);
        body.TotalFeeHeads.ShouldBe(1);
        body.FeeHeads[0].Module.ShouldBe(FeeHeadModule.Academics);
        body.FeeHeads[0].IsConcessionApplied.ShouldBeTrue();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Create_FeeCategory_With_FeeHead_Without_Module()
    {
        var input = BuildCreateRequest();
        input.FeeHeads[0].Module = null;

        var response = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
        body.ShouldNotBeNull();
        body!.FeeHeads.Count.ShouldBe(1);
        body.FeeHeads[0].Module.ShouldBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Reject_Create_When_Name_Duplicate_In_Same_AcademicYear()
    {
        var name = $"Category-{Guid.NewGuid():N}";
        var first = BuildCreateRequest(name);
        (await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", first, JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = BuildCreateRequest(name);
        var response = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", second, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Reject_Create_When_No_FeeHeads_Provided()
    {
        var input = BuildCreateRequest();
        input.FeeHeads.Clear();

        var response = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_ById_Return_FeeHeads()
    {
        var created = await CreateCategoryAsync();

        var response = await Client.GetAsync($"{BaseUrl}/{created.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
        body.ShouldNotBeNull();
        body!.Id.ShouldBe(created.Id);
        body.FeeHeads.Count.ShouldBe(1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Update_FeeCategory_Add_And_Remove_FeeHeads()
    {
        var created = await CreateCategoryAsync();
        var existingHead = created.FeeHeads[0];

        var update = new FeeCategoryUpdateRequest
        {
            Name = created.Name,
            Description = "Updated description",
            FeeHeads = new List<FeeHeadItemRequest>
            {
                new()
                {
                    Id = existingHead.Id,
                    Name = existingHead.Name,
                    Module = FeeHeadModule.Transport,
                    IsConcessionApplied = false,
                    Description = "Updated head"
                },
                new()
                {
                    Name = $"Head-{Guid.NewGuid():N}".Substring(0, 20),
                    Module = FeeHeadModule.Library,
                    IsConcessionApplied = true,
                    Description = "New head"
                }
            }
        };

        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", update, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
        body.ShouldNotBeNull();
        body!.Description.ShouldBe("Updated description");
        body.FeeHeads.Count.ShouldBe(2);
        body.FeeHeads.ShouldContain(h => h.Id == existingHead.Id && h.Module == FeeHeadModule.Transport);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Cascade_Status_To_FeeHeads_On_Deactivate()
    {
        var created = await CreateCategoryAsync();

        var statusInput = new FeeCategoryStatusUpdateRequest
        {
            IsActive = false,
            DeactivationReason = "No longer needed"
        };

        var response = await Client.PatchAsJsonAsync($"{BaseUrl}/{created.Id}/status", statusInput, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
        body.ShouldNotBeNull();
        body!.IsActive.ShouldBeFalse();
        body.DeactivationReason.ShouldBe("No longer needed");
        body.FeeHeads.ShouldAllBe(h => !h.IsActive && h.DeactivationReason == "No longer needed");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Reject_Deactivation_When_Reason_Is_Whitespace()
    {
        var created = await CreateCategoryAsync();
        var statusInput = new FeeCategoryStatusUpdateRequest
        {
            IsActive = false,
            DeactivationReason = " \t "
        };

        var response = await Client.PatchAsJsonAsync($"{BaseUrl}/{created.Id}/status", statusInput, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Activate_Without_A_Deactivation_Reason()
    {
        var created = await CreateCategoryAsync();
        var deactivateResponse = await Client.PatchAsJsonAsync(
            $"{BaseUrl}/{created.Id}/status",
            new FeeCategoryStatusUpdateRequest
            {
                IsActive = false,
                DeactivationReason = "No longer needed"
            },
            JsonOptions);
        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var activateResponse = await Client.PatchAsJsonAsync(
            $"{BaseUrl}/{created.Id}/status",
            new FeeCategoryStatusUpdateRequest { IsActive = true },
            JsonOptions);

        activateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await activateResponse.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
        body.ShouldNotBeNull();
        body!.IsActive.ShouldBeTrue();
        body.DeactivationReason.ShouldBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Activate_FeeHead_Without_A_Deactivation_Reason()
    {
        var created = await CreateCategoryAsync();
        var feeHeadId = created.FeeHeads[0].Id;
        var deactivateResponse = await Client.PatchAsJsonAsync(
            $"/api/fee-heads/{feeHeadId}/status",
            new FeeHeadStatusUpdateRequest
            {
                IsActive = false,
                DeactivationReason = "No longer needed"
            },
            JsonOptions);
        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var activateResponse = await Client.PatchAsJsonAsync(
            $"/api/fee-heads/{feeHeadId}/status",
            new FeeHeadStatusUpdateRequest { IsActive = true },
            JsonOptions);

        activateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await activateResponse.Content.ReadFromJsonAsync<FeeHeadResponse>(JsonOptions);
        body.ShouldNotBeNull();
        body!.IsActive.ShouldBeTrue();
        body.DeactivationReason.ShouldBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Delete_FeeCategory_When_Not_In_Use()
    {
        var created = await CreateCategoryAsync();

        var response = await Client.DeleteAsync($"{BaseUrl}/{created.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResponse = await Client.GetAsync($"{BaseUrl}/{created.Id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_List_Returns_Paged_Shape_With_FeeHeads()
    {
        await CreateCategoryAsync();

        var response = await Client.GetAsync($"{BaseUrl}?academicYearId={_academicYearId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FeeCategoryListResponse>(JsonOptions);
        body.ShouldNotBeNull();
        body!.Items.ShouldNotBeEmpty();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_FeeHeadModules_List()
    {
        var response = await Client.GetAsync("/api/fee-head-modules");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<FeeHeadModuleDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body!.ShouldNotBeEmpty();
        body.ShouldContain(m => m.Value == (int)FeeHeadModule.Academics);
    }

    private async Task<FeeCategoryResponse> CreateCategoryAsync(string? name = null)
    {
        var input = BuildCreateRequest(name);
        var response = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", input, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions))!;
    }
}
