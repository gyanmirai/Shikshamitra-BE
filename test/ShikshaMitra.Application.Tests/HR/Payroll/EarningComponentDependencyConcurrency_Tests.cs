using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Payroll.Settings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EarningComponentDependencyConcurrency_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IEarningComponentAppService _earningComponents;
	private readonly ICurrentTenant _currentTenant;

	public EarningComponentDependencyConcurrency_Tests()
	{
		_earningComponents = GetRequiredService<IEarningComponentAppService>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Two_Transactions_With_Reciprocal_Updates_Should_Not_Commit_A_Cycle()
	{
		var suffix = Guid.NewGuid().ToString("N");
		var first = await CreateFixedAsync($"Concurrent Cycle A {suffix}");
		var second = await CreateFixedAsync($"Concurrent Cycle B {suffix}");
		var firstUpdate = BuildPercentageUpdate(first, second.Id);
		var secondUpdate = BuildPercentageUpdate(second, first.Id);
		firstUpdate.PreviewHash =
			(await _earningComponents.PreviewUpdateImpactAsync(first.Id, firstUpdate)).PreviewHash;
		secondUpdate.PreviewHash =
			(await _earningComponents.PreviewUpdateImpactAsync(second.Id, secondUpdate)).PreviewHash;

		var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var firstMutation = RunConcurrentMutationAsync(
			gate.Task,
			service => service.UpdateAsync(first.Id, firstUpdate));
		var secondMutation = RunConcurrentMutationAsync(
			gate.Task,
			service => service.UpdateAsync(second.Id, secondUpdate));
		gate.SetResult(true);

		var results = await Task.WhenAll(firstMutation, secondMutation);

		results.Count(result => result.Succeeded).ShouldBe(1);
		var rejected = results.Single(result => !result.Succeeded);
		rejected.ErrorCode.ShouldBe(HRErrorCodes.PayrollComponentDependencyCycle);

		var storedFirst = await ReadComponentAsync(first.Id);
		var storedSecond = await ReadComponentAsync(second.Id);
		var isReciprocalCycle =
			storedFirst.SelectedEarningComponentId == storedSecond.Id &&
			storedSecond.SelectedEarningComponentId == storedFirst.Id;
		isReciprocalCycle.ShouldBeFalse();
	}

	[Fact]
	public async Task Two_Transactions_Creating_Dependency_And_Retiring_Base_Should_Preserve_Active_Target()
	{
		var suffix = Guid.NewGuid().ToString("N");
		var baseComponent = await CreateFixedAsync($"Concurrent Retire Base {suffix}");
		var dependentName = $"Concurrent Retire Dependent {suffix}";
		var dependentInput = new CreateEarningComponentDto
		{
			Name = dependentName,
			Description = "Concurrent dependency-retirement regression.",
			ComponentType = EarningComponentType.Variable,
			CalculationType = CalculationType.Percentage,
			Percentage = 10m,
			SelectedEarningComponentId = baseComponent.Id,
			DependOn = ComponentDependency.CustomComponent,
			VisibleInPayslip = true,
			DisplayOrder = 2
		};

		var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var createMutation = RunConcurrentMutationAsync(
			gate.Task,
			service => service.CreateAsync(dependentInput));
		var retireMutation = RunConcurrentMutationAsync(
			gate.Task,
			service => service.DeactivateAsync(baseComponent.Id));
		gate.SetResult(true);

		var results = await Task.WhenAll(createMutation, retireMutation);

		results.Count(result => result.Succeeded).ShouldBe(1);
		var rejected = results.Single(result => !result.Succeeded);
		(
			rejected.ErrorCode == HRErrorCodes.PayrollComponentDependencyInUse ||
			rejected.ErrorCode == HRErrorCodes.PayrollComponentDependencyTargetInvalid
		).ShouldBeTrue();

		var storedBase = await ReadComponentAsync(baseComponent.Id);
		var storedDependent = await FindComponentByNameAsync(dependentName);
		if (storedDependent is not null)
		{
			storedDependent.IsActive.ShouldBeTrue();
			storedDependent.SelectedEarningComponentId.ShouldBe(storedBase.Id);
			storedBase.IsActive.ShouldBeTrue();
		}
		else
		{
			storedBase.IsActive.ShouldBeFalse();
		}
	}

	[Fact]
	public async Task One_Transaction_Should_Reenter_The_Tenant_Graph_Lock_For_Sequential_Mutations()
	{
		using var scope = ServiceProvider.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		using var tenantScope = tenant.Change(_currentTenant.Id);
		var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<UnitOfWorkManager>();
		using var unitOfWork = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		var service = scope.ServiceProvider.GetRequiredService<IEarningComponentAppService>();
		var suffix = Guid.NewGuid().ToString("N");

		var first = await service.CreateAsync(BuildFixedInput($"Sequential Lock A {suffix}"));
		var second = await service.CreateAsync(BuildFixedInput($"Sequential Lock B {suffix}"));
		await unitOfWork.CompleteAsync();

		first.IsActive.ShouldBeTrue();
		second.IsActive.ShouldBeTrue();
	}

	private async Task<EarningComponentDto> CreateFixedAsync(string name) =>
		await _earningComponents.CreateAsync(BuildFixedInput(name));

	private static CreateEarningComponentDto BuildFixedInput(string name) =>
		new()
		{
			Name = name,
			Description = "Concurrent dependency-graph regression.",
			ComponentType = EarningComponentType.Fixed,
			CalculationType = CalculationType.Amount,
			Amount = 1000m,
			DependOn = ComponentDependency.None,
			VisibleInPayslip = true,
			DisplayOrder = 1
		};

	private static UpdateEarningComponentDto BuildPercentageUpdate(
		EarningComponentDto component,
		Guid targetId) =>
		new()
		{
			Name = component.Name,
			Description = component.Description,
			ComponentType = EarningComponentType.Variable,
			CalculationType = CalculationType.Percentage,
			Percentage = 10m,
			SelectedEarningComponentId = targetId,
			DependOn = ComponentDependency.CustomComponent,
			VisibleInPayslip = component.VisibleInPayslip,
			DisplayOrder = component.DisplayOrder
		};

	private async Task<MutationResult> RunConcurrentMutationAsync<T>(
		Task gate,
		Func<IEarningComponentAppService, Task<T>> mutation)
	{
		await gate;
		using var scope = ServiceProvider.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		using var tenantScope = tenant.Change(_currentTenant.Id);
		var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<UnitOfWorkManager>();
		using var unitOfWork = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		unitOfWork.Options.IsTransactional.ShouldBeTrue();
		var service = scope.ServiceProvider.GetRequiredService<IEarningComponentAppService>();

		try
		{
			await mutation(service);
			await unitOfWork.CompleteAsync();
			return MutationResult.Success();
		}
		catch (BusinessException exception)
		{
			return MutationResult.Rejected(exception.Code);
		}
	}

	private async Task<MutationResult> RunConcurrentMutationAsync(
		Task gate,
		Func<IEarningComponentAppService, Task> mutation)
	{
		return await RunConcurrentMutationAsync(
			gate,
			async service =>
			{
				await mutation(service);
				return true;
			});
	}

	private async Task<EarningComponent> ReadComponentAsync(Guid id)
	{
		using var scope = ServiceProvider.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		using var tenantScope = tenant.Change(_currentTenant.Id);
		var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		using var unitOfWork = unitOfWorkManager.Begin(requiresNew: true);
		var repository = scope.ServiceProvider.GetRequiredService<IEarningComponentRepository>();
		var component = await repository.GetAsync(id);
		await unitOfWork.CompleteAsync();
		return component;
	}

	private async Task<EarningComponent?> FindComponentByNameAsync(string name)
	{
		using var scope = ServiceProvider.CreateScope();
		var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		using var tenantScope = tenant.Change(_currentTenant.Id);
		var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		using var unitOfWork = unitOfWorkManager.Begin(requiresNew: true);
		var repository = scope.ServiceProvider.GetRequiredService<IEarningComponentRepository>();
		var component = await repository.FindByNameAsync(name);
		await unitOfWork.CompleteAsync();
		return component;
	}

	private sealed record MutationResult(bool Succeeded, string? ErrorCode)
	{
		public static MutationResult Success() => new(true, null);

		public static MutationResult Rejected(string? errorCode) => new(false, errorCode);
	}
}
