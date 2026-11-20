using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.FeeRefundTests;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.FeeAdjustment;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class FeeAdjustmentApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/fee-adjustments";

    public FeeAdjustmentApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Get_Eligibility_With_Empty_Student_And_Year_Should_Be_Forbidden()
    {
        var response = await Client.GetAsync(
            $"{BaseUrl}/eligibility?studentId={Guid.Empty}&academicYearId={Guid.Empty}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}

[Collection(FinanceRoutingApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class FeeAdjustmentAdministratorCreationApi_Tests(FinanceRoutingApiTestFactory factory)
    : ShikshaMitraApiTestBase(factory)
{
    private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
    private const string Add = ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add;
    private const string View = ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View;
    private const string Approve = ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Approve;
    private string CreateUrl => $"/api/fee-adjustments?academicYearId={Factory.SeededAcademicYearId}";

    [Theory]
    [InlineData(true, true, HttpStatusCode.Created, FeeAdjustmentStatus.Applied)]
    [InlineData(false, true, HttpStatusCode.Created, FeeAdjustmentStatus.Pending)]
    [InlineData(true, false, HttpStatusCode.Forbidden, null)]
    public async Task Only_configured_administrators_with_approval_permission_apply_at_creation(
        bool administrator, bool canApprove, HttpStatusCode expectedResponse, FeeAdjustmentStatus? expectedStatus)
    {
        var (actor, student, source, destination, laterDestination) = await SeedAsync(administrator);
        using var client = CreateClientAs(actor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "admin");
        client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
            canApprove ? $"{Add},{View},{Approve}" : $"{Add},{View}");
        var input = new CreateFeeAdjustmentDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"), StudentId = student,
            Reason = "Move the unpaid fee balance",
            SourceLines = [new() { DueFeeId = source, Amount = 125.75 }],
        };

        using var response = await client.PostAsJsonAsync(CreateUrl, input, JsonOptions);
        response.StatusCode.ShouldBe(expectedResponse, await response.Content.ReadAsStringAsync());
        Guid? requestId = null;
        if (expectedStatus.HasValue)
        {
            var detail = (await response.Content.ReadFromJsonAsync<FeeAdjustmentDetailDto>(JsonOptions))!;
            requestId = detail.Id;
            detail.Status.ShouldBe(expectedStatus.Value);
            detail.RequestedByUserId.ShouldBe(actor);
            if (expectedStatus == FeeAdjustmentStatus.Applied)
            {
                detail.ProcessedByUserId.ShouldBe(actor);
                detail.AppliedAmount.ShouldBe(125.75);
                detail.RemainingAmount.ShouldBe(0);
                detail.TargetAllocations.Single().DueFeeId.ShouldBe(destination);
                using var replay = await client.PostAsJsonAsync(CreateUrl, input, JsonOptions);
                replay.StatusCode.ShouldBe(HttpStatusCode.Created);
                (await replay.Content.ReadFromJsonAsync<FeeAdjustmentDetailDto>(JsonOptions))!.Id.ShouldBe(detail.Id);
            }
        }

        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ICurrentTenant>().Change(TenantId);
        using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        var dues = services.GetRequiredService<IRepository<DueFee, Guid>>();
        var applied = expectedStatus == FeeAdjustmentStatus.Applied ? 125.75m : 0m;
        (await dues.GetAsync(source)).OutstandingAmount.ShouldBe(500m - applied);
        (await dues.GetAsync(destination)).OutstandingAmount.ShouldBe(10m + applied);
        (await dues.GetAsync(laterDestination)).OutstandingAmount.ShouldBe(200m);
        var events = await services.GetRequiredService<IRepository<FinanceAuditEvent, Guid>>()
            .GetListAsync(item => item.StudentId == student && item.ActorUserId == actor &&
                item.EntityType == FinanceAuditEntityTypes.FeeAdjustment);
        events.Count.ShouldBe(expectedStatus == null ? 0 : applied > 0m ? 2 : 1);
        if (applied > 0m)
            events.Single(item => item.Action == FinanceAuditActions.Approve).EntityId.ShouldBe(requestId!.Value);
        await uow.CompleteAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Administrator_paid_credit_applies_only_to_outstanding_balances(bool hasDestinations)
    {
        var (actor, student, source, destination, laterDestination) = await SeedAsync(administrator: true);
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ICurrentTenant>().Change(TenantId);
        var uowManager = services.GetRequiredService<IUnitOfWorkManager>();
        var dues = services.GetRequiredService<IDueFeeRepository>();
        using (var setup = uowManager.Begin(requiresNew: true))
        {
            const decimal payment = 750m;
            var paidDue = await dues.GetAsync(source);
            paidDue.ApplyPayment(payment);
            await dues.UpdateAsync(paidDue);
            var number = $"ADJ-TEST-{source:N}";
            var collection = new FeeRecordMetadata(Guid.NewGuid(), TenantId, student,
                Factory.SeededAcademicYearId, (double)payment, number, FeeCollectionStatus.Paid);
            await services.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(collection);
            await services.GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(
                new FeeCollectionDueFeeAllocation(Guid.NewGuid(), TenantId, collection.Id, source, (double)payment));
            await services.GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().InsertAsync(
                new FeePaymentReceipt(Guid.NewGuid())
                {
                    TenantId = TenantId, StudentId = student, AcademicYearId = Factory.SeededAcademicYearId,
                    BoardId = Factory.SeededBoardId, EducationMediumId = Factory.SeededMediumId,
                    FeeCollectionTransactionId = collection.Id, ReceiptNumber = number,
                    ReceiptKind = FeeReceiptKind.FinalFee, ReceiptScope = FeeReceiptScope.Fee,
                    VerificationStatus = FeeReceiptVerificationStatus.Verified, TotalPaidAmount = payment,
                    ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow),
                });
            if (!hasDestinations)
            {
                foreach (var due in await dues.GetListAsync(item => item.Id == destination || item.Id == laterDestination))
                {
                    due.ApplyPayment(due.OutstandingAmount);
                    await dues.UpdateAsync(due);
                }
            }
            await setup.CompleteAsync();
        }

        using var client = CreateClientAs(actor);
        using var response = await client.PostAsJsonAsync(CreateUrl, new CreateFeeAdjustmentDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"), StudentId = student,
            Reason = "Apply credit from settled payment",
            SourceLines = [new() { DueFeeId = source, Amount = 225 }],
        }, JsonOptions);

        using var verify = uowManager.Begin(requiresNew: true);
        var savedSource = await dues.GetAsync(source);
        if (hasDestinations)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            var detail = (await response.Content.ReadFromJsonAsync<FeeAdjustmentDetailDto>(JsonOptions))!;
            detail.Status.ShouldBe(FeeAdjustmentStatus.PartiallyApplied);
            detail.AppliedAmount.ShouldBe(210);
            detail.RemainingAmount.ShouldBe(15);
            detail.TargetAllocations.Single(line => line.DueFeeId == destination).Amount.ShouldBe(10);
            detail.TargetAllocations.Single(line => line.DueFeeId == laterDestination).Amount.ShouldBe(200);
            savedSource.ReservedPaidCreditAmount.ShouldBe(15m);
            savedSource.TransferredPaidCreditAmount.ShouldBe(210m);
        }
        else
        {
            response.IsSuccessStatusCode.ShouldBeFalse();
            (await response.Content.ReadAsStringAsync()).ShouldContain(FeeAdjustmentErrorCodes.NoEligibleDestination);
            savedSource.ReservedPaidCreditAmount.ShouldBe(0m);
            savedSource.TransferredPaidCreditAmount.ShouldBe(0m);
            (await services.GetRequiredService<IRepository<FeeAdjustmentRequest, Guid>>()
                .GetListAsync(item => item.StudentId == student)).ShouldBeEmpty();
        }
        await verify.CompleteAsync();
    }

    private async Task<(Guid Actor, Guid Student, Guid Source, Guid Destination, Guid LaterDestination)> SeedAsync(
        bool administrator)
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ICurrentTenant>().Change(TenantId);
        using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        var actor = Guid.NewGuid();
        var user = new IdentityUser(actor, $"adjustment-{actor:N}", $"{actor:N}@example.test", TenantId);
        if (administrator)
        {
            var roleName = services.GetRequiredService<IOptions<UserScopeOptions>>().Value.AdminRoles.First();
            var roles = services.GetRequiredService<IdentityRoleManager>();
            var role = await roles.FindByNameAsync(roleName);
            if (role == null)
            {
                role = new IdentityRole(Guid.NewGuid(), roleName, TenantId);
                (await roles.CreateAsync(role)).Succeeded.ShouldBeTrue();
            }
            user.AddRole(role.Id);
        }
        await services.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(user);
        if (!administrator)
            await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
                new UserScopeAssignment(Guid.NewGuid(), TenantId, actor, academicYearId: Factory.SeededAcademicYearId));

        var student = Student.CreateDirectRegistration(Guid.NewGuid(), TenantId, "Adjustment", null, "Student",
            Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId,
            Factory.SeededStandardId, Factory.SeededDivisionId, DateTimeOffset.UtcNow.AddYears(-8),
            GenderEnum.Male, TestDataHelper.UniquePhone());
        await services.GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student);
        var category = new FeeCategory(Guid.NewGuid(), TenantId, Factory.SeededAcademicYearId,
            $"Adjustment {student.Id:N}", null);
        await services.GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category);
        var account = new AccountHead(Guid.NewGuid(), TenantId, Factory.SeededAcademicYearId,
            $"Adjustment revenue {student.Id:N}", AccountHeadType.Income, null, null);
        await services.GetRequiredService<IRepository<AccountHead, Guid>>().InsertAsync(account);
        var income = new TransactionCategory(Guid.NewGuid(), TenantId, Factory.SeededAcademicYearId,
            TransactionType.Income, $"Adjustment income {student.Id:N}"[..35], null, [], true,
            $"ADJ-{student.Id:N}"[..16], account.Id, DateTimeOffset.UtcNow.AddDays(-1), null);
        await services.GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income);
        var head = new FeeHead(Guid.NewGuid(), TenantId, Factory.SeededAcademicYearId, category.Id,
            "Adjustment tuition", FeeHeadModule.Finance, false, null, income.Id);
        await services.GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(head);
        var source = new DueFee(Guid.NewGuid(), TenantId, student.Id, head.Id, MonthEnum.June, 2026, false, 500m,
            new DateTime(2026, 6, 10));
        var target = new DueFee(Guid.NewGuid(), TenantId, student.Id, head.Id, MonthEnum.July, 2026, false, 10m,
            new DateTime(2026, 7, 10));
        var later = new DueFee(Guid.NewGuid(), TenantId, student.Id, head.Id, MonthEnum.August, 2026, false, 200m,
            new DateTime(2026, 8, 10));
        await services.GetRequiredService<IRepository<DueFee, Guid>>().InsertManyAsync([source, target, later]);
        await uow.CompleteAsync();
        return (actor, student.Id, source.Id, target.Id, later.Id);
    }
}
