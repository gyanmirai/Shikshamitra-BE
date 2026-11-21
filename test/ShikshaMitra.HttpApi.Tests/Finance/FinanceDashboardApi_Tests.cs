using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Dashboard;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance;

[Collection(FinanceRoutingApiTestCollection.Name)]
public class FinanceDashboardApi_Tests(FinanceRoutingApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
    private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
    private const string Overview = "/api/finance/dashboard/overview?academicYearId=";
    private const string Activity = "/api/finance/dashboard/activity?academicYearId=";

    [Fact]
    public async Task Dashboard_uses_real_fee_balances_and_receipts_without_crossing_scope()
    {
        await EnsureDefaultTestUserHasTenantWideScopeAsync();
        var (yearId, visibleClass, scopedUser) = await SeedAsync(TenantId);

        var full = await GetAndReadAsync<FinanceDashboardOverviewDto>(Overview + yearId);
        full.Balances.ShouldNotBeNull();
        full.Balances.Outstanding.ShouldBe(841.75m);
        full.Balances.Overdue.ShouldBe(841.75m);
        full.Balances.StudentsWithOverdueFees.ShouldBe(2);
        var selectedDues = await GetAndReadAsync<FinanceDashboardOverviewDto>(
            Overview + yearId + "&from=2025-06-01&to=2025-06-01");
        selectedDues.Balances!.Outstanding.ShouldBe(140.75m);
        selectedDues.Balances.Classes.Single().ClassId.ShouldBe(visibleClass);
        var outsideDues = await GetAndReadAsync<FinanceDashboardOverviewDto>(
            Overview + yearId + "&from=2025-06-12&to=2025-06-12");
        outsideDues.Balances!.Outstanding.ShouldBe(0m);
        outsideDues.Balances.Classes.ShouldBeEmpty();

        using var restricted = CreateClientAs(scopedUser);
        restricted.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");
        using var overviewResponse = await restricted.GetAsync(Overview + yearId);
        overviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await overviewResponse.Content.ReadAsStringAsync());
        var visible = await overviewResponse.Content.ReadFromJsonAsync<FinanceDashboardOverviewDto>(JsonOptions);
        visible.ShouldNotBeNull();
        visible.Balances.ShouldNotBeNull();
        visible.Balances.Outstanding.ShouldBe(140.75m);
        visible.Balances.StudentsWithOverdueFees.ShouldBe(1);
        visible.Balances.Classes.Single().ClassId.ShouldBe(visibleClass);

        var period = Activity + yearId + "&from=2025-06-12&to=2025-06-12";
        var allActivity = await GetAndReadAsync<FinanceDashboardActivityDto>(period);
        allActivity.ConfirmedFees.ShouldBe(115m);
        allActivity.PaymentMethods.Single(row => row.Method == PaymentMethodEnum.Cash).Amount.ShouldBe(100m);
        allActivity.PaymentMethods.Single(row => row.Method == PaymentMethodEnum.CreditCard).Amount.ShouldBe(45m);
        using var activityResponse = await restricted.GetAsync(period);
        activityResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await activityResponse.Content.ReadAsStringAsync());
        var scopedActivity = await activityResponse.Content.ReadFromJsonAsync<FinanceDashboardActivityDto>(JsonOptions);
        scopedActivity.ShouldNotBeNull();
        scopedActivity.ConfirmedFees.ShouldBe(40m);
        scopedActivity.ReceiptCount.ShouldBe(2);
        scopedActivity.Days.Single().Date.ShouldBe(new DateOnly(2025, 6, 12));
        scopedActivity.PaymentMethods.Single(row => row.Method == PaymentMethodEnum.Cash).Amount.ShouldBe(25m);
        scopedActivity.PaymentMethods.Single(row => row.Method == PaymentMethodEnum.CreditCard).Amount.ShouldBe(45m);

        var emptyActivity = await GetAndReadAsync<FinanceDashboardActivityDto>(
            Activity + yearId + "&from=2025-06-11&to=2025-06-11");
        emptyActivity.ConfirmedFees.ShouldBe(0m);
        emptyActivity.PaymentMethods.ShouldBeEmpty();

        using var invalidPeriod = await Client.GetAsync(Activity + yearId + "&from=2024-12-31&to=2025-06-12");
        invalidPeriod.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var incompleteOverviewPeriod = await Client.GetAsync(Overview + yearId + "&from=2025-06-12");
        incompleteOverviewPeriod.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var anonymous = CreateAnonymousClient();
        using var unauthenticated = await anonymous.GetAsync(Overview + yearId);
        unauthenticated.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var (foreignYearId, _, _) = await SeedAsync(Guid.NewGuid());
        using var forbidden = await Client.GetAsync(Overview + foreignYearId);
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<(Guid Year, Guid VisibleClass, Guid RestrictedUser)> SeedAsync(Guid tenantId)
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        async Task<T> Save<T>(T entity) where T : class, IEntity<Guid> =>
            await services.GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, autoSave: true);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var year = await Save(await services.GetRequiredService<AcademicYearManager>().CreateAsync(
            "Dashboard " + suffix, new DateTime(2025, 1, 1), new DateTime(2025, 12, 31)));
        var board = await Save(await services.GetRequiredService<EducationBoardManager>().CreateAsync(
            year.Id, EducationBoardType.CBSE, year.StartDate, year.EndDate));
        var medium = await Save(await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "English"));
        var stage = await Save(await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Primary"));
        var visible = await Save(await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Visible", 6, null));
        var hidden = await Save(await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Hidden", 6, null));

        var restricted = Guid.NewGuid();
        await Save(new IdentityUser(restricted, "dashboard-" + suffix, suffix + "@example.test", tenantId));
        await Save(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: restricted, academicYearId: year.Id,
            educationBoardId: board.Id, educationMediumId: medium.Id, educationMediumStageId: stage.Id, standardId: visible.Id));

        var category = await Save(new FeeCategory(Guid.NewGuid(), tenantId, year.Id, "Dashboard fees", null));
        var accountHead = await Save(new AccountHead(Guid.NewGuid(), tenantId, year.Id, "Dashboard revenue",
            AccountHeadType.Income, null, null));
        var income = await Save(new TransactionCategory(Guid.NewGuid(), tenantId, year.Id, TransactionType.Income,
            "Dashboard income", null, [], true, "DASH-" + suffix, accountHead.Id, DateTimeOffset.UtcNow.AddDays(-1), null));
        var head = await Save(new FeeHead(Guid.NewGuid(), tenantId, year.Id, category.Id, "Dashboard tuition",
            FeeHeadModule.Finance, false, null, income.Id));

        var paymentDate = new DateTimeOffset(2025, 6, 12, 0, 0, 0, TimeSpan.Zero);
        foreach (var standard in new[] { visible, hidden })
        {
            var student = await Save(new Student(Guid.NewGuid(), tenantId, "Dashboard", null, standard.Name,
                year.Id, board.Id, medium.Id, stage.Id, standard.Id, null, status: StudentLifeCycleStatusEnum.Enrolled));
            var amount = standard.Id == visible.Id ? 101.25m : 701m;
            await services.GetRequiredService<IDueFeeRepository>().InsertAsync(
                new DueFee(Guid.NewGuid(), tenantId, student.Id, head.Id, MonthEnum.June, 2025, false,
                    amount, new DateTime(2025, 6, standard.Id == visible.Id ? 1 : 2)), autoSave: true);

            var receiptAmount = standard.Id == visible.Id ? 25m : 75m;
            var code = "DASH-" + Guid.NewGuid().ToString("N")[..16];
            var transaction = await Save(new FeeRecordMetadata(Guid.NewGuid(), tenantId, student.Id,
                year.Id, (double)receiptAmount, code, FeeCollectionStatus.Paid));
            await Save(PaymentMethodRecord.Create(student.Id, transaction.Id, receiptAmount,
                paymentDate, PaymentMethodEnum.Cash, tenantId: tenantId));
            await Save(new FeePaymentReceipt(Guid.NewGuid())
            {
                TenantId = tenantId, StudentId = student.Id, FeeCollectionTransactionId = transaction.Id,
                AcademicYearId = year.Id, BoardId = board.Id, EducationMediumId = medium.Id,
                ReceiptNumber = code, ReceiptKind = FeeReceiptKind.FinalFee, ReceiptScope = FeeReceiptScope.Fee,
                VerificationStatus = FeeReceiptVerificationStatus.NotApplicable, ReceiptDate = new DateOnly(2025, 6, 12),
                TotalPaidAmount = receiptAmount,
            });
        }

        var applicant = await Save(new FinanceFeeAccount(Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid(),
            year.Id, board.Id, medium.Id, stage.Id, visible.Id, "Dashboard Applicant"));
        await services.GetRequiredService<IDueFeeRepository>().InsertAsync(
            new DueFee(Guid.NewGuid(), tenantId, null, head.Id, MonthEnum.June, 2025, false, 39.50m,
                new DateTime(2025, 6, 1), financeFeeAccountId: applicant.Id), autoSave: true);
        var applicantCode = "DASH-" + Guid.NewGuid().ToString("N")[..16];
        var applicantCollection = await Save(new FeeRecordMetadata(Guid.NewGuid(), tenantId, null,
            year.Id, 45d, applicantCode, FeeCollectionStatus.Paid, financeFeeAccountId: applicant.Id));
        await Save(PaymentMethodRecord.Create(null, applicantCollection.Id, 45m, paymentDate,
            PaymentMethodEnum.CreditCard, tenantId: tenantId, financeFeeAccountId: applicant.Id));
        var combined = new FeePaymentReceipt(Guid.NewGuid())
        {
            TenantId = tenantId, FeeCollectionTransactionId = applicantCollection.Id,
            AcademicYearId = year.Id, BoardId = board.Id, EducationMediumId = medium.Id,
            ReceiptNumber = applicantCode, ReceiptKind = FeeReceiptKind.FinalFee, ReceiptScope = FeeReceiptScope.Combined,
            VerificationStatus = FeeReceiptVerificationStatus.NotApplicable, ReceiptDate = new DateOnly(2025, 6, 12),
            TotalPaidAmount = 45m,
        };
        combined.SetFinanceFeeAccount(applicant.Id);
        combined.SetAmountBreakdown(15m, 30m);
        await Save(combined);

        await uow.CompleteAsync();
        return (year.Id, visible.Id, restricted);
    }
}
