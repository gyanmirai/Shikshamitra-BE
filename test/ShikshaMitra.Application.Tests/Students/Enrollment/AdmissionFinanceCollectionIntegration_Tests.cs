using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
    [Fact]
    public Task Full_admission_payment_should_remain_settled_after_onboarding_handoff() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("FullyPaidHandoff");
            var service = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var prepared = await service.PrepareAsync(fixture.Application.Id, new());
            prepared.IsSettled.ShouldBeFalse();
            prepared.CanCollect.ShouldBeTrue();
            var due = (await AccountDuesAsync(prepared.AccountId!.Value)).Single();
            var command = AdmissionCashPayment(due, 1000m, "fully-paid-handoff");
            var payment = await service.CollectAsync(fixture.Application.Id, command);
            var settled = await service.GetSummaryAsync(fixture.Application.Id);
            settled.IsSettled.ShouldBeTrue();
            settled.CanCollect.ShouldBeFalse();
            settled.OutstandingAmount.ShouldBe(0m);
            settled.ConfirmedPaidAmount.ShouldBe(1000m);

            await CompleteCollectibleDecisionAsync(fixture.Application.Id);
            var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "fully-paid-finalize");
            var sourceApplication = await ReadAsync<AdmissionApplication>(fixture.Application.Id);
            sourceApplication.HasFinanceFeePayment.ShouldBeTrue();
            (sourceApplication.GetFeeCollectionDeferral() == null).ShouldBeTrue();
            sourceApplication.GetFeeSelection()!.SavedAt.UtcDateTime.ShouldBeLessThanOrEqualTo(sourceApplication.FinalizedAt!.Value);
            (await GetRequiredService<IAdmissionFinanceIntegration>().IsSettledForOnboardingAsync(fixture.Application.Id)).ShouldBeTrue();
            var onboarding = await GetRequiredService<IStudentOnboardingAppService>().GetAsync(finalized.OnboardingCaseId);
            onboarding.ApplicationId.ShouldBe(fixture.Application.Id);
            onboarding.FeeStructureConfirmed.ShouldBeTrue();
            onboarding.FeeAssignmentConfirmed.ShouldBe(true);
            onboarding.Steps.Single(s => s.Key == StudentOnboardingStepKeys.FinanceAccountsServices)
                .Status.ShouldBe(StudentOnboardingStepStatus.Completed);
            onboarding.StudentEnrollmentId.ShouldBeNull();
            var onboardingService = GetRequiredService<IStudentOnboardingAppService>();
            var selection = await onboardingService.GetFeeStructureAsync(onboarding.Id);
            selection.OnboardingConfirmed.ShouldBeTrue();
            selection.AutomaticallyConfirmedFromAdmissions.ShouldBeTrue();
            selection.CanConfirmOnboarding.ShouldBeFalse();
            selection.Selection!.OnboardingConfirmedAt.ShouldBeNull();
            var pending = await onboardingService.GetListAsync(new StudentOnboardingListInput
                { AcademicYearId = fixture.YearId, PendingTask = "fees", MaxResultCount = 100 });
            pending.Items.ShouldNotContain(c => c.Id == onboarding.Id);
            // A stale client posting the same selection is a no-op; it cannot create another confirmation.
            await onboardingService.SaveFeeStructureAsync(onboarding.Id, new SaveStudentOnboardingFeeStructureDto
            {
                ConcurrencyStamp = onboarding.ConcurrencyStamp,
                StructureId = selection.Selection.StructureId,
                OptionalHeadIds = selection.Selection.Heads.Where(h => h.IsOptional).Select(h => h.FeeHeadId).ToList()
            });
            var reread = await onboardingService.GetAsync(onboarding.Id);
            reread.Events.ShouldNotContain(e => e.EventType == "FeeStructureConfirmed");
            (await ReadAsync<AdmissionApplication>(fixture.Application.Id)).GetFeeSelection()!.OnboardingConfirmedAt.ShouldBeNull();
            var inherited = await service.GetSummaryAsync(onboarding.ApplicationId);
            inherited.IsSettled.ShouldBeTrue();
            inherited.CanCollect.ShouldBeFalse();
            inherited.Receipts.Single().Id.ShouldBe(payment.FeeReceiptId!.Value);
            (await AccountDuesAsync(prepared.AccountId.Value)).Single().Id.ShouldBe(due.Id);
            // Existing idempotent retries remain valid even after checkout becomes read-only.
            (await service.CollectAsync(fixture.Application.Id, command)).Id.ShouldBe(payment.Id);
        });

    [Fact]
    public Task Admission_collection_read_should_not_prepare_and_repeated_prepare_should_reuse_obligations() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("ReadPrepare");
            var service = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var preview = await service.GetSummaryAsync(fixture.Application.Id);
            preview.AccountId.ShouldBeNull();
            preview.TotalAmount.ShouldBe(1000m);
            (await AccountCountAsync(fixture.Application.Id)).ShouldBe(0);

            var first = await service.PrepareAsync(fixture.Application.Id, new());
            var original = await AccountDuesAsync(first.AccountId!.Value);
            var second = await service.PrepareAsync(fixture.Application.Id, new());
            second.AccountId.ShouldBe(first.AccountId);
            (await AccountCountAsync(fixture.Application.Id)).ShouldBe(1);
            (await AccountDuesAsync(first.AccountId.Value)).Select(d => d.Id).ShouldBe(original.Select(d => d.Id));
            original.ShouldNotBeEmpty();
            original.ShouldAllBe(d => d.StudentId == null && d.StudentFeeStructureId == null);
            original.Sum(d => d.OriginalAmount).ShouldBe(1000m);
            var form = await service.GetCollectFormAsync(fixture.Application.Id);
            form.Student.StudentId.ShouldBeNull();
            form.Student.FinanceFeeAccountId.ShouldBe(first.AccountId);
            form.Ledger.SelectMany(m => m.Items).Select(d => d.DueFeeId).Order().ShouldBe(original.Select(d => d.Id).Order());
            (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>().CountAsync())).ShouldBe(0);
        });

    [Fact]
    public Task Admission_collection_replay_should_return_one_receipt_and_reject_changed_payment() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("Replay");
            var service = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var prepared = await service.PrepareAsync(fixture.Application.Id, new());
            var due = (await AccountDuesAsync(prepared.AccountId!.Value)).Single();
            var command = AdmissionCashPayment(due, 300m, "admission-payment-retry");
            var first = await service.CollectAsync(fixture.Application.Id, command);
            var repeated = await service.CollectAsync(fixture.Application.Id, command);
            repeated.Id.ShouldBe(first.Id);
            repeated.FeeReceiptId.ShouldBe(first.FeeReceiptId);
            repeated.ReceiptNumber.ShouldBe(first.ReceiptNumber);
            first.StudentId.ShouldBeNull();
            first.FinanceFeeAccountId.ShouldBe(prepared.AccountId);
            await AssertOneCollectionAsync(first, prepared.AccountId.Value);
            (await Should.ThrowAsync<UserFriendlyException>(() => service.CollectAsync(fixture.Application.Id,
                AdmissionCashPayment(due, 301m, command.IdempotencyKey)))).Message.ShouldContain("retry key");
            await AssertOneCollectionAsync(first, prepared.AccountId.Value);
            var balance = await service.GetSummaryAsync(fixture.Application.Id);
            balance.ConfirmedPaidAmount.ShouldBe(300m);
            balance.PendingVerificationAmount.ShouldBe(0m);
            balance.OutstandingAmount.ShouldBe(700m);
            balance.Receipts.Single().Id.ShouldBe(first.FeeReceiptId!.Value);
            balance.HasPaymentEvidence.ShouldBeTrue();
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Admission_account_activation_should_preserve_due_identity_and_optional_partial_payment(bool collectBeforeFinalization) =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("Handoff");
            var service = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var prepared = await service.PrepareAsync(fixture.Application.Id, new());
            var accountId = prepared.AccountId!.Value;
            var due = (await AccountDuesAsync(accountId)).Single();
            CollectFeeOutput? payment = null;
            FeeRecordMetadata? originalMetadata = null;
            FeePaymentReceipt? originalReceipt = null;
            if (collectBeforeFinalization)
            {
                payment = await service.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 300m, "handoff-partial"));
                originalMetadata = await ReadAsync<FeeRecordMetadata>(payment.Id);
                originalReceipt = await ReadAsync<FeePaymentReceipt>(payment.FeeReceiptId!.Value);
                // A pinned, already-used schedule must survive subsequent master archival.
                await WithUnitOfWorkAsync(async () =>
                {
                    var terms = GetRequiredService<IRepository<PaymentTerm, Guid>>();
                    var term = await terms.GetAsync(fixture.TermId);
                    term.Archive("The next intake uses a new schedule.");
                    await terms.UpdateAsync(term, true);
                });
            }
            await CompleteCollectibleDecisionAsync(fixture.Application.Id);
            var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "finance-handoff-finalize");
            var linked = (await AccountDuesAsync(accountId)).Single();
            linked.Id.ShouldBe(due.Id);
            linked.StudentId.ShouldBe(finalized.StudentId);
            linked.StudentFeeStructureId.ShouldBeNull();
            linked.OriginalAmount.ShouldBe(due.OriginalAmount);
            linked.PaidAmount.ShouldBe(collectBeforeFinalization ? 300m : 0m);
            var linkedForm = await GetRequiredService<IFeeCollectionAppService>().GetCollectFormAsync(finalized.StudentId, fixture.YearId);
            linkedForm.Student.StudentId.ShouldBe(finalized.StudentId);
            linkedForm.Student.FinanceFeeAccountId.ShouldBe(accountId);
            linkedForm.Ledger.SelectMany(m => m.Items).Single().DueFeeId.ShouldBe(due.Id);
            if (payment != null)
            {
                var metadata = await ReadAsync<FeeRecordMetadata>(payment.Id);
                metadata.StudentId.ShouldBe(finalized.StudentId);
                metadata.CommandFingerprint.ShouldBe(originalMetadata!.CommandFingerprint);
                metadata.CommandResultJson.ShouldBe(originalMetadata.CommandResultJson);
                var receipt = await ReadAsync<FeePaymentReceipt>(payment.FeeReceiptId!.Value);
                receipt.StudentId.ShouldBe(finalized.StudentId);
                receipt.ReceiptNumber.ShouldBe(originalReceipt!.ReceiptNumber);
                receipt.PdfBlobName.ShouldBe(originalReceipt.PdfBlobName);
                (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeeCollectionRecord, Guid>>()
                    .GetListAsync(r => r.FinanceFeeAccountId == accountId))).Single().StudentId.ShouldBe(finalized.StudentId);
                (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<PaymentMethodRecord, Guid>>()
                    .GetListAsync(r => r.FinanceFeeAccountId == accountId))).Single().StudentId.ShouldBe(finalized.StudentId);
                var income = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.Incomes.Transaction, Guid>>()
                    .GetListAsync(r => r.FinanceFeeAccountId == accountId));
                income.ShouldNotBeEmpty();
                income.ShouldAllBe(r => r.StudentId == finalized.StudentId);
                // The original response remains stable even after the true Student is linked.
                (await service.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 300m, "handoff-partial"))).Id.ShouldBe(payment.Id);
            }

            var item = await ActivateCollectibleOnboardingAsync(fixture, finalized);
            item.StudentEnrollmentId.ShouldNotBeNull();
            item.Status.ShouldBe(StudentOnboardingStatus.Completed, item.LastError);
            var assignment = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>()
                .GetAsync(a => a.StudentId == finalized.StudentId && a.AcademicYearId == fixture.YearId));
            assignment.FinanceFeeAccountId.ShouldBe(accountId);
            var boundDue = (await AccountDuesAsync(accountId)).Single();
            boundDue.Id.ShouldBe(due.Id);
            boundDue.StudentFeeStructureId.ShouldBe(assignment.Id);
            boundDue.OriginalAmount.ShouldBe(1000m);
            boundDue.PaidAmount.ShouldBe(collectBeforeFinalization ? 300m : 0m);
            (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>()
                .CountAsync(a => a.StudentId == finalized.StudentId && a.AcademicYearId == fixture.YearId))).ShouldBe(1);
            (await service.PrepareAsync(fixture.Application.Id, new())).AccountId.ShouldBe(accountId);
            (await AccountDuesAsync(accountId)).Single().Id.ShouldBe(due.Id);
            if (!collectBeforeFinalization)
            {
                // A pending instrument from another academic year must not inflate this ledger's summary.
                await WithUnitOfWorkAsync(async () =>
                {
                    var currentYear = await GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(fixture.YearId);
                    var laterYear = await GetRequiredService<AcademicYearManager>().CreateAsync("Other collection year",
                        currentYear.StartDate.AddYears(1), currentYear.EndDate.AddYears(1));
                    await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(laterYear, true);
                    var laterIncome = await FeeHeadAccountingTestData.CreateIncomeCategoryAsync(
                        GetRequiredService<IRepository<AccountHead, Guid>>(), GetRequiredService<IRepository<TransactionCategory, Guid>>(),
                        laterYear.Id, TestTenantId, "Other year payment");
                    var laterCategory = await GetRequiredService<IRepository<FeeCategory, Guid>>()
                        .InsertAsync(new(Guid.NewGuid(), TestTenantId, laterYear.Id, "Other year fees", null), true);
                    var laterHead = await GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId,
                        laterYear.Id, laterCategory.Id, "Other year fee", FeeHeadModule.Finance, false, null, laterIncome), true);
                    var otherDue = new DueFee(Guid.NewGuid(), TestTenantId, finalized.StudentId, laterHead.Id,
                        (MonthEnum)(laterYear.StartDate.Month - 1), laterYear.StartDate.Year, true, 500m, laterYear.StartDate.AddDays(10));
                    await GetRequiredService<IRepository<DueFee, Guid>>().InsertAsync(otherDue, true);
                    var pending = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, finalized.StudentId, laterYear.Id,
                        150d, "OTHER-YEAR-PENDING", FeeCollectionStatus.InReconciliation);
                    await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(pending, true);
                    await GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>()
                        .InsertAsync(new(Guid.NewGuid(), TestTenantId, pending.Id, otherDue.Id, 150d), true);
                });
                var selectedYear = await GetRequiredService<IFeeCollectionAppService>().GetCollectFormAsync(finalized.StudentId, fixture.YearId);
                selectedYear.Summary.ReconciliationAmount.ShouldBe(0d);
                selectedYear.Ledger.SelectMany(m => m.Items).Select(d => d.DueFeeId).ShouldBe(new[] { due.Id });
            }

            // Existing Finance entry must continue paying the same ledger after activation.
            var normal = GetRequiredService<IFeeCollectionAppService>();
            var remaining = collectBeforeFinalization ? 700d : 1000d;
            CollectFeeInput NormalCommand() => new()
            {
                StudentId = finalized.StudentId, AcademicYearId = fixture.YearId,
                IdempotencyKey = "finance-after-activation",
                DueFeePayments = [new() { DueFeeId = due.Id, FeeHeadId = due.FeeHeadId, Amount = remaining }],
                Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = remaining }]
            };
            var normalPayment = await normal.CollectFeeAsync(NormalCommand());
            normalPayment.FinanceFeeAccountId.ShouldBe(accountId);
            (await normal.CollectFeeAsync(NormalCommand())).Id.ShouldBe(normalPayment.Id);
            var closedBalance = await service.GetSummaryAsync(fixture.Application.Id);
            closedBalance.ConfirmedPaidAmount.ShouldBe(1000m);
            closedBalance.OutstandingAmount.ShouldBe(0m);
            (await AccountDuesAsync(accountId)).Single().PaidAmount.ShouldBe(1000m);
            var retry = await GetRequiredService<IStudentOnboardingAppService>().ActivateAsync(item.Id,
                new() { IdempotencyKey = "finance-handoff-activate", ConcurrencyStamp = item.ConcurrencyStamp });
            retry.StudentEnrollmentId.ShouldBe(item.StudentEnrollmentId);
            (await AccountDuesAsync(accountId)).Single().Id.ShouldBe(due.Id);
        });

    [Fact]
    public Task Admission_finance_account_should_not_be_read_or_collected_from_another_tenant_or_unscoped_user() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("Scope");
            var service = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var readiness = GetRequiredService<IAdmissionFinanceIntegration>();
            var prepared = await service.PrepareAsync(fixture.Application.Id, new());
            var due = (await AccountDuesAsync(prepared.AccountId!.Value)).Single();
            using (_currentTenant.Change(Guid.NewGuid()))
            {
                await Should.ThrowAsync<EntityNotFoundException>(() => service.GetSummaryAsync(fixture.Application.Id));
                await Should.ThrowAsync<EntityNotFoundException>(() => readiness.IsSettledForOnboardingAsync(fixture.Application.Id));
                await Should.ThrowAsync<EntityNotFoundException>(() => service.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 100m, "wrong-tenant")));
            }
            using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()), new Claim(AbpClaimTypes.UserName, "no-finance-scope")], "Test"))))
            {
                await Should.ThrowAsync<EntityNotFoundException>(() => service.GetSummaryAsync(fixture.Application.Id));
                await Should.ThrowAsync<EntityNotFoundException>(() => readiness.IsSettledForOnboardingAsync(fixture.Application.Id));
                await Should.ThrowAsync<EntityNotFoundException>(() => service.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 100m, "wrong-scope")));
            }
            (await AccountDuesAsync(prepared.AccountId.Value)).Single().PaidAmount.ShouldBe(0m);
            (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().CountAsync())).ShouldBe(0);
        });

    [Fact]
    public Task Admission_preparation_should_adopt_compatible_existing_student_assignment_without_duplicate_dues() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("ExistingFinance");
            await CompleteCollectibleDecisionAsync(fixture.Application.Id);
            var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "legacy-assignment-finalize");
            await ActivateCollectibleOnboardingAsync(fixture, finalized);
            var assignments = GetRequiredService<IStudentFeeStructureAppService>();
            var input = new AssignStudentFeeStructureDto
            {
                StudentId = finalized.StudentId, AcademicYearId = fixture.YearId, StandardFeeStructureId = fixture.StructureId
            };
            input.PreviewToken = (await assignments.PreviewAsync(input)).PreviewToken;
            var committed = await assignments.AssignAsync(input);
            var originalDue = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<DueFee, Guid>>()
                .GetAsync(d => d.StudentFeeStructureId == committed.AssignmentId));
            originalDue.FinanceFeeAccountId.ShouldBeNull();
            var normalPayment = await GetRequiredService<IFeeCollectionAppService>().CollectFeeAsync(new()
            {
                StudentId = finalized.StudentId, AcademicYearId = fixture.YearId, IdempotencyKey = "pre-existing-finance-payment",
                DueFeePayments = [new() { DueFeeId = originalDue.Id, FeeHeadId = originalDue.FeeHeadId, Amount = 300d }],
                Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 300d }]
            });
            var admissions = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var first = await admissions.PrepareAsync(fixture.Application.Id, new());
            var repeat = await admissions.PrepareAsync(fixture.Application.Id, new());
            repeat.AccountId.ShouldBe(first.AccountId);
            var adoptedDue = (await AccountDuesAsync(first.AccountId!.Value)).Single();
            adoptedDue.Id.ShouldBe(originalDue.Id);
            adoptedDue.StudentFeeStructureId.ShouldBe(committed.AssignmentId);
            adoptedDue.PaidAmount.ShouldBe(300m);
            first.ConfirmedPaidAmount.ShouldBe(300m);
            first.OutstandingAmount.ShouldBe(700m);
            first.HasPaymentEvidence.ShouldBeTrue();
            first.Receipts.Single().Id.ShouldBe(normalPayment.FeeReceiptId!.Value);
            (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<DueFee, Guid>>().CountAsync(d => d.StudentId == finalized.StudentId))).ShouldBe(1);
            (await ReadAsync<StudentFeeStructure>(committed.AssignmentId)).FinanceFeeAccountId.ShouldBe(first.AccountId);
            (await AccountCountAsync(fixture.Application.Id)).ShouldBe(1);
        });

    [Fact]
    public Task Onboarding_collection_should_work_when_the_admission_workflow_has_no_payment_stage() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("OnboardingOnly", paymentStage: false);
            await CompleteCollectibleDecisionAsync(fixture.Application.Id);
            var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "onboarding-only-finalize");
            var onboarding = GetRequiredService<IStudentOnboardingAppService>();
            var item = await onboarding.GetAsync(finalized.OnboardingCaseId);
            var selection = await onboarding.SaveFeeStructureAsync(item.Id,
                new() { StructureId = fixture.StructureId, ConcurrencyStamp = item.ConcurrencyStamp });
            selection.OnboardingConfirmed.ShouldBeTrue();
            var fees = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var account = await fees.PrepareAsync(fixture.Application.Id, new());
            account.StudentId.ShouldBe(finalized.StudentId);
            var due = (await AccountDuesAsync(account.AccountId!.Value)).Single();
            due.StudentFeeStructureId.ShouldBeNull();
            var payment = await fees.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 300m, "onboarding-only-payment"));
            payment.StudentId.ShouldBe(finalized.StudentId);
            var activated = await ActivateCollectibleOnboardingAsync(fixture, finalized);
            activated.StudentEnrollmentId.ShouldNotBeNull();
            var persisted = (await AccountDuesAsync(account.AccountId.Value)).Single();
            persisted.Id.ShouldBe(due.Id);
            persisted.StudentFeeStructureId.ShouldNotBeNull();
            persisted.PaidAmount.ShouldBe(300m);
            var balance = await fees.GetSummaryAsync(fixture.Application.Id);
            balance.OutstandingAmount.ShouldBe(700m);
            balance.Receipts.Single().Id.ShouldBe(payment.FeeReceiptId!.Value);
        });

    [Fact]
    public Task Admission_pending_instrument_should_reserve_balance_once_and_keep_verification_status_after_handoff() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("PendingInstrument");
            var fees = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var prepared = await fees.PrepareAsync(fixture.Application.Id, new());
            var due = (await AccountDuesAsync(prepared.AccountId!.Value)).Single();
            var command = AdmissionCashPayment(due, 300m, "admission-pending-instrument");
            command.Payments = [new()
            {
                PaymentMethod = PaymentMethodEnum.Cheque, Amount = 300m, BankAccountId = fixture.BankId,
                Details = new() { ["chequeNumber"] = "000001", ["bankName"] = "QA Bank", ["chequeDate"] = DateTime.UtcNow.ToString("O") }
            }];
            var pending = await fees.CollectAsync(fixture.Application.Id, command);
            pending.Status.ShouldBe(FeeCollectionStatus.InReconciliation);
            (await fees.CollectAsync(fixture.Application.Id, command)).FeeReceiptId.ShouldBe(pending.FeeReceiptId);
            await AssertOneCollectionAsync(pending, prepared.AccountId.Value);
            var before = await fees.GetSummaryAsync(fixture.Application.Id);
            before.ConfirmedPaidAmount.ShouldBe(0m);
            before.PendingVerificationAmount.ShouldBe(300m);
            before.OutstandingAmount.ShouldBe(700m);
            before.Receipts.Single().Status.ShouldBe("pendingVerification");
            await CompleteCollectibleDecisionAsync(fixture.Application.Id);
            var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "pending-finance-finalize");
            await ActivateCollectibleOnboardingAsync(fixture, finalized);
            var stored = await ReadAsync<FeeRecordMetadata>(pending.Id);
            stored.StudentId.ShouldBe(finalized.StudentId);
            stored.Status.ShouldBe(FeeCollectionStatus.InReconciliation);
            var after = await fees.GetSummaryAsync(fixture.Application.Id);
            after.ConfirmedPaidAmount.ShouldBe(0m);
            after.PendingVerificationAmount.ShouldBe(300m);
            after.OutstandingAmount.ShouldBe(700m);
            after.Receipts.Single().Id.ShouldBe(pending.FeeReceiptId!.Value);
            (await AccountDuesAsync(prepared.AccountId.Value)).Single().Id.ShouldBe(due.Id);
            await AssertOneCollectionAsync(pending, prepared.AccountId.Value);
        });

    [Fact]
    public Task Admission_account_activation_should_discard_only_unused_drafts_after_selection_changes_without_requiring_collection() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("UnusedDraft");
            var fees = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var opened = await fees.PrepareAsync(fixture.Application.Id, new());
            var abandonedDue = (await AccountDuesAsync(opened.AccountId!.Value)).Single();
            var replacement = await CreateCollectibleRevisionAsync(fixture, 1200m);
            var selections = GetRequiredService<IAdmissionFeeSelectionAppService>();
            var state = await selections.GetAsync(fixture.Application.Id);
            await selections.SaveAsync(fixture.Application.Id, new() { StructureId = replacement.Id, ConcurrencyStamp = state.ConcurrencyStamp });
            await CompleteCollectibleDecisionAsync(fixture.Application.Id);
            var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "unused-draft-finalize");
            var activated = await ActivateCollectibleOnboardingAsync(fixture with { StructureId = replacement.Id }, finalized);
            activated.StudentEnrollmentId.ShouldNotBeNull();
            activated.Status.ShouldBe(StudentOnboardingStatus.Completed, activated.LastError);
            var unused = await ReadAsync<FinanceFeeAccount>(opened.AccountId.Value);
            unused.StudentId.ShouldBe(finalized.StudentId);
            unused.AssignmentSnapshotJson.ShouldBeNull();
            unused.StudentFeeStructureId.ShouldBeNull();
            unused.HasPaymentEvidence.ShouldBeFalse();
            (await AccountDuesAsync(unused.Id)).ShouldBeEmpty();
            (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>().CountAsync(a => a.StudentId == finalized.StudentId))).ShouldBe(0);
            var later = await fees.PrepareAsync(fixture.Application.Id, new());
            later.AccountId.ShouldBe(unused.Id);
            later.TotalAmount.ShouldBe(1200m);
            var newDue = (await AccountDuesAsync(unused.Id)).Single();
            newDue.Id.ShouldNotBe(abandonedDue.Id);
            newDue.StudentFeeStructureId.ShouldNotBeNull();
            newDue.OriginalAmount.ShouldBe(1200m);
        });

    [Fact]
    public Task Admission_collection_variable_preparation_replay_should_preserve_dues_before_and_after_payment() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("VariableRetry");
            var fees = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var initial = await fees.PrepareAsync(fixture.Application.Id, new());
            var abandonedDue = (await AccountDuesAsync(initial.AccountId!.Value)).Single();
            var variable = await CreateCollectibleRevisionAsync(fixture, null);
            var selections = GetRequiredService<IAdmissionFeeSelectionAppService>();
            var selection = await selections.GetAsync(fixture.Application.Id);
            await selections.SaveAsync(fixture.Application.Id, new() { StructureId = variable.Id, ConcurrencyStamp = selection.ConcurrencyStamp });
            var requested = await fees.GetSummaryAsync(fixture.Application.Id);
            requested.AccountId.ShouldBe(initial.AccountId);
            requested.TotalAmount.ShouldBeNull();
            requested.VariableFeeHeads.Count.ShouldBe(1);
            var input = new PrepareAdmissionFeeCollectionDto
            {
                VariableFeeHeads = [new() { FeeHeadId = requested.VariableFeeHeads.Single().FeeHeadId, Amount = 750.25m }]
            };
            var first = await fees.PrepareAsync(fixture.Application.Id, input);
            // Finance's existing assignment calculator rounds obligations up to whole rupees.
            first.TotalAmount.ShouldBe(751m);
            var due = (await AccountDuesAsync(first.AccountId!.Value)).Single();
            due.Id.ShouldNotBe(abandonedDue.Id);
            (await fees.PrepareAsync(fixture.Application.Id, input)).AccountId.ShouldBe(first.AccountId);
            (await AccountDuesAsync(first.AccountId.Value)).Single().Id.ShouldBe(due.Id);
            await fees.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 300m, "variable-retry-payment"));
            var replay = await fees.PrepareAsync(fixture.Application.Id, input);
            replay.ConfirmedPaidAmount.ShouldBe(300m);
            replay.OutstandingAmount.ShouldBe(451m);
            (await AccountDuesAsync(first.AccountId.Value)).Single().Id.ShouldBe(due.Id);
            (await AccountCountAsync(fixture.Application.Id)).ShouldBe(1);
        });

    [Fact]
    public Task Admission_collection_summary_should_show_net_confirmed_payment_after_a_refund() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("NetRefund");
            var fees = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var account = await fees.PrepareAsync(fixture.Application.Id, new());
            var due = (await AccountDuesAsync(account.AccountId!.Value)).Single();
            await fees.CollectAsync(fixture.Application.Id, AdmissionCashPayment(due, 300m, "net-refund-payment"));
            await WithUnitOfWorkAsync(async () =>
            {
                var repository = GetRequiredService<IRepository<DueFee, Guid>>();
                var persisted = await repository.GetAsync(due.Id);
                persisted.ApplyRefund(100m);
                await repository.UpdateAsync(persisted, true);
            });
            var summary = await fees.GetSummaryAsync(fixture.Application.Id);
            summary.ConfirmedPaidAmount.ShouldBe(200m);
            summary.OutstandingAmount.ShouldBe(700m);
            summary.FeeHeads.Single().PaidAmount.ShouldBe(200m);
            summary.FeeHeads.Single().OutstandingAmount.ShouldBe(700m);
            summary.HasPaymentEvidence.ShouldBeTrue();
        });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task Admission_collection_receipt_history_should_show_one_cleared_receipt_or_the_rejected_instrument(bool cleared) =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("ReceiptHistory");
            var fees = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var account = await fees.PrepareAsync(fixture.Application.Id, new());
            var due = (await AccountDuesAsync(account.AccountId!.Value)).Single();
            var command = AdmissionCashPayment(due, 300m, "history-instrument");
            command.Payments = [new()
            {
                PaymentMethod = PaymentMethodEnum.Cheque, Amount = 300m, BankAccountId = fixture.BankId,
                Details = new() { ["chequeNumber"] = "000002", ["bankName"] = "QA Bank", ["chequeDate"] = DateTime.UtcNow.ToString("O") }
            }];
            var payment = await fees.CollectAsync(fixture.Application.Id, command);
            var visibleReceiptId = payment.FeeReceiptId!.Value;
            await WithUnitOfWorkAsync(async () =>
            {
                var receiptRepository = GetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
                var provisional = await receiptRepository.GetAsync(payment.FeeReceiptId.Value);
                var metadataRepository = GetRequiredService<IRepository<FeeRecordMetadata, Guid>>();
                var metadata = await metadataRepository.GetAsync(payment.Id);
                if (cleared)
                {
                    var final = new FeePaymentReceipt(Guid.NewGuid())
                    {
                        TenantId = TestTenantId, FeeCollectionTransactionId = payment.Id, AcademicYearId = fixture.YearId,
                        BoardId = fixture.Offering.BoardId, EducationMediumId = fixture.Offering.MediumId,
                        ReceiptScope = FeeReceiptScope.Fee, ReceiptKind = FeeReceiptKind.FinalFee,
                        TotalPaidAmount = 300d, ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow), ReceiptNumber = "FIN-QA-CLEARED"
                    };
                    final.SetFinanceFeeAccount(account.AccountId.Value);
                    final.SetLinkedProvisionalReceiptId(provisional.Id);
                    await receiptRepository.InsertAsync(final, true);
                    provisional.MarkVerified(final.Id, "Instrument cleared.");
                    metadata.MarkPartial();
                    visibleReceiptId = final.Id;
                }
                else
                {
                    provisional.MarkRejected("Instrument returned.");
                    metadata.MarkReversed();
                    var dueRepository = GetRequiredService<IRepository<DueFee, Guid>>();
                    var persisted = await dueRepository.GetAsync(due.Id);
                    persisted.ReversePayment(300m);
                    await dueRepository.UpdateAsync(persisted, true);
                }
                await receiptRepository.UpdateAsync(provisional, true);
                await metadataRepository.UpdateAsync(metadata, true);
            });
            var summary = await fees.GetSummaryAsync(fixture.Application.Id);
            summary.Receipts.Count.ShouldBe(1);
            summary.Receipts.Single().Id.ShouldBe(visibleReceiptId);
            summary.Receipts.Single().Status.ShouldBe(cleared ? "confirmed" : "reversed");
            summary.ConfirmedPaidAmount.ShouldBe(cleared ? 300m : 0m);
            summary.PendingVerificationAmount.ShouldBe(0m);
            summary.OutstandingAmount.ShouldBe(cleared ? 700m : 1000m);
            summary.HasPaymentEvidence.ShouldBeTrue();
        });

    [Fact]
    public Task Admission_collection_normal_finance_entry_before_activation_should_replay_same_unmodified_input() =>
        RunCertificationScenarioAsync(async () =>
        {
            var fixture = await CreateCollectibleAdmissionAsync("NormalBeforeActivation");
            var admissionFees = GetRequiredService<IAdmissionFeeCollectionAppService>();
            var account = await admissionFees.PrepareAsync(fixture.Application.Id, new());
            var due = (await AccountDuesAsync(account.AccountId!.Value)).Single();
            var runtime = await _applicationAppService.GetWorkflowAsync(fixture.Application.Id);
            var current = await _applicationAppService.GetAsync(fixture.Application.Id);
            await _applicationAppService.AdvanceWorkflowAsync(fixture.Application.Id, new()
            {
                ExpectedCurrentStageInstanceId = runtime.CurrentStageInstanceId!.Value,
                ConcurrencyStamp = current.ConcurrencyStamp, IdempotencyKey = "normal-collect-after-finalization",
                DeferFeeCollection = true, Reason = "Parent will pay after admission finalization."
            });
            await CompleteCollectibleDecisionAsync(fixture.Application.Id);
            var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(fixture.Application.Id), true, "normal-before-activation-finalize");
            var input = new CollectFeeInput
            {
                StudentId = finalized.StudentId, AcademicYearId = fixture.YearId, IdempotencyKey = "normal-before-activation-collect",
                DueFeePayments = [new() { DueFeeId = due.Id, FeeHeadId = due.FeeHeadId, Amount = 300d }],
                Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 300d }]
            };
            var finance = GetRequiredService<IFeeCollectionAppService>();
            var first = await finance.CollectFeeAsync(input);
            var retry = await finance.CollectFeeAsync(input);
            input.FinanceFeeAccountId.ShouldBeNull();
            retry.Id.ShouldBe(first.Id);
            retry.FeeReceiptId.ShouldBe(first.FeeReceiptId);
            first.FinanceFeeAccountId.ShouldBe(account.AccountId);
            await AssertOneCollectionAsync(first, account.AccountId.Value);
            var before = await admissionFees.GetSummaryAsync(fixture.Application.Id);
            before.ConfirmedPaidAmount.ShouldBe(300m);
            before.OutstandingAmount.ShouldBe(700m);
            (await AccountDuesAsync(account.AccountId.Value)).Single().StudentFeeStructureId.ShouldBeNull();
            var activated = await ActivateCollectibleOnboardingAsync(fixture, finalized);
            activated.StudentEnrollmentId.ShouldNotBeNull();
            var persisted = (await AccountDuesAsync(account.AccountId.Value)).Single();
            persisted.Id.ShouldBe(due.Id);
            persisted.StudentFeeStructureId.ShouldNotBeNull();
            persisted.PaidAmount.ShouldBe(300m);
            await AssertOneCollectionAsync(first, account.AccountId.Value);
            // Records can correct a live student's name/roll after activation while
            // the original applicant identity remains Finance evidence.
            await WithUnitOfWorkAsync(async () =>
            {
                var student = await _studentRepository.GetAsync(finalized.StudentId);
                student.UpdateGeneralInformation("Updated", null, "Payer");
                student.AssignRollNo(99);
                await _studentRepository.UpdateAsync(student, true);
            });
            foreach (var search in new[] { "99", "Updated" })
            {
                var results = await finance.GetSummaryAsync(new()
                {
                    AcademicYearId = fixture.YearId, BoardId = fixture.Offering.BoardId,
                    EducationMediumId = fixture.Offering.MediumId, Search = search, MaxResultCount = 10
                });
                results.TotalCount.ShouldBe(1);
                var row = results.Items.Single();
                row.StudentId.ShouldBe(finalized.StudentId);
                row.FinanceFeeAccountId.ShouldBe(account.AccountId);
                row.TotalFees.ShouldBe(1000d);
                row.PaidFees.ShouldBe(300d);
                row.PendingFees.ShouldBe(700d);
            }
            await Should.ThrowAsync<Volo.Abp.Validation.AbpValidationException>(() => finance.GetSummaryAsync(new()
            {
                AcademicYearId = fixture.YearId, BoardId = fixture.Offering.BoardId,
                EducationMediumId = fixture.Offering.MediumId, Search = "1", MaxResultCount = 10
            }));
        });

    private Task<T> ReadAsync<T>(Guid id) where T : class, IEntity<Guid> =>
        WithUnitOfWorkAsync(() => GetRequiredService<IRepository<T, Guid>>().GetAsync(id));

    private Task<int> AccountCountAsync(Guid applicationId) =>
        WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().CountAsync(a => a.ApplicationId == applicationId));

    private Task<System.Collections.Generic.List<DueFee>> AccountDuesAsync(Guid accountId) =>
        WithUnitOfWorkAsync(() => GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(d => d.FinanceFeeAccountId == accountId));

    private static CollectAdmissionFeeInput AdmissionCashPayment(DueFee due, decimal amount, string key) => new()
    {
        IdempotencyKey = key,
        DueFeePayments = [new() { DueFeeId = due.Id, FeeHeadId = due.FeeHeadId, Amount = amount }],
        Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = amount }]
    };

    private async Task AssertOneCollectionAsync(CollectFeeOutput result, Guid accountId)
    {
        (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().CountAsync(m => m.FinanceFeeAccountId == accountId))).ShouldBe(1);
        (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().CountAsync(a => a.FeeRecordMetadataId == result.Id))).ShouldBe(1);
        (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().CountAsync(r => r.FinanceFeeAccountId == accountId))).ShouldBe(1);
        (await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<PaymentMethodRecord, Guid>>().CountAsync(r => r.FinanceFeeAccountId == accountId))).ShouldBe(1);
    }

    private async Task<CollectibleAdmissionFixture> CreateCollectibleAdmissionAsync(string name, bool paymentStage = true,
        decimal amount = 1000m, bool oneTime = true)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var roles = GetRequiredService<Volo.Abp.Identity.IdentityRoleManager>();
            if (!await roles.RoleExistsAsync("admin"))
                (await roles.CreateAsync(new Volo.Abp.Identity.IdentityRole(Guid.NewGuid(), "admin", TestTenantId))).Succeeded.ShouldBeTrue();
            var users = GetRequiredService<Volo.Abp.Identity.IdentityUserManager>();
            var user = (await users.FindByIdAsync(TestUserId.ToString()))!;
            if (!await users.IsInRoleAsync(user, "admin")) (await users.AddToRoleAsync(user, "admin")).Succeeded.ShouldBeTrue();
        });
        var entry = Guid.NewGuid(); var decision = Guid.NewGuid();
        var workflow = await _workflowAppService.CreateAsync(new()
        {
            Name = "Finance collection journey", Code = $"COLLECT_{Guid.NewGuid():N}"[..24],
            Stages = paymentStage ? [new() { Id = entry, Code = "FEES", Name = "Fee structure confirmation",
                StageType = AdmissionWorkflowStageType.Payment,
                IsEntryStage = true, IsRequired = true }, new() { Id = decision, Code = "DECISION", Name = "Admission decision",
                StageType = AdmissionWorkflowStageType.Approval, DisplayOrder = 1, IsTerminalStage = true, IsRequired = true }]
                : [new() { Id = decision, Code = "DECISION", Name = "Admission decision", StageType = AdmissionWorkflowStageType.Approval,
                    IsEntryStage = true, IsTerminalStage = true, IsRequired = true }],
            Transitions = paymentStage ? [new() { FromStageId = entry, ToStageId = decision, IsDefault = true, Priority = 1 }] : []
        });
        workflow = await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
        var offering = await CreateAvailableOfferingAsync(workflow.Id);
        var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(offering.AdmissionCycleId));
        await ConfigureOfferingReadinessAsync(offering, cycle.AcademicYearId);
        var applicant = await CreateCertificationApplicantAsync(name, true, true, null);
        var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
        (await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
        var policy = await GetRequiredService<ICollectionPolicyAppService>().CreateAsync(new()
        {
            AcademicYearId = cycle.AcademicYearId, PolicyName = "Admission counter policy", PolicyCode = $"COL-{Guid.NewGuid():N}"[..20],
            Status = "Active", EnableLateFee = false, AllowOnlineCollection = true
        });
        // Create publishes a version but its return projection precedes the version write.
        // Read the canonical current version before attaching immutable policy evidence.
        policy = await GetRequiredService<ICollectionPolicyAppService>().GetAsync(policy.Id);
        var bank = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BankAccount, Guid>>().InsertAsync(
            new BankAccount(Guid.NewGuid(), cycle.AcademicYearId, "QA School Bank", "Test branch", null, "QA School",
                BankAccountType.Current, "1234567890", "Admission counter", "TEST0000001", null, tenantId: TestTenantId), true));
        var (structure, termId) = await WithUnitOfWorkAsync(async () =>
        {
            var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(cycle.AcademicYearId);
            var income = await FeeHeadAccountingTestData.CreateIncomeCategoryAsync(
                GetRequiredService<IRepository<AccountHead, Guid>>(), GetRequiredService<IRepository<TransactionCategory, Guid>>(), year.Id, TestTenantId, "Admission counter");
            var category = await GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId, year.Id, "Admission fees", null), true);
            var head = await GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId, year.Id, category.Id,
                "Admission confirmation", FeeHeadModule.Finance, false, null, income), true);
            var term = new PaymentTerm(Guid.NewGuid(), TestTenantId, year.Id, offering.BoardId, "Admission payment", $"AD-{Guid.NewGuid():N}"[..20], null,
                false, HolidayShiftingMethod.None, DemandGenerationRule.SpecificDate, null, InitialDemandStatus.Published);
            var version = new PaymentTermVersion(Guid.NewGuid(), TestTenantId, term.Id, 1, PaymentTermVersionStatus.Draft,
                HolidayShiftingMethod.None, DemandGenerationRule.SpecificDate, null, null, null);
            version.SetInstallments([new(Guid.NewGuid(), version.Id, 1, "Admission installment", year.StartDate.Date, year.EndDate.Date,
                year.StartDate.Date, year.StartDate.Date.AddDays(10), 100m, true, false, allowPartialOverride: true)]);
            version.Publish(year.StartDate, TestUserId);
            term.AddVersion(version);
            await GetRequiredService<IRepository<PaymentTerm, Guid>>().InsertAsync(term, true);
            var metadata = await GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId, year.Id,
                offering.BoardId, offering.MediumId, null, "Admission counter metadata"), true);
            var value = new FeeStructure(Guid.NewGuid(), TestTenantId, metadata.Id, offering.StandardId, "Admission counter plan");
            var detail = new FeeStructureDetail(Guid.NewGuid(), TestTenantId, value.Id, head.Id, amount, oneTime, false, null, null);
            detail.AttachPaymentTerm(term.Id, version.Id);
            value.AddDetail(detail);
            value.AttachCollectionPolicy(policy.Id, policy.CurrentVersionId!.Value);
            value.Lock();
            await GetRequiredService<IRepository<FeeStructure, Guid>>().InsertAsync(value, true);
            return (value, term.Id);
        });
        if (paymentStage)
        {
            var selections = GetRequiredService<IAdmissionFeeSelectionAppService>();
            var selection = await selections.GetAsync(application.Id);
            await selections.SaveAsync(application.Id, new() { StructureId = structure.Id, ConcurrencyStamp = selection.ConcurrencyStamp });
        }
        return new(await _applicationAppService.GetAsync(application.Id), offering, cycle.AcademicYearId, structure.Id, termId, bank.Id);
    }

    private async Task CompleteCollectibleDecisionAsync(Guid applicationId)
    {
        var runtime = await _applicationAppService.GetWorkflowAsync(applicationId);
        var stage = runtime.Stages.SingleOrDefault(s => s.Id == runtime.CurrentStageInstanceId);
        if (stage?.StageType == AdmissionWorkflowStageType.Payment)
        {
            var defer = !(await GetRequiredService<IAdmissionFeeSelectionAppService>().GetAsync(applicationId)).HasRecordedFeePayment;
            var application = await _applicationAppService.GetAsync(applicationId);
            await _applicationAppService.AdvanceWorkflowAsync(applicationId, new()
            {
                ExpectedCurrentStageInstanceId = stage.Id, ConcurrencyStamp = application.ConcurrencyStamp,
                IdempotencyKey = $"finance-test-payment-stage-{stage.Id:N}", DeferFeeCollection = defer,
                Reason = defer ? "Parent will complete collection in Onboarding." : null
            });
        }
        await CompleteOnboardingDecisionAsync(applicationId);
    }
    private async Task<StudentOnboardingCaseDto> ActivateCollectibleOnboardingAsync(CollectibleAdmissionFixture fixture, AdmissionFinalizationResultDto finalized)
    {
        var service = GetRequiredService<IStudentOnboardingAppService>();
        var division = await WithUnitOfWorkAsync(async () =>
        {
            var value = await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, $"Collect-{Guid.NewGuid():N}"[..18], 40);
            return await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(value, true);
        });
        var item = await service.GetAsync(finalized.OnboardingCaseId);
        item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
        item = await service.AssignRollNumberAsync(item.Id, new() { RollNumber = 1, RollAssignmentMode = "automatic", ConcurrencyStamp = item.ConcurrencyStamp });
        var series = (await service.GetAvailableIdentifierSeriesAsync(item.Id)).Single();
        item = await service.AssignGrNumberAsync(item.Id, new() { NumberSeriesId = series.Id, ConcurrencyStamp = item.ConcurrencyStamp });
        item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
        var selection = await service.SaveFeeStructureAsync(item.Id, new() { StructureId = fixture.StructureId, ConcurrencyStamp = item.ConcurrencyStamp });
        selection.OnboardingConfirmed.ShouldBeTrue();
        item = await service.GetAsync(item.Id);
        return await service.ActivateAsync(item.Id, new() { IdempotencyKey = "finance-handoff-activate", ConcurrencyStamp = item.ConcurrencyStamp });
    }

    private Task<FeeStructure> CreateCollectibleRevisionAsync(CollectibleAdmissionFixture fixture, decimal? amount) =>
        WithUnitOfWorkAsync(async () =>
        {
            var repository = GetRequiredService<IRepository<FeeStructure, Guid>>();
            var original = await repository.GetAsync(fixture.StructureId);
            var detail = await GetRequiredService<IRepository<FeeStructureDetail, Guid>>().GetAsync(d => d.FeeStructureId == original.Id);
            var revision = new FeeStructure(Guid.NewGuid(), TestTenantId, original.FeeStructureMetadataId, original.StandardId, original.Name);
            revision.SetRevision(original, original.RevisionNumber + 1);
            revision.AttachCollectionPolicy(original.CollectionPolicyId, original.CollectionPolicyVersionId);
            var revisedDetail = new FeeStructureDetail(Guid.NewGuid(), TestTenantId, revision.Id, detail.FeeHeadId,
                amount, true, false, null, null, isVariable: !amount.HasValue);
            revisedDetail.AttachPaymentTerm(detail.PaymentTermId!.Value, detail.PaymentTermVersionId!.Value);
            revision.AddDetail(revisedDetail);
            revision.Lock();
            return await repository.InsertAsync(revision, true);
        });

    private sealed record CollectibleAdmissionFixture(AdmissionApplicationDto Application, AdmissionOffering Offering, Guid YearId, Guid StructureId, Guid TermId, Guid BankId);
}
