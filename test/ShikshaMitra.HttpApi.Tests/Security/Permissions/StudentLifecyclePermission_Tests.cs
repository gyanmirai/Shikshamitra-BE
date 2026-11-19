using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students.Records;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public class StudentLifecyclePermission_Tests
{
	[Fact]
	public void Student_controllers_and_services_reserve_Delete_for_actual_record_removal()
	{
		var assemblies = new[] { typeof(StudentAppService).Assembly,
			typeof(ShikshaMitra.HttpApi.Controllers.Students.Records.StudentRecordReportsController).Assembly };
		var removalGuards = assemblies.SelectMany(assembly => assembly.GetTypes())
			.Where(type => type.Namespace?.Contains("Student", StringComparison.Ordinal) == true)
			.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
			.SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>(true)
				.Where(attribute => attribute.Policy?.StartsWith("Students.", StringComparison.Ordinal) == true &&
					attribute.Policy.EndsWith(".Delete", StringComparison.Ordinal))
				.Select(attribute => (method, attribute.Policy))).ToArray();
		removalGuards.ShouldNotBeEmpty();
		foreach (var (method, policy) in removalGuards)
			(method.Name.StartsWith("Delete", StringComparison.Ordinal) || method.Name.StartsWith("Remove", StringComparison.Ordinal))
				.ShouldBeTrue($"{method.DeclaringType?.Name}.{method.Name} uses {policy}; lifecycle updates require Add.");
	}
}
