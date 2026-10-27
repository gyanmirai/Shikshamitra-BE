using System;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Mobile;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using Permissions = ShikshaMitra.Finance.Mobile.MobileFinancePermissions;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
    [FamilyFinancePostgresFact]
    public Task PostgreSQL_family_same_key_race_replays_one_pending_request_and_rolls_back_loser() => FamilyAdjustmentPostgresRace(true);

    [FamilyFinancePostgresFact]
    public Task PostgreSQL_family_different_keys_cannot_overreserve_one_due() => FamilyAdjustmentPostgresRace(false);

    private Task FamilyAdjustmentPostgresRace(bool sameKey) => WithUnitOfWorkAsync(new AbpUnitOfWorkOptions { IsTransactional = false }, async () =>
    {
        var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
        var f = await Seed(tenantId, true); using var identity = FinanceIdentity(f.User.Id);
        await GrantFinancePermissionAsync(f.User.Id, Permissions.RequestAdjustment);
        var category = await Insert(new FeeCategory(Guid.NewGuid(), tenantId, f.Year.Id, "PG family fees", null));
        var income = await Insert(new TransactionCategory(Guid.NewGuid(), tenantId, f.Year.Id, TransactionType.Income, "PG family income", null));
        var head = await Insert(new FeeHead(Guid.NewGuid(), tenantId, f.Year.Id, category.Id, "PG Tuition", null, true, null, income.Id));
        var due = await Insert(new DueFee(Guid.NewGuid(), tenantId, f.Student.Id, head.Id,
            (ShikshaMitra.Finance.Fees.Collections.MonthEnum)(DateTime.Today.Month - 1), DateTime.Today.Year, false, 500m, DateTime.Today));
        var key = Guid.NewGuid().ToString("N");
        var input = new MobileAdjustmentInput { IdempotencyKey = key, Reason = "Correct the assigned tuition charge",
            SourceLines = [new() { DueFeeId = due.Id, Amount = "400.00" }] };
        var preview = JsonSerializer.SerializeToElement(await FamilyService<FeeAdjustmentAppService>().FamilyAdjustmentPreviewAsync(f.Student.Id, f.Year.Id, input));
        input.PreviewFingerprint = preview.GetProperty("PreviewFingerprint").GetString();
        var second = JsonSerializer.Deserialize<MobileAdjustmentInput>(JsonSerializer.Serialize(input))!;
        if (!sameKey) second.IdempotencyKey = Guid.NewGuid().ToString("N");
        var barrier = GetRequiredService<FamilyFinancePostgresBarrier>(); barrier.Arm();
        async Task<(MobileFinanceCommandResult? Result, Exception? Error)> Run(MobileAdjustmentInput command)
        {
            try
            {
                // A fresh outer UOW gives access checks and the command their own connection/context per racing execution.
                using var scope = ServiceProvider.CreateScope();
                using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(isTransactional: false, requiresNew: true);
                var result = await FamilyService<FeeAdjustmentAppService>().FamilyAdjustmentCreateAsync(f.Student.Id, f.Year.Id, command);
                await unit.CompleteAsync(); return (result, null);
            }
            catch (Exception error) { return (null, error); }
        }
        var executions = new[] { Task.Run(() => Run(input)), Task.Run(() => Run(second)) };
        var completion = Task.WhenAll(executions);
        try
        {
            if (await Task.WhenAny(barrier.Reached, completion).WaitAsync(TimeSpan.FromSeconds(45)) == completion)
                throw new InvalidOperationException("Commands finished before the database race barrier:\n" +
                    string.Join("\n", (await completion).Select(x => x.Error?.ToString() ?? "Completed without reaching barrier")));
        }
        finally { barrier.Release(); }
        var outcomes = await completion.WaitAsync(TimeSpan.FromSeconds(90));
        barrier.Connections.Count.ShouldBe(2);
        barrier.Connections.Values.ShouldAllBe(level => level == IsolationLevel.Serializable);
        outcomes.Count(x => x.Result != null).ShouldBe(1, string.Join("\n", outcomes.Select(x => x.Error?.ToString())));
        barrier.Failures.ShouldNotBeEmpty();
        barrier.Failures.Keys.ShouldContain(state => state == "40001" || state == "23505" || state == "40P01");
        var winner = outcomes.Single(x => x.Result != null).Result!;
        winner.Status.ShouldBe("Pending"); winner.Amount.ShouldBe("400.00");
        if (sameKey)
        {
            var replay = await FamilyService<FeeAdjustmentAppService>().FamilyAdjustmentCreateAsync(f.Student.Id, f.Year.Id, input);
            replay.Id.ShouldBe(winner.Id);
        }
        else
        {
            var losingInput = outcomes[0].Result == null ? input : second;
            await Should.ThrowAsync<BusinessException>(() => FamilyService<FeeAdjustmentAppService>().FamilyAdjustmentCreateAsync(f.Student.Id, f.Year.Id, losingInput));
        }
        var pending = await GetRequiredService<IRepository<FeeAdjustmentRequest, Guid>>().GetListAsync(x => x.StudentId == f.Student.Id);
        pending.ShouldHaveSingleItem().RequestedAmount.ShouldBe(400d);
        (await GetRequiredService<IRepository<FinanceAuditEvent, Guid>>().CountAsync(x => x.StudentId == f.Student.Id && x.EntityType == "family_command_receipt")).ShouldBe(1);
        (await GetRequiredService<IRepository<DueFee, Guid>>().GetAsync(due.Id)).OutstandingAmount.ShouldBe(500m);
    });
}
