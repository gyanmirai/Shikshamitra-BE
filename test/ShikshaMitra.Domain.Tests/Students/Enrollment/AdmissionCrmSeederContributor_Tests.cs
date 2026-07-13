using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ShikshaMitra.Seeders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmSeederContributor_Tests
{
	[Fact]
	public void Operational_Seed_Should_Use_Repaired_First_Run_Prerequisites()
	{
		var method = typeof(AdmissionCrmSeederContributor).GetMethod(
			"RepairOperationalScenariosAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);

		method.ShouldNotBeNull();
		var parameterTypes = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

		parameterTypes.ShouldContain(typeof(IReadOnlyDictionary<Guid, AdmissionInquiryLead>));
		parameterTypes.ShouldContain(typeof(IReadOnlyList<AdmissionCrmExamSlot>));
	}
}
