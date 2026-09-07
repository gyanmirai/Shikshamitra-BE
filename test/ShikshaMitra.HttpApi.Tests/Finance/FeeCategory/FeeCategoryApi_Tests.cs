using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.FeeCategories;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class FeeCategoryApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/fee-categories";
	private static int _academicYearSequence;

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
        body.FeeHeads[0].IsCurrentRevision.ShouldBeTrue();
        body.FeeHeads[0].RevisionNumber.ShouldBe(1);
        body.FeeHeads[0].IsEffectivelyActive.ShouldBeTrue();
        body.ConcurrencyStamp.ShouldNotBeNullOrWhiteSpace();
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
	public async Task Should_Reject_Normalized_Case_Insensitive_Duplicate()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var first = BuildCreateRequest($"CBSE   Tuition {suffix}");
        (await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", first, JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var duplicate = BuildCreateRequest($"  cbse tuition {suffix}  ");
        var response = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", duplicate, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Enforce_Normalized_Duplicate_Protection_At_The_Database()
	{
		var suffix = Guid.NewGuid().ToString("N");
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var firstScope = Factory.Services.CreateScope();
		var firstTenant = firstScope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var firstUowManager = firstScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var firstRepository = firstScope.ServiceProvider.GetRequiredService<IRepository<FeeCategory, Guid>>();
		using (firstTenant.Change(tenantId))
		using (var firstUow = firstUowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await firstRepository.InsertAsync(new FeeCategory(
				Guid.NewGuid(), tenantId, _academicYearId, $"Concurrent CBSE {suffix}", null));
			await firstUow.CompleteAsync();
		}

		var exception = await Should.ThrowAsync<Exception>(async () =>
		{
			using var secondScope = Factory.Services.CreateScope();
			var secondTenant = secondScope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var secondUowManager = secondScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var secondRepository = secondScope.ServiceProvider.GetRequiredService<IRepository<FeeCategory, Guid>>();
			using (secondTenant.Change(tenantId))
			using (var secondUow = secondUowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				await secondRepository.InsertAsync(new FeeCategory(
					Guid.NewGuid(), tenantId, _academicYearId, $"  concurrent   cbse {suffix}  ", null));
				await secondUow.CompleteAsync();
			}
		});
		exception.GetBaseException().Message.ShouldContain("UNIQUE", Case.Insensitive);
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
    public async Task Should_Roll_Back_Entire_Create_When_A_Nested_Head_Is_Invalid()
    {
        var name = $"Atomic-{Guid.NewGuid():N}";
        var input = BuildCreateRequest(name);
        input.FeeHeads.Add(new FeeHeadItemRequest
        {
            Name = input.FeeHeads[0].Name.ToUpperInvariant(),
            Module = FeeHeadModule.Transport,
            Description = "Duplicate normalized name"
        });

        var response = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", input, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var list = await Client.GetFromJsonAsync<FeeCategoryListResponse>(
            $"{BaseUrl}?academicYearId={_academicYearId}&search={Uri.EscapeDataString(name)}", JsonOptions);
        list!.TotalCount.ShouldBe(0);
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
    public async Task Should_Update_FeeCategory_Metadata_Without_Changing_FeeHeads()
    {
        var created = await CreateCategoryAsync();
        var update = new FeeCategoryUpdateRequest
        {
            Name = created.Name,
            Description = "Updated description",
            ConcurrencyStamp = created.ConcurrencyStamp
        };

        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", update, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
        body.ShouldNotBeNull();
        body!.Description.ShouldBe("Updated description");
        body.FeeHeads.Count.ShouldBe(1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Preserve_Head_Status_And_Change_Effective_Status_On_Deactivate()
    {
        var created = await CreateCategoryAsync();

        var statusInput = new FeeCategoryStatusUpdateRequest
        {
            IsActive = false,
            DeactivationReason = "No longer needed",
            ConcurrencyStamp = created.ConcurrencyStamp
        };

        var response = await Client.PatchAsJsonAsync($"{BaseUrl}/{created.Id}/status", statusInput, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
        body.ShouldNotBeNull();
        body!.IsActive.ShouldBeFalse();
        body.DeactivationReason.ShouldBe("No longer needed");
        body.FeeHeads.ShouldAllBe(h => h.IsActive && !h.IsEffectivelyActive);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Not_Reactivate_Individually_Archived_Head_When_Category_Is_Reactivated()
    {
        var created = await CreateCategoryAsync();
        var head = created.FeeHeads[0];
        var headResponse = await Client.PatchAsJsonAsync($"/api/fee-heads/{head.Id}/status", new FeeHeadStatusUpdateRequest
        {
            IsActive = false,
            DeactivationReason = "Optional charge retired",
            ConcurrencyStamp = head.ConcurrencyStamp
        }, JsonOptions);
        headResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var currentCategory = await Client.GetFromJsonAsync<FeeCategoryResponse>($"{BaseUrl}/{created.Id}", JsonOptions);

        var archived = await Client.PatchAsJsonAsync($"{BaseUrl}/{created.Id}/status", new FeeCategoryStatusUpdateRequest
        {
            IsActive = false,
            DeactivationReason = "Category paused",
            ConcurrencyStamp = currentCategory!.ConcurrencyStamp
        }, JsonOptions);
        archived.StatusCode.ShouldBe(HttpStatusCode.OK);
        var archivedBody = await archived.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
        var reactivated = await Client.PatchAsJsonAsync($"{BaseUrl}/{created.Id}/status", new FeeCategoryStatusUpdateRequest
        {
            IsActive = true,
            ConcurrencyStamp = archivedBody!.ConcurrencyStamp
        }, JsonOptions);

        reactivated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await reactivated.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
        body!.FeeHeads[0].IsActive.ShouldBeFalse();
        body.FeeHeads[0].IsEffectivelyActive.ShouldBeFalse();
        body.FeeHeads[0].DeactivationReason.ShouldBe("Optional charge retired");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Reject_Stale_Category_Update()
    {
        var created = await CreateCategoryAsync();
        var update = new FeeCategoryUpdateRequest
        {
            Name = created.Name,
            Description = "First update",
            ConcurrencyStamp = created.ConcurrencyStamp
        };
        (await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", update, JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        update.Description = "Stale update";
        var stale = await Client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", update, JsonOptions);

        stale.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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
        body.ShouldContain(m => m.Value == nameof(FeeHeadModule.Academics));
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Move_Unused_FeeHead_To_Another_Category()
    {
        var source = await CreateCategoryAsync();
        var destination = await CreateCategoryAsync();
        var head = source.FeeHeads[0];
        var update = new FeeHeadUpdateRequest
        {
            FeeCategoryId = destination.Id,
            Name = head.Name,
            Module = head.Module,
            IsConcessionApplied = head.IsConcessionApplied,
            Description = head.Description,
            ConcurrencyStamp = head.ConcurrencyStamp
        };

        var response = await Client.PutAsJsonAsync($"/api/fee-heads/{head.Id}", update, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FeeHeadResponse>(JsonOptions);
        body!.FeeCategoryId.ShouldBe(destination.Id);
        body.RevisionNumber.ShouldBe(1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Revision_And_Hide_Superseded_Head_From_Default_Selector()
    {
        var category = await CreateCategoryAsync();
        var head = category.FeeHeads[0];
		await MarkHeadUsedAsync(head.Id);
        var revision = new FeeHeadRevisionRequest
        {
            FeeCategoryId = category.Id,
            Name = $"{head.Name}-R2",
            Module = FeeHeadModule.Transport,
            IsConcessionApplied = false,
            Description = "Revised fee head",
            ConcurrencyStamp = head.ConcurrencyStamp,
            Reason = "Configuration changed"
        };

        var response = await Client.PostAsJsonAsync($"/api/fee-heads/{head.Id}/revisions", revision, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var replacement = await response.Content.ReadFromJsonAsync<FeeHeadResponse>(JsonOptions);
        replacement!.RevisionGroupId.ShouldBe(head.RevisionGroupId);
        replacement.RevisionNumber.ShouldBe(2);
        replacement.PreviousRevisionId.ShouldBe(head.Id);
        replacement.IsCurrentRevision.ShouldBeTrue();
        replacement.RevisionReason.ShouldBe("Configuration changed");

        var list = await Client.GetFromJsonAsync<FeeHeadListResponse>(
            $"/api/fee-heads?academicYearId={_academicYearId}&feeCategoryId={category.Id}", JsonOptions);
        list!.Items.Count.ShouldBe(1);
        list.Items[0].Id.ShouldBe(replacement.Id);

        var detail = await Client.GetFromJsonAsync<FeeCategoryResponse>($"{BaseUrl}/{category.Id}", JsonOptions);
        detail!.FeeHeads.Count.ShouldBe(2);
		detail.FeeHeads.ShouldContain(item => item.Id == head.Id && !item.IsCurrentRevision);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Edit_Only_Description_Of_Used_Head_Without_A_Revision()
	{
		var category = await CreateCategoryAsync();
		var head = category.FeeHeads[0];
		await MarkHeadUsedAsync(head.Id);
		var response = await Client.PutAsJsonAsync($"/api/fee-heads/{head.Id}", new FeeHeadUpdateRequest
		{
			FeeCategoryId = category.Id,
			Name = head.Name,
			Module = head.Module,
			IsConcessionApplied = head.IsConcessionApplied,
			Description = "Corrected description",
			ConcurrencyStamp = head.ConcurrencyStamp
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var updated = await response.Content.ReadFromJsonAsync<FeeHeadResponse>(JsonOptions);
		updated!.Id.ShouldBe(head.Id);
		updated.RevisionNumber.ShouldBe(1);
		updated.Description.ShouldBe("Corrected description");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Destination_Category_Revision_When_Moving_A_Used_Head()
	{
		var source = await CreateCategoryAsync();
		var destination = await CreateCategoryAsync();
		var head = source.FeeHeads[0];
		await MarkHeadUsedAsync(head.Id);

		var response = await Client.PostAsJsonAsync($"/api/fee-heads/{head.Id}/revisions", new FeeHeadRevisionRequest
		{
			FeeCategoryId = destination.Id,
			Name = head.Name,
			Module = head.Module,
			IsConcessionApplied = head.IsConcessionApplied,
			Description = head.Description,
			ConcurrencyStamp = head.ConcurrencyStamp,
			Reason = "Moved to the correct category"
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var revision = await response.Content.ReadFromJsonAsync<FeeHeadResponse>(JsonOptions);
		revision!.FeeCategoryId.ShouldBe(destination.Id);
		revision.PreviousRevisionId.ShouldBe(head.Id);
		revision.RevisionNumber.ShouldBe(2);
		var sourceDetail = await Client.GetFromJsonAsync<FeeCategoryResponse>($"{BaseUrl}/{source.Id}", JsonOptions);
		sourceDetail!.FeeHeads.ShouldContain(item => item.Id == head.Id && !item.IsCurrentRevision);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Foreign_Academic_Year_Category_For_A_New_Head()
	{
		var category = await CreateCategoryAsync();
		var otherYearId = await CreateAcademicYearAsync();
		var response = await Client.PostAsJsonAsync($"/api/fee-heads?academicYearId={otherYearId}", new FeeHeadCreateRequest
		{
			FeeCategoryId = category.Id,
			Name = "Foreign year head",
			Module = FeeHeadModule.Academics,
			IsConcessionApplied = false
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Not_Expose_A_Foreign_Tenant_Category()
	{
		var otherTenantId = Guid.NewGuid();
		var categoryId = Guid.NewGuid();
		using (var scope = Factory.Services.CreateScope())
		{
			var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<FeeCategory, Guid>>();
			using (currentTenant.Change(otherTenantId))
			using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
			{
				await repository.InsertAsync(new FeeCategory(
					categoryId, otherTenantId, _academicYearId, $"Other tenant {Guid.NewGuid():N}", null));
				await uow.CompleteAsync();
			}
		}

		var response = await Client.GetAsync($"{BaseUrl}/{categoryId}");

		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reject_Stale_Fee_Head_Update()
	{
		var category = await CreateCategoryAsync();
		var head = category.FeeHeads[0];
		var request = new FeeHeadUpdateRequest
		{
			FeeCategoryId = category.Id,
			Name = head.Name,
			Module = head.Module,
			IsConcessionApplied = head.IsConcessionApplied,
			Description = "First correction",
			ConcurrencyStamp = head.ConcurrencyStamp
		};
		(await Client.PutAsJsonAsync($"/api/fee-heads/{head.Id}", request, JsonOptions))
			.StatusCode.ShouldBe(HttpStatusCode.OK);
		request.Description = "Stale correction";

		var stale = await Client.PutAsJsonAsync($"/api/fee-heads/{head.Id}", request, JsonOptions);

		stale.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Block_Deleting_Only_Current_Head_From_Active_Category()
    {
        var category = await CreateCategoryAsync();

        var response = await Client.DeleteAsync($"/api/fee-heads/{category.FeeHeads[0].Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Normalize_Whitespace_While_Preserving_Acronyms_And_Punctuation()
	{
		var input = BuildCreateRequest("  CBSE\tRTE (25%)  ");
		input.FeeHeads[0].Name = "  PTA  /\tSTEM  ";

		var response = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
		body!.Name.ShouldBe("CBSE RTE (25%)");
		body.FeeHeads[0].Name.ShouldBe("PTA / STEM");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Non_English_Name_Characters()
	{
		var input = BuildCreateRequest("ટ્યુશન Fee");

		var response = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Category_With_Multiple_Heads_Atomically()
	{
		var input = BuildCreateRequest();
		input.FeeHeads.Add(new FeeHeadItemRequest
		{
			Name = $"Transport-{Guid.NewGuid():N}"[..30],
			Module = FeeHeadModule.Transport,
			Description = "Bus charge"
		});

		var response = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
		body!.FeeHeads.Count.ShouldBe(2);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Archive_Reasons()
	{
		var category = await CreateCategoryAsync();
		var categoryResponse = await Client.PatchAsJsonAsync($"{BaseUrl}/{category.Id}/status", new FeeCategoryStatusUpdateRequest
		{
			IsActive = false,
			ConcurrencyStamp = category.ConcurrencyStamp
		}, JsonOptions);
		var headResponse = await Client.PatchAsJsonAsync($"/api/fee-heads/{category.FeeHeads[0].Id}/status", new FeeHeadStatusUpdateRequest
		{
			IsActive = false,
			ConcurrencyStamp = category.FeeHeads[0].ConcurrencyStamp
		}, JsonOptions);

		categoryResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		headResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Not_Reactivate_A_Category_Without_A_Current_Head()
	{
		var category = await CreateCategoryAsync();
		var archived = await Client.PatchAsJsonAsync($"{BaseUrl}/{category.Id}/status", new FeeCategoryStatusUpdateRequest
		{
			IsActive = false,
			DeactivationReason = "Paused",
			ConcurrencyStamp = category.ConcurrencyStamp
		}, JsonOptions);
		var archivedBody = await archived.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
		(await Client.DeleteAsync($"/api/fee-heads/{category.FeeHeads[0].Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);

		var reactivated = await Client.PatchAsJsonAsync($"{BaseUrl}/{category.Id}/status", new FeeCategoryStatusUpdateRequest
		{
			IsActive = true,
			ConcurrencyStamp = archivedBody!.ConcurrencyStamp
		}, JsonOptions);

		reactivated.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Return_Only_Effectively_Active_Current_Heads_When_Requested()
	{
		var category = await CreateCategoryAsync();
		var archived = await Client.PatchAsJsonAsync($"{BaseUrl}/{category.Id}/status", new FeeCategoryStatusUpdateRequest
		{
			IsActive = false,
			DeactivationReason = "Paused",
			ConcurrencyStamp = category.ConcurrencyStamp
		}, JsonOptions);
		archived.StatusCode.ShouldBe(HttpStatusCode.OK);

		var list = await Client.GetFromJsonAsync<FeeHeadListResponse>(
			$"/api/fee-heads?academicYearId={_academicYearId}&feeCategoryId={category.Id}&effectiveOnly=true&pageSize=100", JsonOptions);

		list!.Items.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Show_Exact_Usage_Reference_And_Block_Delete()
	{
		var category = await CreateCategoryAsync();
		var referenceId = await MarkHeadUsedAsync(category.FeeHeads[0].Id);

		var impact = await Client.GetFromJsonAsync<FeeHeadImpactResponse>(
			$"/api/fee-heads/{category.FeeHeads[0].Id}/impact", JsonOptions);
		var deletion = await Client.DeleteAsync($"/api/fee-heads/{category.FeeHeads[0].Id}");

		impact!.IsUsed.ShouldBeTrue();
		impact.Blockers.ShouldContain(item => item.Contains(referenceId.ToString("D"), StringComparison.OrdinalIgnoreCase));
		deletion.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Preview_And_Copy_Current_Heads_Without_Revision_Lineage()
	{
		var source = await CreateCategoryAsync();
		var archivedHeadResponse = await Client.PatchAsJsonAsync($"/api/fee-heads/{source.FeeHeads[0].Id}/status", new FeeHeadStatusUpdateRequest
		{
			IsActive = false,
			DeactivationReason = "Retired for the next year",
			ConcurrencyStamp = source.FeeHeads[0].ConcurrencyStamp
		}, JsonOptions);
		archivedHeadResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		source = await (await Client.GetAsync($"{BaseUrl}/{source.Id}")).Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions) ?? source;
		var archivedCategoryResponse = await Client.PatchAsJsonAsync($"{BaseUrl}/{source.Id}/status", new FeeCategoryStatusUpdateRequest
		{
			IsActive = false,
			DeactivationReason = "Paused in the source year",
			ConcurrencyStamp = source.ConcurrencyStamp
		}, JsonOptions);
		archivedCategoryResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await archivedCategoryResponse.Content.ReadAsStringAsync());
		source = (await archivedCategoryResponse.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions))!;
		var destinationYearId = await CreateAcademicYearAsync();
		var previewRequest = new FeeCategoryCopyPreviewRequest
		{
			SourceAcademicYearId = _academicYearId,
			DestinationAcademicYearId = destinationYearId,
			CategoryIds = [source.Id]
		};

		var previewResponse = await Client.PostAsJsonAsync($"{BaseUrl}/copy/preview", previewRequest, JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = await previewResponse.Content.ReadFromJsonAsync<FeeCategoryCopyPreviewResponse>(JsonOptions);
		preview!.NewCount.ShouldBe(1);
		preview.Items[0].FeeHeads.Count.ShouldBe(1);
		preview.Items[0].FeeHeads[0].Classification.ShouldBe("New");

		var copyResponse = await Client.PostAsJsonAsync($"{BaseUrl}/copy", new FeeCategoryCopyCommitRequest
		{
			SourceAcademicYearId = _academicYearId,
			DestinationAcademicYearId = destinationYearId,
			CategoryIds = [source.Id]
		}, JsonOptions);
		copyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await copyResponse.Content.ReadAsStringAsync());
		var copied = await copyResponse.Content.ReadFromJsonAsync<FeeCategoryCopyResult>(JsonOptions);
		copied!.Created.ShouldBe(1);
		copied.Unresolved.ShouldBe(0);
		copied.Categories[0].IsActive.ShouldBeFalse();
		copied.Categories[0].FeeHeads[0].IsActive.ShouldBeFalse();
		copied.Categories[0].DeactivationReason.ShouldBe("Paused in the source year");
		copied.Categories[0].FeeHeads[0].DeactivationReason.ShouldBe("Retired for the next year");
		copied.Categories[0].FeeHeads[0].RevisionNumber.ShouldBe(1);
		copied.Categories[0].FeeHeads[0].RevisionGroupId.ShouldBe(copied.Categories[0].FeeHeads[0].Id);
		copied.Categories[0].FeeHeads[0].RevisionGroupId.ShouldNotBe(source.FeeHeads[0].RevisionGroupId);

		var repeated = await Client.PostAsJsonAsync($"{BaseUrl}/copy", new FeeCategoryCopyCommitRequest
		{
			SourceAcademicYearId = _academicYearId,
			DestinationAcademicYearId = destinationYearId,
			CategoryIds = [source.Id]
		}, JsonOptions);
		var repeatedBody = await repeated.Content.ReadFromJsonAsync<FeeCategoryCopyResult>(JsonOptions);
		repeatedBody!.Created.ShouldBe(0);
		repeatedBody.Skipped.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Require_Explicit_Copy_Conflict_Resolution_Without_Overwriting()
	{
		var source = await CreateCategoryAsync();
		var destinationYearId = await CreateAcademicYearAsync();
		var request = new FeeCategoryCopyCommitRequest
		{
			SourceAcademicYearId = _academicYearId,
			DestinationAcademicYearId = destinationYearId,
			CategoryIds = [source.Id]
		};
		(await Client.PostAsJsonAsync($"{BaseUrl}/copy", request, JsonOptions))
			.StatusCode.ShouldBe(HttpStatusCode.OK);
		var sourceUpdate = new FeeCategoryUpdateRequest
		{
			Name = source.Name,
			Description = "Source description changed",
			ConcurrencyStamp = source.ConcurrencyStamp
		};
		(await Client.PutAsJsonAsync($"{BaseUrl}/{source.Id}", sourceUpdate, JsonOptions))
			.StatusCode.ShouldBe(HttpStatusCode.OK);

		var unresolved = await Client.PostAsJsonAsync($"{BaseUrl}/copy", request, JsonOptions);
		unresolved.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		request.Resolutions =
		[
			new FeeCategoryCopyResolution
			{
				SourceCategoryId = source.Id,
				Action = "Keep"
			}
		];
		var kept = await Client.PostAsJsonAsync($"{BaseUrl}/copy", request, JsonOptions);
		kept.StatusCode.ShouldBe(HttpStatusCode.OK, await kept.Content.ReadAsStringAsync());
		var keptResult = await kept.Content.ReadFromJsonAsync<FeeCategoryCopyResult>(JsonOptions);
		keptResult!.Created.ShouldBe(0);
		keptResult.Skipped.ShouldBe(1);

		var copiedName = $"Resolved {Guid.NewGuid():N}";
		request.Resolutions[0].Action = "Copy";
		request.Resolutions[0].NewName = copiedName;
		var renamed = await Client.PostAsJsonAsync($"{BaseUrl}/copy", request, JsonOptions);
		renamed.StatusCode.ShouldBe(HttpStatusCode.OK, await renamed.Content.ReadAsStringAsync());
		var renamedResult = await renamed.Content.ReadFromJsonAsync<FeeCategoryCopyResult>(JsonOptions);
		renamedResult!.Created.ShouldBe(1);
		renamedResult.Categories[0].Name.ShouldBe(copiedName);
	}

	[Theory]
	[InlineData(0, 10)]
	[InlineData(1, 0)]
	[InlineData(1, 101)]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Reject_Invalid_Pagination(int page, int pageSize)
	{
		var response = await Client.GetAsync($"{BaseUrl}?academicYearId={_academicYearId}&page={page}&pageSize={pageSize}");
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Enforce_View_Add_And_Delete_Permission_Boundaries()
	{
		using var viewClient = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.View);
		using var addClient = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Add);
		using var deleteClient = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.Delete);
		var input = BuildCreateRequest();

		(await viewClient.GetAsync($"{BaseUrl}?academicYearId={_academicYearId}"))
			.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await viewClient.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", input, JsonOptions))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var createResponse = await addClient.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", input, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var created = await createResponse.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions);
		var destinationYearId = await CreateAcademicYearAsync();
		(await viewClient.PostAsJsonAsync($"{BaseUrl}/copy/preview", new FeeCategoryCopyPreviewRequest
		{
			SourceAcademicYearId = _academicYearId,
			DestinationAcademicYearId = destinationYearId,
			CategoryIds = [created!.Id]
		}, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await addClient.DeleteAsync($"{BaseUrl}/{created!.Id}"))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await deleteClient.DeleteAsync($"{BaseUrl}/{created.Id}"))
			.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

    private async Task<FeeCategoryResponse> CreateCategoryAsync(string? name = null)
    {
        var input = BuildCreateRequest(name);
        var response = await Client.PostAsJsonAsync($"{BaseUrl}?academicYearId={_academicYearId}", input, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<FeeCategoryResponse>(JsonOptions))!;
    }

	private async Task<Guid> MarkHeadUsedAsync(Guid feeHeadId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var structureRepository = scope.ServiceProvider.GetRequiredService<IRepository<FeeStructure, Guid>>();
		var metadataRepository = scope.ServiceProvider.GetRequiredService<IRepository<FeeStructureMetadata, Guid>>();
		var standardRepository = scope.ServiceProvider.GetRequiredService<IRepository<Standard, Guid>>();
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var metadataId = await (await metadataRepository.GetQueryableAsync()).Select(item => item.Id).FirstAsync();
			var standardId = await (await standardRepository.GetQueryableAsync()).Select(item => item.Id).FirstAsync();
			var structure = new FeeStructure(Guid.NewGuid(), currentTenant.Id, metadataId, standardId, $"Usage-{Guid.NewGuid():N}");
			var detail = new FeeStructureDetail(Guid.NewGuid(), currentTenant.Id, structure.Id, feeHeadId, 100, false, false, null, null);
			structure.AddDetail(detail);
			await structureRepository.InsertAsync(structure);
			await uow.CompleteAsync();
			return detail.Id;
		}
	}

	private async Task<Guid> CreateAcademicYearAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>();
		var manager = scope.ServiceProvider.GetRequiredService<AcademicYearManager>();
		var year = 2400 + Interlocked.Increment(ref _academicYearSequence);
		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYear = await manager.CreateAsync($"Fee copy {year}-{Guid.NewGuid():N}", new DateTime(year, 4, 1), new DateTime(year + 1, 3, 31));
			await repository.InsertAsync(academicYear);
			await uow.CompleteAsync();
			return academicYear.Id;
		}
	}

	private System.Net.Http.HttpClient CreateClientWithPermissions(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",", permissions));
		return client;
	}
}
