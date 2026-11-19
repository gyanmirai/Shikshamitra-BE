using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.Finance.FeeRefunds;

/// <summary>Two real HTTP requests on separate PostgreSQL connections, using the production source and collector.</summary>
[Collection(FeeRefundApiTestCollection.Name)]
public class AdmissionFinanceCollectionPostgres_Tests(FeeRefundApiTestFactory factory, ITestOutputHelper output)
    : ShikshaMitraApiTestBase(factory)
{
    private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
    private static readonly Guid ActorId = Guid.Parse(TestAuthHandler.TestUserId);
    private static string Route(Guid applicationId) => $"/api/fee-collections/admission-applications/{applicationId}";

    [Fact]
    public async Task Concurrent_Prepare_Should_Reuse_One_Account_And_Canonical_Due_Schedule()
    {
        var applicationId = await SeedSourceAsync();
        factory.Barrier.Arm("INSERT INTO \"Finance_FeeAccounts\"");
        var requests = Enumerable.Range(0, 2).Select(_ => Client.PostAsJsonAsync(Route(applicationId) + "/prepare",
            new PrepareAdmissionFeeCollectionDto(), JsonOptions)).ToArray();
        var responses = await CompleteRaceAsync(requests);
        try
        {
            var success = responses.FirstOrDefault(r => r.IsSuccessStatusCode);
            success.ShouldNotBeNull("At least one real racing prepare must commit successfully.");
            var winner = (await success.Content.ReadFromJsonAsync<AdmissionFeeCollectionDto>(JsonOptions))!;
            var originalIds = await InTenantAsync(async services => (await services.GetRequiredService<IDueFeeRepository>()
                .GetListAsync(d => d.FinanceFeeAccountId == winner.AccountId)).Select(d => d.Id).Order().ToArray());
            foreach (var response in responses.Where(r => !r.IsSuccessStatusCode)) AssertProviderConflict(response);
            var retry = await Client.PostAsJsonAsync(Route(applicationId) + "/prepare", new PrepareAdmissionFeeCollectionDto(), JsonOptions);
            retry.StatusCode.ShouldBe(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
            var result = (await retry.Content.ReadFromJsonAsync<AdmissionFeeCollectionDto>(JsonOptions))!;
            result.AccountId.ShouldBe(winner.AccountId);
            await InTenantAsync(async services =>
            {
                (await services.GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().CountAsync(a => a.ApplicationId == applicationId)).ShouldBe(1);
                var dues = await services.GetRequiredService<IDueFeeRepository>().GetListAsync(d => d.FinanceFeeAccountId == winner.AccountId);
                dues.Select(d => d.Id).Order().ShouldBe(originalIds);
                dues.Sum(d => d.OriginalAmount).ShouldBe(1000m);
                dues.ShouldAllBe(d => d.StudentId == null && d.StudentFeeStructureId == null);
                return true;
            });
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Concurrent_Same_Key_Collection_And_Retry_Should_Return_One_Payment_And_Receipt()
    {
        var applicationId = await SeedSourceAsync();
        var prepare = await Client.PostAsJsonAsync(Route(applicationId) + "/prepare", new PrepareAdmissionFeeCollectionDto(), JsonOptions);
        prepare.StatusCode.ShouldBe(HttpStatusCode.OK, await prepare.Content.ReadAsStringAsync());
        var accountId = (await prepare.Content.ReadFromJsonAsync<AdmissionFeeCollectionDto>(JsonOptions))!.AccountId!.Value;
        var due = await InTenantAsync(async services => (await services.GetRequiredService<IDueFeeRepository>()
            .GetListAsync(d => d.FinanceFeeAccountId == accountId)).Single());
        await InTenantAsync(async services =>
        {
            var sequences = services.GetRequiredService<IReceiptNumberSequenceRepository>();
            if (!await sequences.AnyAsync(s => s.StoredTenantId == TenantId && s.Year == DateTime.UtcNow.Year && s.StoredKind == ReceiptNumberSequence.LegacyKindSentinel))
                await sequences.InsertAsync(ReceiptNumberSequence.New(Guid.NewGuid(), TenantId, DateTime.UtcNow.Year, null));
            return true;
        });
        var command = new CollectAdmissionFeeInput
        {
            IdempotencyKey = $"pg-collect-{Guid.NewGuid():N}",
            DueFeePayments = [new() { DueFeeId = due.Id, FeeHeadId = due.FeeHeadId, Amount = 300m }],
            Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 300m }]
        };
        // The real receipt counter is the first serialized write in the collector.
        factory.Barrier.Arm("UPDATE \"Finance_FinanceReceiptNumberSequences\"");
        var requests = Enumerable.Range(0, 2).Select(_ => Client.PostAsJsonAsync(Route(applicationId), command, JsonOptions)).ToArray();
        var responses = await CompleteRaceAsync(requests);
        try
        {
            var success = responses.FirstOrDefault(r => r.IsSuccessStatusCode);
            success.ShouldNotBeNull("At least one real racing collection must commit successfully.");
            var winner = JsonSerializer.Deserialize<CollectFeeOutput>(await success.Content.ReadAsStringAsync(), JsonOptions)!;
            winner.StudentId.ShouldBeNull();
            winner.FinanceFeeAccountId.ShouldBe(accountId);
            foreach (var response in responses.Where(r => !r.IsSuccessStatusCode)) AssertProviderConflict(response);
            foreach (var response in responses.Where(r => r.IsSuccessStatusCode))
                JsonSerializer.Deserialize<CollectFeeOutput>(await response.Content.ReadAsStringAsync(), JsonOptions)!.Id.ShouldBe(winner.Id);
            var retry = await Client.PostAsJsonAsync(Route(applicationId), command, JsonOptions);
            retry.StatusCode.ShouldBe(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
            var replay = (await retry.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions))!;
            replay.Id.ShouldBe(winner.Id); replay.FeeReceiptId.ShouldBe(winner.FeeReceiptId); replay.ReceiptNumber.ShouldBe(winner.ReceiptNumber);
            await InTenantAsync(async services =>
            {
                (await services.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().CountAsync(r => r.FinanceFeeAccountId == accountId)).ShouldBe(1);
                (await services.GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().CountAsync(r => r.FeeRecordMetadataId == winner.Id)).ShouldBe(1);
                (await services.GetRequiredService<IRepository<PaymentMethodRecord, Guid>>().CountAsync(r => r.FinanceFeeAccountId == accountId)).ShouldBe(1);
                (await services.GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().CountAsync(r => r.FinanceFeeAccountId == accountId)).ShouldBe(1);
                var stored = await services.GetRequiredService<IDueFeeRepository>().GetAsync(due.Id);
                stored.PaidAmount.ShouldBe(300m); stored.OutstandingAmount.ShouldBe(700m);
                return true;
            });
            command.DueFeePayments[0].Amount = 301m; command.Payments[0].Amount = 301m;
            var changed = await Client.PostAsJsonAsync(Route(applicationId), command, JsonOptions);
            changed.StatusCode.ShouldBe(HttpStatusCode.Conflict, await changed.Content.ReadAsStringAsync());
            var changedBody = await changed.Content.ReadAsStringAsync();
            changedBody.ShouldContain("retry key");
            using (var conflict = JsonDocument.Parse(changedBody))
                conflict.RootElement.GetProperty("errorCode").GetString().ShouldBe(FeeCollectionErrorCodes.IdempotencyConflict);
            var balance = await Client.GetFromJsonAsync<AdmissionFeeCollectionDto>(Route(applicationId) + "/summary", JsonOptions);
            balance!.ConfirmedPaidAmount.ShouldBe(300m); balance.OutstandingAmount.ShouldBe(700m);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    private async Task<HttpResponseMessage[]> CompleteRaceAsync(Task<HttpResponseMessage>[] requests)
    {
        TimeoutException? barrierFailure = null;
        try { await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30)); }
        catch (TimeoutException cause) { barrierFailure = cause; }
        finally { factory.Barrier.Release(); }
        var responses = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(90));
        foreach (var response in responses)
            output.WriteLine($"Race HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        output.WriteLine($"Observed PostgreSQL states: {string.Join(",", factory.Barrier.ProviderFailures.Keys.Order())}");
        if (barrierFailure != null)
            throw new TimeoutException("Both independent requests must reach real PostgreSQL writes; see the recorded HTTP responses.", barrierFailure);
        factory.Barrier.Connections.Count.ShouldBe(2);
        factory.Barrier.ConnectionIsolations.Values.ShouldAllBe(level => level == IsolationLevel.Serializable);
        return responses;
    }

    private void AssertProviderConflict(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
        factory.Barrier.ProviderFailures.ShouldNotBeEmpty("A losing response must be explained by the real PostgreSQL collision, not ignored business validation.");
        factory.Barrier.ProviderFailures.Keys.ShouldAllBe(state => state == "40001" || state == "23505" || state == "40P01");
    }

    private async Task<T> InTenantAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var serviceScope = Factory.Services.CreateScope();
        using var tenant = serviceScope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
        using var unit = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        var value = await action(serviceScope.ServiceProvider); await unit.CompleteAsync(); return value;
    }

    private async Task<Guid> SeedSourceAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var createPolicy = await Client.PostAsJsonAsync("/api/finance/collection-policies", new CreateUpdateCollectionPolicyDto
        {
            AcademicYearId = factory.SeededAcademicYearId, PolicyName = $"PG counter {suffix}", PolicyCode = $"PG-{suffix}",
            Status = "Active", EnableLateFee = false, RoundingMethod = "None", AllowOnlineCollection = true
        }, JsonOptions);
        createPolicy.IsSuccessStatusCode.ShouldBeTrue(await createPolicy.Content.ReadAsStringAsync());
        var createdPolicy = (await createPolicy.Content.ReadFromJsonAsync<CollectionPolicyDto>(JsonOptions))!;
        // The create response precedes version lookup. Re-read persisted owner metadata.
        var policy = (await Client.GetFromJsonAsync<CollectionPolicyDto>($"/api/finance/collection-policies/{createdPolicy.Id}", JsonOptions))!;
        policy.IsAttachable.ShouldBeTrue(); policy.CurrentVersionId.ShouldNotBeNull();
        return await InTenantAsync(async services =>
        {
            var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(factory.SeededAcademicYearId);
            var form = new AdmissionFormDefinition(Guid.NewGuid(), TenantId, "PG collection form", $"PGF_{suffix}", AdmissionFormLayoutType.SinglePage);
            form.Publish(ActorId); await services.GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>().InsertAsync(form);
            var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), TenantId, "PG collection journey", $"PGW_{suffix}");
            workflow.Publish(ActorId); await services.GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>().InsertAsync(workflow);
            var feeStage = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.Id, "FEES", "Fee structure confirmation", AdmissionWorkflowStageType.Payment, 0, true, false, null, null, true);
            await services.GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>().InsertAsync(feeStage);
            var decisionStage = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.Id, "DECISION", "Admission decision", AdmissionWorkflowStageType.Approval, 1, true, false, null, null, false, true);
            await services.GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>().InsertAsync(decisionStage);
            await services.GetRequiredService<IRepository<AdmissionWorkflowTransitionDefinition, Guid>>().InsertAsync(
                new(Guid.NewGuid(), workflow.Id, feeStage.Id, decisionStage.Id, 1, isDefault: true));
            var cycle = new AdmissionCycle(Guid.NewGuid(), TenantId, "PG admission cycle", $"PGC_{suffix}", year.Id, year.StartDate, year.EndDate);
            cycle.Publish(ActorId, DateTime.UtcNow);
            await services.GetRequiredService<IRepository<AdmissionCycle, Guid>>().InsertAsync(cycle);
            var offering = new AdmissionOffering(Guid.NewGuid(), TenantId, cycle.Id, null, factory.SeededBoardId,
                factory.SeededMediumId, factory.SeededMediumStageId, factory.SeededStandardId, $"PGO_{suffix}", true, null, null);
            offering.ConfigureReferences(workflow.Id, null, form.Id);
            await services.GetRequiredService<IRepository<AdmissionOffering, Guid>>().InsertAsync(offering);
            var family = new AdmissionFamily(Guid.NewGuid(), TenantId, $"PG-FAM-{suffix}");
            await services.GetRequiredService<IRepository<AdmissionFamily, Guid>>().InsertAsync(family);
            var applicant = new AdmissionApplicant(Guid.NewGuid(), TenantId, family.Id, "Postgres", null, "Applicant", year.StartDate.AddYears(-10), "Male", null, Guid.NewGuid());
            await services.GetRequiredService<IRepository<AdmissionApplicant, Guid>>().InsertAsync(applicant);
            var accountHead = new AccountHead(Guid.NewGuid(), TenantId, year.Id, $"PG Revenue {suffix}", AccountHeadType.Income, null, null);
            await services.GetRequiredService<IRepository<AccountHead, Guid>>().InsertAsync(accountHead);
            var income = new TransactionCategory(Guid.NewGuid(), TenantId, year.Id, TransactionType.Income, $"PG Admission Income {suffix}", null,
                [], true, $"PG-INC-{suffix}", accountHead.Id, new DateTimeOffset(DateTime.SpecifyKind(year.StartDate, DateTimeKind.Utc)), null);
            await services.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income);
            var category = new FeeCategory(Guid.NewGuid(), TenantId, year.Id, $"PG fees {suffix}", null);
            await services.GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category);
            var head = new FeeHead(Guid.NewGuid(), TenantId, year.Id, category.Id, $"PG admission fee {suffix}", FeeHeadModule.Finance, false, null, income.Id);
            await services.GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(head);
            var term = new PaymentTerm(Guid.NewGuid(), TenantId, year.Id, factory.SeededBoardId, "PG installment", $"PGT-{suffix}", null,
                false, HolidayShiftingMethod.None, DemandGenerationRule.SpecificDate, null, InitialDemandStatus.Published);
            var termVersion = new PaymentTermVersion(Guid.NewGuid(), TenantId, term.Id, 1, PaymentTermVersionStatus.Draft,
                HolidayShiftingMethod.None, DemandGenerationRule.SpecificDate, null, null, null);
            termVersion.SetInstallments([new(Guid.NewGuid(), termVersion.Id, 1, "Admission installment", year.StartDate.Date, year.EndDate.Date,
                year.StartDate.Date, year.StartDate.Date.AddDays(10), 100m, true, false, allowPartialOverride: true)]);
            termVersion.Publish(year.StartDate, ActorId); term.AddVersion(termVersion);
            await services.GetRequiredService<IRepository<PaymentTerm, Guid>>().InsertAsync(term);
            var metadata = new FeeStructureMetadata(Guid.NewGuid(), TenantId, year.Id, factory.SeededBoardId, factory.SeededMediumId, null, $"PG metadata {suffix}");
            await services.GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(metadata);
            var structure = new FeeStructure(Guid.NewGuid(), TenantId, metadata.Id, factory.SeededStandardId, $"PG plan {suffix}");
            var detail = new FeeStructureDetail(Guid.NewGuid(), TenantId, structure.Id, head.Id, 1000m, true, false, null, null);
            detail.AttachPaymentTerm(term.Id, termVersion.Id); structure.AddDetail(detail); structure.AttachCollectionPolicy(policy.Id, policy.CurrentVersionId!.Value); structure.Lock();
            await services.GetRequiredService<IRepository<FeeStructure, Guid>>().InsertAsync(structure);
            var application = new AdmissionApplication(Guid.NewGuid(), TenantId, $"APP-PG-{suffix}", applicant.Id, null, cycle.Id, offering.Id, workflow.Id, form.Id);
            application.Submit();
            var instance = new AdmissionApplicationWorkflowInstance(Guid.NewGuid(), TenantId, application.Id, workflow.Id);
            var stage = new AdmissionApplicationStageInstance(Guid.NewGuid(), TenantId, instance.Id, feeStage.Id, feeStage.Code, feeStage.Name, feeStage.StageType, 0, true);
            application.SetCurrentStage(stage.Id);
            application.SaveFeeSelection(new(structure.Id, structure.Name, structure.RevisionNumber, year.Id, factory.SeededStandardId, stage.Id, "INR",
                [new(head.Id, head.Name, 1000m, false, true, term.Id, termVersion.Id)], ActorId, DateTimeOffset.UtcNow), false);
            await services.GetRequiredService<IRepository<AdmissionApplication, Guid>>().InsertAsync(application);
            await services.GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>().InsertAsync(instance);
            await services.GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().InsertAsync(stage);
            await services.GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().InsertAsync(
                new(Guid.NewGuid(), TenantId, instance.Id, decisionStage.Id, decisionStage.Code, decisionStage.Name, decisionStage.StageType, 1, false));
            return application.Id;
        });
    }
}
