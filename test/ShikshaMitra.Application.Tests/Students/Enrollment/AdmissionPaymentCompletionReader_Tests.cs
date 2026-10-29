using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionPaymentCompletionReader_Tests
{
	[Theory]
	[InlineData("partial", 50, 150, false, true)]
	[InlineData("full", 50, 150, false, true)]
	[InlineData("full", 200, 0, true, true)]
	[InlineData("partial", 0, 0, true, true)]
	[InlineData("full", 0, 0, true, true)]
	[InlineData("full", 0, 0, false, false)]
	[InlineData("partial", 0, 200, false, false)]
	public async Task Uses_Finance_confirmed_payments_or_verified_settlement_and_never_missing_zero(string rule, int confirmed, int outstanding, bool cleared, bool allowed)
	{
		var (application, stage, definition) = Fixture(rule);
		var finance = Substitute.For<IAdmissionFinanceIntegration>();
		finance.IsSettledForOnboardingAsync(application.Id).Returns(cleared);
		var collections = Substitute.For<IAdmissionFeeCollectionAppService>();
		collections.GetSummaryAsync(application.Id).Returns(new AdmissionFeeCollectionDto
		{ AccountId = cleared || outstanding > 0 ? Guid.NewGuid() : null, ConfirmedPaidAmount = confirmed, OutstandingAmount = outstanding });
		var state = await Reader(finance, collections).ReadAsync(application, stage, true);
		state.CanComplete.ShouldBe(allowed);
		if (cleared) collections.ReceivedCalls().ShouldBeEmpty();
		collections.ReceivedCalls().ShouldAllBe(call => call.GetMethodInfo().Name == "GetSummaryAsync");
		finance.ReceivedCalls().ShouldAllBe(call => call.GetMethodInfo().Name == "IsSettledForOnboardingAsync");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Permission_and_pending_or_reversed_evidence_fail_closed_without_blocking_allowed_deferral(bool readAllowed)
	{
		var (application, stage, definition) = Fixture("partial");
		application.RecordFinanceFeePayment(Guid.NewGuid()); // historical flag alone is never settlement
		var finance = Substitute.For<IAdmissionFinanceIntegration>();
		var collections = Substitute.For<IAdmissionFeeCollectionAppService>();
		collections.GetSummaryAsync(application.Id).Returns(new AdmissionFeeCollectionDto
		{ AccountId = Guid.NewGuid(), HasPaymentEvidence = true, ConfirmedPaidAmount = 0m, PendingVerificationAmount = 50m, OutstandingAmount = 150m });
		var state = await Reader(finance, collections, readAllowed).ReadAsync(application, stage, true);
		state.CanComplete.ShouldBeFalse(); state.CanDeferCollection.ShouldBeTrue(); state.Blocker.ShouldNotBeNull();
		if (!readAllowed) collections.ReceivedCalls().ShouldBeEmpty();
	}

	[Theory]
	[InlineData(false, true)]
	[InlineData(true, false)]
	public async Task Retired_deferral_settings_are_ignored_but_inactive_stages_remain_blocked(bool later, bool active)
	{
		var (application, stage, definition) = Fixture("full", later);
		var finance = Substitute.For<IAdmissionFinanceIntegration>();
		finance.IsSettledForOnboardingAsync(application.Id).Returns(true);
		var result = await Reader(finance, Substitute.For<IAdmissionFeeCollectionAppService>()).ReadAsync(application, stage, active);
		result.CanDeferCollection.ShouldBe(active); result.CanComplete.ShouldBe(active);
	}

	[Fact]
	public async Task A_missing_or_wrong_stage_selection_never_reads_Finance_or_passes()
	{
		var (application, stage, definition) = Fixture("full", selection: false);
		var finance = Substitute.For<IAdmissionFinanceIntegration>();
		var collections = Substitute.For<IAdmissionFeeCollectionAppService>();
		var result = await Reader(finance, collections).ReadAsync(application, stage, true);
		result.CanComplete.ShouldBeFalse(); result.Status.ShouldBe("feeStructureRequired");
		finance.ReceivedCalls().ShouldBeEmpty(); collections.ReceivedCalls().ShouldBeEmpty();
	}

	[Fact]
	public async Task Finance_configuration_failure_keeps_selection_readable_but_blocks_completion()
	{
		var (application, stage, definition) = Fixture("partial");
		var finance = Substitute.For<IAdmissionFinanceIntegration>();
		finance.IsSettledForOnboardingAsync(application.Id).Returns(Task.FromException<bool>(new BusinessException("Finance:NotPrepared")));
		var result = await Reader(finance, Substitute.For<IAdmissionFeeCollectionAppService>()).ReadAsync(application, stage, true);
		result.CanComplete.ShouldBeFalse(); result.CanDeferCollection.ShouldBeTrue(); result.Blocker.ShouldContain("could not verify");
	}

	[Fact]
	public async Task A_later_Payment_stage_reuses_the_validated_original_selection_owner()
	{
		var (application, first, definition) = Fixture("full");
		var next = new AdmissionApplicationStageInstance(Guid.NewGuid(), null, first.WorkflowInstanceId,
			definition.Id, "PAY2", "Second payment", AdmissionWorkflowStageType.Payment, 1, true);
		application.SetCurrentStage(next.Id);
		var finance = Substitute.For<IAdmissionFinanceIntegration>();
		finance.IsSettledForOnboardingAsync(application.Id).Returns(true);
		var result = await Reader(finance, Substitute.For<IAdmissionFeeCollectionAppService>())
			.ReadAsync(application, next, true, first.Id);
		result.CanComplete.ShouldBeTrue();
	}

	private static AdmissionPaymentCompletionReader Reader(IAdmissionFinanceIntegration finance, IAdmissionFeeCollectionAppService collections, bool readAllowed = true)
	{
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(Task.FromResult(readAllowed ? AuthorizationResult.Success() : AuthorizationResult.Failed()));
		return new(
			Substitute.For<IRepository<AdmissionApplicationStageInstance, Guid>>(), finance, collections)
		{ LazyServiceProvider = new AbpLazyServiceProvider(new ServiceCollection().AddSingleton<IAuthorizationService>(authorization).BuildServiceProvider()) };
	}

	private static (AdmissionApplication, AdmissionApplicationStageInstance, AdmissionWorkflowStageDefinition) Fixture(string rule, bool later = true, bool selection = true)
	{
		var definition = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), Guid.NewGuid(), "PAY", "Payment", AdmissionWorkflowStageType.Payment,
			0, true, false, description: null,
			configurationJson: JsonSerializer.Serialize(new { paymentCompletion = new { version = 1, paymentRule = rule, allowCollectionLater = later } }),
			isEntryStage: true, isTerminalStage: false);
		var application = new AdmissionApplication(Guid.NewGuid(), null, "QA", Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), definition.WorkflowDefinitionId, Guid.NewGuid());
		var stage = new AdmissionApplicationStageInstance(Guid.NewGuid(), null, Guid.NewGuid(), definition.Id, "PAY", "Payment", AdmissionWorkflowStageType.Payment, 0, true);
		application.SetCurrentStage(stage.Id);
		if (selection) application.SaveFeeSelection(new(Guid.NewGuid(), "Class fees", 1, Guid.NewGuid(), Guid.NewGuid(), stage.Id, "INR",
			[new(Guid.NewGuid(), "Tuition", 200m, false, true, null, null)], Guid.NewGuid(), DateTimeOffset.UtcNow), false);
		return (application, stage, definition);
	}
}
