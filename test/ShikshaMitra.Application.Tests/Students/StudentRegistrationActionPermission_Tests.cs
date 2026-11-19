using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentRegistrationActionPermission_Tests
{
	[Theory]
	[InlineData(StudentLifeCycleStatusEnum.RegistrationFormIssued, false)]
	[InlineData(StudentLifeCycleStatusEnum.Registration, false)]
	[InlineData(StudentLifeCycleStatusEnum.EntranceExam, false)]
	[InlineData(StudentLifeCycleStatusEnum.Admission, false)]
	[InlineData(StudentLifeCycleStatusEnum.RegistrationFormIssued, true)]
	[InlineData(StudentLifeCycleStatusEnum.Registration, true)]
	[InlineData(StudentLifeCycleStatusEnum.EntranceExam, true)]
	[InlineData(StudentLifeCycleStatusEnum.Admission, true)]
	public async Task Registration_section_access_requires_the_matching_application_action(StudentLifeCycleStatusEnum status, bool writing)
	{
		var fixture = CreateFixture(status);
		var permission = writing ? "Students.Admissions.Applications.Add" : "Students.Admissions.Applications.View";
		fixture.Authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), permission)
			.Returns(Task.FromResult(AuthorizationResult.Failed()));
		var denied = await Should.ThrowAsync<AbpAuthorizationException>(() => fixture.Service.ReadOrWriteAsync(fixture.Student.Id, writing));
		denied.Data["PolicyName"].ShouldBe(permission);
		fixture.Authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), permission)
			.Returns(Task.FromResult(AuthorizationResult.Success()));
		(await fixture.Service.ReadOrWriteAsync(fixture.Student.Id, writing)).Id.ShouldBe(fixture.Student.Id);
	}

	[Theory]
	[InlineData(StudentLifeCycleStatusEnum.Enrolled)]
	[InlineData(StudentLifeCycleStatusEnum.InActive)]
	[InlineData(StudentLifeCycleStatusEnum.Left)]
	public async Task Operational_sections_require_record_actions_even_when_admissions_actions_are_granted(StudentLifeCycleStatusEnum status)
	{
		var fixture = CreateFixture(status);
		fixture.Permissions.IsGrantedAsync("Students.Records.StudentList.View").Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => fixture.Service.ReadOrWriteAsync(fixture.Student.Id, false));
		await fixture.Permissions.Received(1).IsGrantedAsync("Students.Records.StudentList.View");
		fixture.Authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), "Students.Records.StudentList.Add")
			.Returns(Task.FromResult(AuthorizationResult.Failed()));
		var denied = await Should.ThrowAsync<AbpAuthorizationException>(() => fixture.Service.ReadOrWriteAsync(fixture.Student.Id, true));
		denied.Data["PolicyName"].ShouldBe("Students.Records.StudentList.Add");
		await fixture.Authorization.DidNotReceive().AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), "Students.Admissions.Applications.View");
		await fixture.Authorization.DidNotReceive().AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), "Students.Admissions.Applications.Add");
	}

	private static RegistrationFixture CreateFixture(StudentLifeCycleStatusEnum status)
	{
		var tenantId = Guid.NewGuid();
		var student = new Student(Guid.NewGuid(), tenantId, "Scoped", null, "Student", Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), status: status);
		var repository = Substitute.For<IStudentRepository>();
		repository.GetRegistrationAggregateAsync(student.Id, Arg.Any<CancellationToken>()).Returns(student);
		var userScope = Substitute.For<IUserScopeAccessService>();
		userScope.GetCurrentUserScopeAsync().Returns(new UserScopeContext { IsAdmin = true, TenantId = tenantId });
		var scope = new StudentScopeAccessService(userScope, repository, null!, null!, null!, null!, null!, null!);
		var service = new RegistrationAccessHarness(repository, new EntityValidationService(null!, null!, null!), scope);
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(Task.FromResult(AuthorizationResult.Success()));
		var permissions = Substitute.For<IPermissionChecker>();
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(tenantId);
		var services = new ServiceCollection();
		services.AddSingleton(tenant);
		services.AddSingleton<IAuthorizationService>(authorization);
		services.AddSingleton(new StudentRecordReadGuard(null!, permissions));
		services.AddSingleton(new StudentRecordWriteGuard(new StudentRecordApplyContext(), null!));
		var provider = new AbpLazyServiceProvider(services.BuildServiceProvider());
		scope.LazyServiceProvider = provider;
		service.LazyServiceProvider = provider;
		return new RegistrationFixture(service, student, authorization, permissions);
	}

	private sealed record RegistrationFixture(RegistrationAccessHarness Service, Student Student,
		IAbpAuthorizationService Authorization, IPermissionChecker Permissions);

	[DisableConventionalRegistration]
	private sealed class RegistrationAccessHarness(IStudentRepository repository, EntityValidationService validation, StudentScopeAccessService scope)
		: StudentRegistrationBaseAppService(repository, validation, scope)
	{
		public Task<Student> ReadOrWriteAsync(Guid studentId, bool writing) => GetScopedRegistrationStudentAsync(studentId,
			writing ? "UpdateGeneralInfoAsync" : "GetRegistrationGeneralDetailsByIdAsync", "Test:InvalidStatus",
			includeStudentRecords: true, forProfileWrite: writing);
	}
}
