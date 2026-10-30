using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalaryStructure;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Payroll.Compensation;
using Volo.Abp.Domain.Repositories;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Xunit;
using H = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR;

namespace ShikshaMitra.HR.Payroll;

/// <summary>Unproxied calls enforce existing controller authority before reading or changing payroll data.</summary>
public class PayrollSettingsActionAuthorization_Tests
{
	[Theory]
	[InlineData("get", H.PayrollManagement_SettingsPfRule.View)]
	[InlineData("list", H.PayrollManagement_SettingsPfRule.View)]
	[InlineData("filtered", H.PayrollManagement_SettingsPfRule.View)]
	[InlineData("create", H.PayrollManagement_SettingsPfRule.Add)]
	[InlineData("update", H.PayrollManagement_SettingsPfRule.Add)]
	[InlineData("delete", H.PayrollManagement_SettingsPfRule.Delete)]
	public async Task Pf_direct_calls_reject_exact_denied_action_before_repository_access(string operation, string permission)
	{
		var service = new ProvidentFundAppService(null!, null!) { LazyServiceProvider = Authorization(null) };
		Func<Task> call = operation switch
		{
			"get" => () => service.GetAsync(Guid.NewGuid()),
			"list" => () => service.GetListAsync(new()),
			"filtered" => () => service.GetFilteredListAsync(0, 20, null),
			"create" => () => service.CreateAsync(new()),
			"update" => () => service.UpdateAsync(Guid.NewGuid(), new()),
			_ => () => service.DeleteAsync(Guid.NewGuid())
		};
		(await Should.ThrowAsync<AbpAuthorizationException>(call)).Data["PolicyName"].ShouldBe(permission);
	}

