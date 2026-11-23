using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
    private IDisposable Reviewer() => GetRequiredService<ICurrentPrincipalAccessor>().Change(
        new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, TestUserId.ToString())], "Test")));

    private static StandardFeeStructureRequest AsRequest(StandardFeeStructureResponseItem response)
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(response))!;
        foreach (var line in json["FeeHeads"]!.AsArray())
        {
            line!["PayableMonth"] ??= System.Text.Json.Nodes.JsonValue.Create(0);
            line["PayableYear"] ??= System.Text.Json.Nodes.JsonValue.Create(0);
        }
        return json.Deserialize<StandardFeeStructureRequest>()!;
    }

    private async Task<(Guid MetadataId, Guid StandardId, StandardFeeStructureRequest Candidate)> ReadyCandidateAsync(bool monthlyTerm = false)
    {
        var (yearId, classId, metadataId, headId) = await SeedStandardAsync();
        var metadata = await _metadataRepo.GetAsync(metadataId);
        var standard = await _standardRepo.GetAsync(classId);
        var term = new PaymentTerm(Guid.NewGuid(), TestTenantId, yearId, metadata.AcademicBoardId!.Value,
            "Annual", "ANNUAL", null, false, HolidayShiftingMethod.None, DemandGenerationRule.OnDueDate, null, InitialDemandStatus.Draft);
        var version = new PaymentTermVersion(Guid.NewGuid(), TestTenantId, term.Id, 1, PaymentTermVersionStatus.Draft,
            HolidayShiftingMethod.None, DemandGenerationRule.OnDueDate, null, null, null);
        version.SetInstallments(monthlyTerm ? Enumerable.Range(0, 12).Select(index =>
        {
            var start = new DateTime(2025, 4, 1).AddMonths(index);
            var end = start.AddMonths(1).AddDays(-1);
            return new PaymentTermInstallment(Guid.NewGuid(), version.Id, index + 1, start.ToString("MMM yyyy"),
                start, end, end, end, index == 11 ? 8.37m : 8.33m, true, false);
        }).ToList() : [
            new PaymentTermInstallment(Guid.NewGuid(), version.Id, 1, "First half", new(2025, 4, 1), new(2025, 9, 30), new(2025, 4, 10), new(2025, 4, 10), 50m, true, false),
            new PaymentTermInstallment(Guid.NewGuid(), version.Id, 2, "Second half", new(2025, 10, 1), new(2026, 3, 31), new(2025, 10, 10), new(2025, 10, 10), 50m, true, false)
        ]);
        version.Publish(new DateTime(2025, 4, 1), TestUserId);
        term.AddVersion(version);
        await GetRequiredService<IRepository<PaymentTerm, Guid>>().InsertAsync(term, autoSave: true);
        var book = new ReceiptBook(Guid.NewGuid(), TestTenantId, yearId, metadata.AcademicBoardId.Value,
            metadata.AcademicMediumId!.Value, standard.EducationMediumStageId, "School Fees", "FEES", "", 1, 99999, "");
        await GetRequiredService<IRepository<ReceiptBook, Guid>>().InsertAsync(book, autoSave: true);
        return (metadataId, classId, new StandardFeeStructureRequest
        {
            Name = "Annual fee structure", ReceiptBookId = book.Id,
            CollectionPolicyId = await CreateCollectionPolicyAsync(yearId, headId, "Collection"),
            ConcessionPolicyId = await CreateConcessionPolicyAsync(yearId, "Concession"),
            FeeHeads = [new FeeHeadAssignmentRequest { FeeHeadId = headId, Amount = 1000.01m, PaymentTermId = term.Id, PaymentTermVersionId = version.Id }]
        });
    }

    [Fact]
    public async Task Publish_Without_Concession_Policy_Does_Not_Select_A_Default()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        candidate.ConcessionPolicyId = null;
        candidate.ConcessionPolicyVersionId = null;
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var published = await _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp });
        published.ConcessionPolicyId.ShouldBeNull();
        published.ConcessionPolicyVersionId.ShouldBeNull();
        published.PublicationReview!.IsReady.ShouldBeTrue();
        published.PublicationReview.PolicyLabels.ShouldNotContainKey("concession");
        published.PublicationReview.Schedules.Single().Installments.Sum(x => x.CalculatedAmount).ShouldBe(1000.01m);
    }

    [Fact]
    public async Task Separate_Revision_Actions_Create_New_Drafts_After_The_First_Is_Renamed_And_Saved()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var root = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var published = await _feeStructures.PublishAsync(root.Id, new() { ConcurrencyStamp = root.ConcurrencyStamp });
        var first = await _feeStructures.CreateRevisionAsync(published.Id, new() { ConcurrencyStamp = published.ConcurrencyStamp });
        var edit = AsRequest(first);
        edit.Name = "Renamed saved revision";
        edit.FeeHeads[0].Amount = 2400;
        first = await _feeStructures.UpdateAsync(first.Id, edit);

        var second = await _feeStructures.CreateRevisionAsync(published.Id, new() { ConcurrencyStamp = published.ConcurrencyStamp });
        second.Id.ShouldNotBe(first.Id);
        second.PreviousRevisionId.ShouldBe(published.Id);
        second.RevisionRootId.ShouldBe(published.RevisionRootId);
        second.RevisionNumber.ShouldBe(first.RevisionNumber + 1);
        second.FeeHeads.Single().Amount.ShouldBe(published.FeeHeads.Single().Amount);
        second.FeeHeads.Single().Id.ShouldNotBe(first.FeeHeads.Single().Id);
        var rows = (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures;
        rows.Count.ShouldBe(3);
        rows.Single(row => row.Id == first.Id).Name.ShouldBe("Renamed saved revision");
        rows.Single(row => row.Id == first.Id).FeeHeads.Single().Amount.ShouldBe(2400);
        rows.Single(row => row.Id == published.Id).ConcurrencyStamp.ShouldBe(published.ConcurrencyStamp);
    }

    private static FeeStructureRevisionRequest RevisionCommand(Guid requestId, string stamp) =>
        JsonSerializer.Deserialize<FeeStructureRevisionRequest>(JsonSerializer.Serialize(new
        {
            RequestId = requestId, ConcurrencyStamp = stamp
        }))!;

    [Fact]
    public async Task Revision_Request_Replay_Returns_Its_Canonical_Draft_And_New_Keys_Create_Separate_Drafts()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var root = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var published = await _feeStructures.PublishAsync(root.Id, new() { ConcurrencyStamp = root.ConcurrencyStamp });
        var key = Guid.NewGuid();
        var first = await _feeStructures.CreateRevisionAsync(published.Id, RevisionCommand(key, published.ConcurrencyStamp));
        first.Id.ShouldBe(key);
        var edit = AsRequest(first); edit.Name = "Saved first revision"; edit.FeeHeads[0].Amount = 2600;
        first = await _feeStructures.UpdateAsync(first.Id, edit);
        var second = await _feeStructures.CreateRevisionAsync(published.Id, RevisionCommand(Guid.NewGuid(), published.ConcurrencyStamp));
        second.Id.ShouldNotBe(first.Id);
        second.RevisionNumber.ShouldBe(3);
        var replay = await _feeStructures.CreateRevisionAsync(published.Id, RevisionCommand(key, published.ConcurrencyStamp));
        replay.Id.ShouldBe(first.Id);
        replay.Name.ShouldBe("Saved first revision");
        replay.FeeHeads.Single().Amount.ShouldBe(2600);
        (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures.Count.ShouldBe(3);
        var publishedRevision = await _feeStructures.PublishAsync(first.Id, new() { ConcurrencyStamp = first.ConcurrencyStamp });
        replay = await _feeStructures.CreateRevisionAsync(published.Id, RevisionCommand(key, published.ConcurrencyStamp));
        replay.Status.ShouldBe(FeeStructureStatus.Locked);
        replay.ConcurrencyStamp.ShouldBe(publishedRevision.ConcurrencyStamp);
        (await Should.ThrowAsync<BusinessException>(() => _feeStructures.CreateRevisionAsync(published.Id,
            RevisionCommand(Guid.NewGuid(), published.ConcurrencyStamp)))).Code.ShouldBe(FeeStructureErrorCodes.ConcurrencyConflict);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("changed-body")]
    [InlineData("different-source")]
    [InlineData("deleted")]
    public async Task Revision_Request_Keys_Cannot_Be_Empty_Or_Reused_For_Another_Command(string scenario)
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var root = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var published = await _feeStructures.PublishAsync(root.Id, new() { ConcurrencyStamp = root.ConcurrencyStamp });
        var key = Guid.NewGuid();
        if (scenario == "empty")
        {
            (await Should.ThrowAsync<BusinessException>(() => _feeStructures.CreateRevisionAsync(published.Id,
                RevisionCommand(Guid.Empty, published.ConcurrencyStamp)))).Code.ShouldBe(FeeStructureErrorCodes.InvalidConfiguration);
            return;
        }
        var revision = await _feeStructures.CreateRevisionAsync(published.Id, RevisionCommand(key, published.ConcurrencyStamp));
        if (scenario == "deleted") await _feeStructures.DeleteAsync(revision.Id);
        if (scenario == "different-source")
        {
            candidate.Name = "Another published root";
            root = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
            published = await _feeStructures.PublishAsync(root.Id, new() { ConcurrencyStamp = root.ConcurrencyStamp });
        }
        var command = RevisionCommand(key, scenario == "changed-body" ? "changed-stamp" : published.ConcurrencyStamp);
        (await Should.ThrowAsync<BusinessException>(() => _feeStructures.CreateRevisionAsync(published.Id, command)))
            .Code.ShouldBe(FeeStructureErrorCodes.ConcurrencyConflict);
    }

    [Fact]
    public async Task Publish_Freezes_Review_Rejects_Edits_And_Creates_Independent_Revisions()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var review = await _feeStructures.PreviewAsync(new() { FeeStructureMetadataId = metadata, StandardId = standard, StructureId = draft.Id, StandardFeeStructures = AsRequest(draft) });
        review.Errors.ShouldBeEmpty();
        review.Schedules.Single().Installments.Sum(x => x.CalculatedAmount).ShouldBe(draft.FeeHeads.Single().Amount);
        var published = await _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp });
        published.PublicationReview.ShouldNotBeNull();
        published.PublicationReview!.PolicyLabels["collection"].ShouldContain("v1");
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.UpdateAsync(published.Id, AsRequest(published)));
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.DeleteAsync(published.Id));
        var revisionCommand = RevisionCommand(Guid.NewGuid(), published.ConcurrencyStamp);
        var revision = await _feeStructures.CreateRevisionAsync(published.Id, revisionCommand);
        revision.RevisionNumber.ShouldBe(2);
        revision.RevisionRootId.ShouldBe(published.Id);
        revision.PreviousRevisionId.ShouldBe(published.Id);
        revision.PublicationReview.ShouldBeNull();
        revision.FeeHeads.Single().Id.ShouldNotBe(published.FeeHeads.Single().Id);
        revision.CollectionPolicyVersionId.ShouldBe(published.CollectionPolicyVersionId);
        var retried = await _feeStructures.CreateRevisionAsync(published.Id, revisionCommand);
        retried.Id.ShouldBe(revision.Id);
        await _feeStructures.DeleteAsync(revision.Id);
        var next = await _feeStructures.CreateRevisionAsync(published.Id, new() { ConcurrencyStamp = published.ConcurrencyStamp });
        next.RevisionNumber.ShouldBe(3); // Deleted revision numbers remain reserved.
        var persisted = (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures.Single(x => x.Id == published.Id);
        persisted.PublicationReview!.Schedules.Single().AnnualAmount.ShouldBe(draft.FeeHeads.Single().Amount);
    }

    [Fact]
    public async Task Incomplete_Draft_Is_Savable_But_Cannot_Be_Published_Or_Legacy_Locked()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (_, standard, metadata, _) = await SeedStandardAsync();
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = new() { Name = "Work in progress", FeeHeads = [] } });
        var review = await _feeStructures.PreviewAsync(new() { FeeStructureMetadataId = metadata, StandardId = standard, StructureId = draft.Id, StandardFeeStructures = AsRequest(draft) });
        review.IsReady.ShouldBeFalse();
        review.Errors.ShouldNotContain(x => x.Contains("Receipt Book"));
        review.Errors.ShouldContain(x => x.Contains("Fee Head"));
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp }));
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.LockAsync(draft.Id));
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.CreateRevisionAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp }));
    }

    [Fact]
    public async Task Stale_Update_Is_Rejected_Without_Overwriting_The_Saved_Draft()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (_, standard, metadata, head) = await SeedStandardAsync();
        var draft = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = new() { Name = "Original", FeeHeads = BuildFeeHeads(head) } });
        var first = AsRequest(draft); first.Name = "First editor";
        var saved = await _feeStructures.UpdateAsync(draft.Id, first);
        saved.ConcurrencyStamp.ShouldNotBe(draft.ConcurrencyStamp);
        var stale = AsRequest(draft); stale.Name = "Stale editor";
        var error = await Should.ThrowAsync<BusinessException>(() => _feeStructures.UpdateAsync(draft.Id, stale));
        error.Code.ShouldBe(FeeStructureErrorCodes.ConcurrencyConflict);
        (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures.Single().Name.ShouldBe("First editor");
    }

    [Fact]
    public async Task Create_Retry_With_Same_Request_Returns_One_Draft_And_Rejects_Key_Reuse()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (_, standard, metadata, head) = await SeedStandardAsync();
        var request = new StandardFeeStructureSaveRequest { RequestId = Guid.NewGuid(), StandardId = standard, StandardFeeStructures = new() { Name = "Retry-safe draft", FeeHeads = BuildFeeHeads(head) } };
        var first = await _feeStructures.CreateAsync(metadata, request);
        (await _feeStructures.CreateAsync(metadata, request)).Id.ShouldBe(first.Id);
        (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures.Count.ShouldBe(1);
        request.StandardFeeStructures.Name = "Different content";
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.CreateAsync(metadata, request));
    }

    [Fact]
    public async Task Customized_Schedule_RoundTrips_And_Derived_Dates_Are_Recalculated()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var settings = await _feeStructures.GetSettingsAsync(metadata, standard);
        var source = settings.Terms.Single().Installments.First();
        candidate.FeeHeads[0].ScheduleMode = ScheduleMode.Customized;
        candidate.FeeHeads[0].InstallmentOverrides = [new ScheduleOverrideDto { SourcePaymentTermInstallmentId = source.SourcePaymentTermInstallmentId, Sequence = source.Sequence, Name = source.Name, DueDate = new(2025, 4, 15) }];
        var created = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        created.FeeHeads.Single().InstallmentOverrides.Single().DueDate.ShouldBe(new DateTime(2025, 4, 15));
        var updated = await _feeStructures.UpdateAsync(created.Id, AsRequest(created));
        updated.FeeHeads.Single().Id.ShouldBe(created.FeeHeads.Single().Id);
        updated.FeeHeads.Single().InstallmentOverrides.Single().DueDate.ShouldBe(new DateTime(2025, 4, 15));
        var review = await _feeStructures.PreviewAsync(new() { FeeStructureMetadataId = metadata, StandardId = standard, StructureId = updated.Id, StandardFeeStructures = AsRequest(updated) });
        review.Errors.ShouldBeEmpty();
        review.Schedules.Single().Installments.First().DemandDate.ShouldBe(new DateTime(2025, 4, 15));
    }

    [Theory]
    [InlineData("foreign-installment")]
    [InlineData("allocation-total")]
    [InlineData("overlap")]
    [InlineData("term-version")]
    [InlineData("duplicate-head")]
    public async Task Invalid_Configuration_Is_Rejected_Before_Persistence(string problem)
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var source = (await _feeStructures.GetSettingsAsync(metadata, standard)).Terms.Single().Installments.First();
        var line = candidate.FeeHeads[0];
        if (problem == "term-version") line.PaymentTermVersionId = Guid.NewGuid();
        else if (problem == "duplicate-head") candidate.FeeHeads.Add(line);
        else
        {
            line.ScheduleMode = ScheduleMode.Customized;
            line.InstallmentOverrides = [new ScheduleOverrideDto { SourcePaymentTermInstallmentId = problem == "foreign-installment" ? Guid.NewGuid() : source.SourcePaymentTermInstallmentId,
                Sequence = source.Sequence, Name = source.Name, AllocationPercentage = problem == "allocation-total" ? 80m : null,
                CoverageEndDate = problem == "overlap" ? new DateTime(2025, 10, 15) : null }];
        }
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate }));
        (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unchanged_Policy_Pins_Are_Not_Upgraded_By_An_Unrelated_Edit()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var created = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        var versions = GetRequiredService<IRepository<FinancePolicyVersion, Guid>>();
        var old = await versions.GetAsync(created.CollectionPolicyVersionId!.Value);
        await versions.InsertAsync(new FinancePolicyVersion(Guid.NewGuid(), TestTenantId, "collection", old.PolicyId, old.AcademicYearId, old.VersionNumber + 1, old.PolicyName, "Active", old.SnapshotJson, "updated"), autoSave: true);
        var edit = AsRequest(created); edit.Name = "Unrelated name edit"; edit.CollectionPolicyVersionId = null;
        var updated = await _feeStructures.UpdateAsync(created.Id, edit);
        updated.CollectionPolicyVersionId.ShouldBe(old.Id);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("invalid JSON")]
    public async Task Corrupt_Policy_Versions_Are_Rejected(string snapshot)
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var versions = GetRequiredService<IRepository<FinancePolicyVersion, Guid>>();
        var current = (await versions.GetListAsync(x => x.PolicyId == candidate.CollectionPolicyId)).Single();
        var corrupt = new FinancePolicyVersion(Guid.NewGuid(), TestTenantId, "collection", current.PolicyId, current.AcademicYearId, 2, current.PolicyName, "Active", snapshot, "updated");
        await versions.InsertAsync(corrupt, autoSave: true);
        candidate.CollectionPolicyVersionId = corrupt.Id;
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate }));
    }

    [Fact]
    public async Task User_Without_Class_Scope_Cannot_Read_Settings_Or_Structure()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (_, standard, metadata, _) = await SeedStandardAsync();
        var other = Guid.NewGuid();
        await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(other, "outside", "outside@example.test", TestTenantId), autoSave: true);
        using var outsider = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, other.ToString())], "Test")));
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.GetSettingsAsync(metadata, standard));
        await Should.ThrowAsync<BusinessException>(() => _feeStructures.GetByStandardAsync(standard));
        var list = await GetRequiredService<IFeeStructureMetadataAppService>().GetListAsync((await _metadataRepo.GetAsync(metadata)).AcademicYearId!.Value);
        list.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Refund_RoundTrip_And_Publish_Ignore_Independent_Proration_Rules()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadata, standard, candidate) = await ReadyCandidateAsync();
        var year = (await _metadataRepo.GetAsync(metadata)).AcademicYearId!.Value;
        candidate.FeeHeads.Single().Amount = 1000m;
        var head = candidate.FeeHeads.Single().FeeHeadId;
        var refundVersions = GetRequiredService<IRefundPolicyAppService>();
        var refund = await refundVersions.CreateAsync(new CreateUpdateRefundPolicyDto
        {
            AcademicYearId = year, PolicyName = "Refund rules", Status = "Active", FeeHeadScope = "SelectedFeeHeads", ApplicableFeeHeadIds = [head],
            ExpenseCategoryId = await CreateRefundExpenseCategoryAsync(year), AllowedPayoutMethods = ["Cash"]
        });
        var savedRefund = await refundVersions.GetAsync(refund.Id);
        var refundOption = (await _feeStructures.GetCatalogueAsync(metadata, standard)).Policies
            .Single(x => x.Id == refund.Id && x.Family == "refund");
        refundOption.VersionId.ShouldBe(savedRefund.CurrentVersionId!.Value);
        refundOption.FeeHeadScope.ShouldBe("SelectedFeeHeads");
        refundOption.ApplicableFeeHeadIds!.ShouldBe(new[] { head });
        refundOption.FeeHeadNames![head].ShouldBe(savedRefund.FeeHeadNames[head]);
        (await refundVersions.GetVersionsAsync(refund.Id, new() { VersionId = refundOption.VersionId }))
            .Items.Single().Id.ShouldBe(refundOption.VersionId);
        (await refundVersions.GetVersionsAsync(refund.Id, new() { VersionId = Guid.NewGuid() }))
            .Items.ShouldBeEmpty();
        var proration = await GetRequiredService<IProrationPolicyAppService>().CreateAsync(new CreateUpdateProrationPolicyDto
        {
            AcademicYearId = year, PolicyName = "Daily proration", Status = "Active", FeeHeadRules = [new ProrationPolicyRuleDto { FeeHeadId = head, RuleType = "Daily" }]
        });
        candidate.RefundPolicyId = refund.Id;
        var saved = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
        saved.RefundPolicyVersionId.ShouldNotBeNull();
        (await _feeStructures.GetSettingsAsync(metadata, standard)).Policies.ShouldNotContain(x => x.Family == "proration");
        var review = await _feeStructures.PreviewAsync(new() { FeeStructureMetadataId = metadata, StandardId = standard, StructureId = saved.Id, StandardFeeStructures = AsRequest(saved) });
        review.Errors.ShouldBeEmpty();
        review.PolicyLabels.ShouldNotContainKey("proration");
        review.Warnings.ShouldNotContain(x => x.Contains("proration", StringComparison.OrdinalIgnoreCase));
        review.Coverage.Single().Refund.ShouldBe("Policy eligible");
        var edit = AsRequest(saved); edit.FeeHeads[0].IsOneTime = true;
        var oneTime = await _feeStructures.UpdateAsync(saved.Id, edit);
        var published = await _feeStructures.PublishAsync(oneTime.Id, new() { ConcurrencyStamp = oneTime.ConcurrencyStamp });
        published.PublicationReview!.IsReady.ShouldBeTrue();
        saved = await _feeStructures.CreateRevisionAsync(published.Id, new() { ConcurrencyStamp = published.ConcurrencyStamp });
        edit = AsRequest(saved);
        edit.Name = "Revised policy fees";
        edit.FeeHeads[0].IsOneTime = false; edit.RefundPolicyId = null; edit.RefundPolicyVersionId = null;
        var detached = await _feeStructures.UpdateAsync(saved.Id, edit);
        detached.RefundPolicyVersionId.ShouldBeNull();
    }

    [Fact]
    public async Task Academic_Workspace_Projection_Returns_Classes_In_Academic_Order()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (year, standardId, metadata, _) = await SeedStandardAsync();
        var stageId = (await _standardRepo.GetAsync(standardId)).EducationMediumStageId;
        var names = new[]
        {
            "LKG", "Class 12", "Class 1", "Class 10", "Class 6", "Class 9", "Class 7",
            "Class 4", "UKG", "Class 11", "Nursery", "Class 2", "Class 5", "Class 3", "Class 8"
        };
        foreach (var name in names)
            await _standardRepo.InsertAsync(new Standard(Guid.NewGuid(), TestTenantId, name, 6, stageId, null), autoSave: true);

        var result = await GetRequiredService<IFeeStructureMetadataAppService>().GetListAsync(year);
        result.Items.Single(item => item.Id == metadata).Classes.Select(item => item.Name)
            .Where(names.Contains)
            .ShouldBe(new[]
            {
                "Nursery", "LKG", "UKG", "Class 1", "Class 2", "Class 3", "Class 4", "Class 5",
                "Class 6", "Class 7", "Class 8", "Class 9", "Class 10", "Class 11", "Class 12"
            });
    }

    [Fact]
    public async Task Academic_Workspace_Projection_Returns_Classes_And_Configured_Counts()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (year, standard, metadata, head) = await SeedStandardAsync();
        var service = GetRequiredService<IFeeStructureMetadataAppService>();
        await service.GenerateFeeStructuresAsync(year);
        var before = await service.GetListAsync(year);
        before.Items.Count.ShouldBe(1);
        before.Items[0].Id.ShouldBe(metadata);
        before.Items[0].Classes.Single().Id.ShouldBe(standard);
        before.Items[0].TotalClasses.ShouldBe(1);
        before.Items[0].ConfiguredClasses.ShouldBe(0);
        await _feeStructures.CreateAsync(metadata, new() { StandardId = standard,
            StandardFeeStructures = new() { Name = "Workspace count", FeeHeads = BuildFeeHeads(head) } });
        (await service.GetListAsync(year)).Items[0].ConfiguredClasses.ShouldBe(1);
    }

    [Fact]
    public async Task ReceiptBook_Is_Ignored_But_Metadata_From_Another_Medium_Is_Rejected()
    {
        using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
        var (metadataId, standard, candidate) = await ReadyCandidateAsync();
        var metadata = await _metadataRepo.GetAsync(metadataId);
        var stage = new ShikshaMitra.TenantSettings.EducationBoards.EducationMediumStage(Guid.NewGuid(), TestTenantId, "Secondary", null, metadata.AcademicMediumId!.Value);
        await _stageRepo.InsertAsync(stage, autoSave: true);
        var wrongBook = new ReceiptBook(Guid.NewGuid(), TestTenantId, metadata.AcademicYearId!.Value, metadata.AcademicBoardId!.Value,
            metadata.AcademicMediumId.Value, stage.Id, "Wrong stage book", "WRONG", "", 1, 999, "");
        await GetRequiredService<IRepository<ReceiptBook, Guid>>().InsertAsync(wrongBook, autoSave: true);
        (await _feeStructures.GetSettingsAsync(metadataId, standard)).ReceiptBooks.ShouldNotContain(x => x.Id == wrongBook.Id);
        candidate.ReceiptBookId = wrongBook.Id;
        var created = await _feeStructures.CreateAsync(metadataId, new() { StandardId = standard, StandardFeeStructures = candidate });
        created.ReceiptBookId.ShouldBeNull();
        var published = await _feeStructures.PublishAsync(created.Id, new() { ConcurrencyStamp = created.ConcurrencyStamp });
        published.PublicationReview!.IsReady.ShouldBeTrue();
        var medium = new ShikshaMitra.TenantSettings.EducationMediums.EducationMedium(Guid.NewGuid(), TestTenantId, "Other medium", metadata.AcademicBoardId.Value);
        await _mediumRepo.InsertAsync(medium, autoSave: true);
        var wrongMetadata = new FeeStructureMetadata(Guid.NewGuid(), TestTenantId, metadata.AcademicYearId!.Value, metadata.AcademicBoardId!.Value, medium.Id, null, "Other workspace");
        await _metadataRepo.InsertAsync(wrongMetadata, autoSave: true);
        var error = await Should.ThrowAsync<BusinessException>(() => _feeStructures.GetSettingsAsync(wrongMetadata.Id, standard));
        error.Code.ShouldBe(FeeStructureErrorCodes.ScopeMismatch);
    }
}
