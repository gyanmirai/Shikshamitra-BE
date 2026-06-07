using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Status;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Groups;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class GroupManagementApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/groups";
	private static readonly DateOnly TestEffectiveDate = new(2026, 5, 31);

	public GroupManagementApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Group_With_Multiple_Active_Employees()
	{
		var employeeA = await EmployeeTestHelper.CreateEmployeeAsync(Client, "CalendarGroupA");
		var employeeB = await EmployeeTestHelper.CreateEmployeeAsync(Client, "CalendarGroupB");

		var created = await CreateEmployeeGroupAsync("CalendarMulti", new[]
		{
			employeeA.Id,
			employeeB.Id
		});

		created.EmployeeCount.ShouldBe(2);
		created.MemberCount.ShouldBe(2);
		created.NameBased.ShouldNotBeNull();
		created.NameBased.Employees.Select(employee => employee.Id).ShouldContain(employeeA.Id);
		created.NameBased.Employees.Select(employee => employee.Id).ShouldContain(employeeB.Id);
		created.NameBased.SelectedMembers.Count.ShouldBe(2);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Deduplicate_Duplicate_Employee_Members()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "CalendarDedupe");

		var created = await CreateEmployeeGroupAsync("CalendarDedupe", new[]
		{
			employee.Id,
			employee.Id
		});

		created.EmployeeCount.ShouldBe(1);
		created.MemberCount.ShouldBe(1);
		created.NameBased.ShouldNotBeNull();
		created.NameBased.SelectedMembers.Count.ShouldBe(1);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Allow_Same_Group_Name_In_Different_Academic_Years()
	{
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "CalendarSameName");
		var otherAcademicYearId = await CreateAcademicYearAsync();
		var groupName = TestDataHelper.UniqueName("CalendarSameName");

		var currentYearGroup = await CreateEmployeeGroupAsync(
			groupName,
			new[] { employee.Id },
			Factory.SeededAcademicYearId,
			useExactName: true);
		var otherYearGroup = await CreateEmployeeGroupAsync(
			groupName,
			new[] { employee.Id },
			otherAcademicYearId,
			useExactName: true);

		currentYearGroup.Name.ShouldBe(groupName);
		otherYearGroup.Name.ShouldBe(groupName);
		currentYearGroup.AcademicYearId.ShouldBe(Factory.SeededAcademicYearId);
		otherYearGroup.AcademicYearId.ShouldBe(otherAcademicYearId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Exclude_Inactive_Employees_From_Selection_Search()
	{
		var marker = $"InactiveCalendar{Guid.NewGuid():N}"[..24];
		var inactiveEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, marker);
		await SetEmployeeInactiveAsync(inactiveEmployee.Id);

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}/users-for-selection",
			new GetUsersForSelectionDto
			{
				AcademicYearId = Factory.SeededAcademicYearId,
				SearchTerm = marker,
				MaxResultCount = 20,
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<PagedResultDto<UserSelectionDto>>(JsonOptions);
		body.ShouldNotBeNull();
		body.Items.Any(item => item.MemberType == GroupMemberType.Employee && item.MemberId == inactiveEmployee.Id)
			.ShouldBeFalse();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Resolve_Rule_Based_Employees_As_Active_Only()
	{
		var marker = $"RuleCalendar{Guid.NewGuid():N}"[..20];
		var activeEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, marker);
		var inactiveEmployee = await EmployeeTestHelper.CreateEmployeeAsync(Client, marker);
		await SetEmployeeInactiveAsync(inactiveEmployee.Id);

		var input = new CreateUpdateGroupDto
		{
			Name = TestDataHelper.UniqueName("CalendarRule"),
			RuleBased = new RuleBasedSelectionDto
			{
				RoleIds = new List<Guid> { activeEmployee.RoleId },
				EmployeeFilters = new List<CreateEmployeeRuleDto>
				{
					new()
					{
						Field = EmployeeRuleField.Name,
						Operator = RuleOperator.Contains,
						Value = marker,
					}
				}
			}
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={Factory.SeededAcademicYearId}",
			input,
			JsonOptions);

		response.EnsureSuccessStatusCode();
		var created = await response.Content.ReadFromJsonAsync<GroupDto>(JsonOptions);
		created.ShouldNotBeNull();
		created.EmployeeCount.ShouldBe(1);
		created.MemberCount.ShouldBe(1);
	}

	private async Task<GroupDto> CreateEmployeeGroupAsync(
		string suffix,
		IEnumerable<Guid> employeeIds,
		Guid? academicYearId = null,
		bool useExactName = false)
	{
		var input = new CreateUpdateGroupDto
		{
			Name = useExactName ? suffix : TestDataHelper.UniqueName(suffix),
			NameBased = new NameBasedSelectionDto
			{
				SelectedMembers = employeeIds
					.Select(employeeId => new CreateGroupMemberDto
					{
						MemberType = GroupMemberType.Employee,
						MemberId = employeeId,
						UserId = null,
					})
					.ToList()
			}
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={academicYearId ?? Factory.SeededAcademicYearId}",
			input,
			JsonOptions);

		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<GroupDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to create group.");
	}

	private async Task<Guid> CreateAcademicYearAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>();
		var academicYearManager = scope.ServiceProvider.GetRequiredService<AcademicYearManager>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var suffix = Guid.NewGuid().ToString("N")[..8];

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var academicYear = await academicYearManager.CreateAsync(
				$"CalendarGroup-{suffix}",
				new DateTime(2035, 4, 1),
				new DateTime(2036, 3, 31));
			await academicYearRepository.InsertAsync(academicYear, autoSave: false);
			await uow.CompleteAsync();
			return academicYear.Id;
		}
	}

	private async Task SetEmployeeInactiveAsync(Guid employeeId)
	{
		var response = await Client.PutAsJsonAsync(
			$"/api/employees/{employeeId}/status",
			new EmployeeStatusUpdateRequest
			{
				EmployeeStatusType = 1,
				Remarks = "Calendar group test inactive",
				EffectiveDate = TestEffectiveDate,
			},
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}
}