	[Theory]
	[InlineData("get", H.PayrollManagement_SettingsProfessionalTax.View)]
	[InlineData("list", H.PayrollManagement_SettingsProfessionalTax.View)]
	[InlineData("create", H.PayrollManagement_SettingsProfessionalTax.Add)]
	[InlineData("update", H.PayrollManagement_SettingsProfessionalTax.Add)]
	[InlineData("delete", H.PayrollManagement_SettingsProfessionalTax.Delete)]
	public async Task Pt_direct_calls_reject_exact_denied_action_before_repository_access(string operation, string permission)
	{
		var service = new ProfessionalTaxAppService(null!, null!) { LazyServiceProvider = Authorization(null) };
		Func<Task> call = operation switch
		{
			"get" => () => service.GetAsync(Guid.NewGuid()),
			"list" => () => service.GetListAsync(new()),
			"create" => () => service.CreateAsync(new()),
			"update" => () => service.UpdateAsync(Guid.NewGuid(), new()),
			_ => () => service.DeleteAsync(Guid.NewGuid())
		};
		(await Should.ThrowAsync<AbpAuthorizationException>(call)).Data["PolicyName"].ShouldBe(permission);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Add_only_reaches_existing_input_validation_without_requiring_view(bool pf, bool update)
	{
		var permission = pf ? H.PayrollManagement_SettingsPfRule.Add : H.PayrollManagement_SettingsProfessionalTax.Add;
		var checkedPolicies = new List<string>();
		var provider = Authorization(permission, checkedPolicies);
		Func<Task> call;
		if (pf)
		{
			var service = new ProvidentFundAppService(null!, null!) { LazyServiceProvider = provider };
			call = update ? () => service.UpdateAsync(Guid.NewGuid(), null!) : () => service.CreateAsync(null!);
		}
		else
		{
			var service = new ProfessionalTaxAppService(null!, null!) { LazyServiceProvider = provider };
			call = update ? () => service.UpdateAsync(Guid.NewGuid(), null!) : () => service.CreateAsync(null!);
		}
		await Should.ThrowAsync<ArgumentNullException>(call);
		checkedPolicies.ShouldBe(new[] { permission });
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Salary_structure_direct_calls_reject_action_before_employee_reads(bool update)
	{
		var service = new EmployeeSalaryStructureAppService(null!, null!, null!, null!) { LazyServiceProvider = Authorization(null) };
		Func<Task> call = update ? () => service.UpdateAsync(Guid.NewGuid(), new()) : () => service.GetAsync(Guid.NewGuid());
		(await Should.ThrowAsync<AbpAuthorizationException>(call)).Data["PolicyName"].ShouldBe(update
			? H.PayrollManagement_PayrollStructure.Add : H.Employee_EmployeeList.View);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Salary_structure_direct_calls_keep_controller_scope_authority_before_repository_reads(bool update)
	{
		var employee = Guid.NewGuid();
		var access = Substitute.For<IEmployeeAuthorizationService>();
		var failure = new BusinessException("synthetic-scope-denied");
		access.CheckEmployeeAccessAsync(employee, true).Returns(Task.FromException(failure));
		access.CheckEmployeeModifyAccessAsync(employee).Returns(Task.FromException(failure));
		var permission = update ? H.PayrollManagement_PayrollStructure.Add : H.Employee_EmployeeList.View;
		var service = new EmployeeSalaryStructureAppService(null!, null!, null!, access) { LazyServiceProvider = Authorization(permission) };
		Func<Task> call = update ? () => service.UpdateAsync(employee, new()) : () => service.GetAsync(employee);
		(await Should.ThrowAsync<BusinessException>(call)).ShouldBeSameAs(failure);
		if (update) await access.Received(1).CheckEmployeeModifyAccessAsync(employee);
		else await access.Received(1).CheckEmployeeAccessAsync(employee, true);
	}

	private static AbpLazyServiceProvider Authorization(string? allowed, List<string>? checkedPolicies = null)
	{
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>()).Returns(call =>
		{
			var policy = call.ArgAt<string>(2);
			checkedPolicies?.Add(policy);
			return Task.FromResult(policy == allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
		});
		return new AbpLazyServiceProvider(new ServiceCollection().AddSingleton<IAuthorizationService>(authorization).BuildServiceProvider());
	}

	[Theory]
	[InlineData("list", H.PayrollManagement_SettingsSalaryTemplates.View)]
	[InlineData("get", H.PayrollManagement_SettingsSalaryTemplates.View)]
	[InlineData("versions", H.PayrollManagement_SettingsSalaryTemplates.View)]
	[InlineData("catalog", H.PayrollManagement_SettingsSalaryTemplates.View)]
	[InlineData("active", H.PayrollManagement_CompensationRevisions.Manage)]
	[InlineData("create", H.PayrollManagement_SettingsSalaryTemplates.Add)]
	[InlineData("update", H.PayrollManagement_SettingsSalaryTemplates.Edit)]
	[InlineData("default", H.PayrollManagement_SettingsSalaryTemplates.SetDefault)]
	[InlineData("archive", H.PayrollManagement_SettingsSalaryTemplates.Archive)]
	[InlineData("calculate", H.PayrollManagement_CompensationRevisions.Manage)]
	[InlineData("preview", H.PayrollManagement_SettingsSalaryTemplates.View)]
	public async Task Template_direct_calls_reject_exact_controller_action_before_repository_access(string operation, string permission)
	{
		var service = new SalaryTemplateAppService(null!, null!, null!, null!, null!, null!, null!, null!, null!) { LazyServiceProvider = Authorization(null) };
		Func<Task> call = operation switch
		{
			"list" => () => service.GetListAsync(), "get" => () => service.GetAsync(Guid.NewGuid()),
			"versions" => () => service.GetVersionsAsync(Guid.NewGuid()), "catalog" => () => service.GetComponentCatalogAsync(),
			"active" => () => service.GetActiveListAsync(), "create" => () => service.CreateAsync(new()),
			"update" => () => service.UpdateAsync(Guid.NewGuid(), new()), "default" => () => service.SetDefaultAsync(Guid.NewGuid()),
			"archive" => () => service.ArchiveAsync(Guid.NewGuid(), new()), "calculate" => () => service.CalculateAsync(Guid.NewGuid(), new()),
			_ => () => service.PreviewAsync(new())
		};
		(await Should.ThrowAsync<AbpAuthorizationException>(call)).Data["PolicyName"].ShouldBe(permission);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Compensation_direct_reads_reject_missing_read_grants_before_employee_or_salary_access(bool effective)
	{
		var service = CompensationService(null!, null!, Authorization(null));
		await Should.ThrowAsync<AbpAuthorizationException>(() => ReadCompensation(service, Guid.NewGuid(), effective));
	}

	[Theory]
	[InlineData(false, H.PayrollManagement_CompensationRevisions.View)]
	[InlineData(true, H.PayrollManagement_CompensationRevisions.View)]
	[InlineData(false, H.PayrollManagement_CompensationRevisions.Manage)]
	[InlineData(true, H.PayrollManagement_CompensationRevisions.Manage)]
	public async Task Compensation_direct_reads_require_scope_with_either_independent_read_grant(bool effective, string permission)
	{
		var employee = Guid.NewGuid();
		var access = Substitute.For<IEmployeeAuthorizationService>();
		var failure = new BusinessException("synthetic-compensation-scope-denied");
		access.CheckEmployeeScopeAccessAsync(employee).Returns(Task.FromException(failure));
		var service = CompensationService(null!, access, Authorization(permission));
		(await Should.ThrowAsync<BusinessException>(() => ReadCompensation(service, employee, effective))).ShouldBeSameAs(failure);
		await access.Received(1).CheckEmployeeScopeAccessAsync(employee);
	}

	[Theory]
	[InlineData(false, H.PayrollManagement_CompensationRevisions.View)]
	[InlineData(true, H.PayrollManagement_CompensationRevisions.View)]
	[InlineData(false, H.PayrollManagement_CompensationRevisions.Manage)]
	[InlineData(true, H.PayrollManagement_CompensationRevisions.Manage)]
	public async Task Compensation_direct_reads_allow_scoped_grant_and_recheck_revocation_before_next_read(bool effective, string permission)
	{
		var employee = Guid.NewGuid();
		var access = Substitute.For<IEmployeeAuthorizationService>();
		var repository = Substitute.For<IRepository<EmployeeCompensationRevision, Guid>>();
		var reached = new InvalidOperationException("synthetic-repository-boundary");
		repository.GetQueryableAsync().Returns(Task.FromException<System.Linq.IQueryable<EmployeeCompensationRevision>>(reached));
		var provider = Authorization(permission);
		var service = CompensationService(repository, access, provider);
		(await Should.ThrowAsync<InvalidOperationException>(() => ReadCompensation(service, employee, effective))).ShouldBeSameAs(reached);
		await access.Received(1).CheckEmployeeScopeAccessAsync(employee);
		var authorization = provider.LazyGetRequiredService<IAuthorizationService>();
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>()).Returns(Task.FromResult(AuthorizationResult.Failed()));
		await Should.ThrowAsync<AbpAuthorizationException>(() => ReadCompensation(service, employee, effective));
		await access.Received(1).CheckEmployeeScopeAccessAsync(employee);
		await repository.Received(1).GetQueryableAsync();
	}

	private static CompensationRevisionAppService CompensationService(IRepository<EmployeeCompensationRevision, Guid> repository,
		IEmployeeAuthorizationService access, AbpLazyServiceProvider provider) =>
		new(repository, null!, null!, null!, null!, null!, null!, null!, null!, null!, access, null!, null!) { LazyServiceProvider = provider };

	private static Task ReadCompensation(CompensationRevisionAppService service, Guid employee, bool effective) =>
		effective ? service.GetEffectiveAsync(employee, new DateTime(2026, 10, 1)) : service.GetListAsync(employee);
}
