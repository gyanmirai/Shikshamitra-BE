using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.HR.Employees.FamilyInfo;
using Shouldly;
using Volo.Abp.Authorization;
using Xunit;

namespace ShikshaMitra.HR.Employees;

public class EmployeeFamilyInfoAccess_Tests
{
	[Fact]
	public async Task Direct_Read_Checks_Employee_Access_Before_Reading_Family_Records()
	{
		var authorization = Substitute.For<IEmployeeAuthorizationService>();
		var employeeId = Guid.NewGuid();
		authorization.CheckEmployeeAccessAsync(employeeId)
			.Returns(Task.FromException(new AbpAuthorizationException("Denied employee scope")));
		var employeeRepository = Substitute.For<IEmployeeRepository>();
		var familyRepository = Substitute.For<IEmployeeFamilyMemberRepository>();
		var service = new EmployeeFamilyInfoAppService(employeeRepository, familyRepository, null!, authorization);

		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(employeeId));

		employeeRepository.ReceivedCalls().ShouldBeEmpty();
		familyRepository.ReceivedCalls().ShouldBeEmpty();
	}

	[Fact]
	public async Task Direct_Write_Checks_Employee_Access_Before_Updating_Family_Records()
	{
		var authorization = Substitute.For<IEmployeeAuthorizationService>();
		var employeeId = Guid.NewGuid();
		authorization.CheckEmployeeModifyAccessAsync(employeeId)
			.Returns(Task.FromException(new AbpAuthorizationException("Denied employee scope")));
		var service = new EmployeeFamilyInfoAppService(null!, null!, null!, authorization);

		await Should.ThrowAsync<AbpAuthorizationException>(() => service.UpsertAsync(employeeId, new FamilyInfoRequest()));
	}
}
