using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using Shouldly;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentActionPermission_Tests
{
	public static IEnumerable<object[]> ProtectedOperations()
	{
		yield return Case<StudentAppService>("GetStudentsByDivisionAsync", "Students.Records.StudentList.View");
		foreach (var operation in new[] { "GetStudentsByDivisionAsync", "GetPhotoAsync", "GetDashboardSummaryAsync", "GetAttendanceDashboardSummaryAsync", "GetStatusHistoryAsync", "GetStudentsByScopeAsync" })
			yield return Case<StudentListAppService>(operation, "Students.Records.StudentList.View");
		foreach (var operation in new[] { "UploadPhotoAsync", "ChangeStudentStatusAsync" })
			yield return Case<StudentListAppService>(operation, "Students.Records.StudentList.Add");
		foreach (var operation in new[] { "GetRegistrationFormsByMediumAsync", "GetRegistrationsByMediumAsync", "SearchStudentsByNameAsync" })
			yield return Case<StudentRegistrationQueryAppService>(operation, "Students.Admissions.Applications.View");
		yield return Case<StudentGeneralInfoAppService>("CreateDirectRegistrationAsync", "Students.Admissions.Applications.Add");
		yield return Case<StudentOnboardingAppService>("SetIdentifierLockAsync", "Students.Onboarding.Add");
		yield return Case<StudentOnboardingAppService>("DeleteSettingsAsync", "Students.Onboarding.Delete");
		yield return Case<StudentCredentialEmailAppService>("QueueAsync", "Students.Onboarding.Add");
		yield return Case<StudentRecordTransitionAppService>("CancelAsync", "Students.Records.Transitions.Add");
	}

	[Theory]
	[MemberData(nameof(ProtectedOperations))]
	public async Task Direct_calls_reject_missing_action_before_reading_records_or_writing(Type serviceType, string methodName, string permission)
	{
		var service = (ApplicationService)Activator.CreateInstance(serviceType,
			serviceType.GetConstructors().Single().GetParameters().Select(_ => (object?)null).ToArray())!;
		var authorization = Substitute.For<IAbpAuthorizationService>();
		authorization.CurrentPrincipal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
		authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
			.Returns(Task.FromResult(AuthorizationResult.Failed()));
		var services = new ServiceCollection();
		services.AddSingleton<IAuthorizationService>(authorization);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services.BuildServiceProvider());
		var method = serviceType.GetMethod(methodName)!;
		var args = method.GetParameters().Select(Argument).ToArray();
		var exception = await Should.ThrowAsync<AbpAuthorizationException>(async () =>
			await (Task)method.Invoke(service, args)!);
		exception.Data["PolicyName"].ShouldBe(permission);
	}

	private static object[] Case<T>(string method, string permission) => [typeof(T), method, permission];
	private static object? Argument(ParameterInfo parameter) => parameter.HasDefaultValue ? parameter.DefaultValue :
		parameter.ParameterType == typeof(Guid) ? Guid.NewGuid() :
		parameter.ParameterType == typeof(string) ? "Student" : Activator.CreateInstance(parameter.ParameterType);
}
