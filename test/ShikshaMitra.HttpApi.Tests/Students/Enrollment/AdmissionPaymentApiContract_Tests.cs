using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionPayments;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionPaymentApiContract_Tests
{
	[Theory]
	[InlineData(nameof(AdmissionPaymentAppService.GetAsync), Permissions.View)]
	[InlineData(nameof(AdmissionPaymentAppService.RecordAsync), Permissions.Record)]
	[InlineData(nameof(AdmissionPaymentAppService.ReviewAsync), Permissions.Verify)]
	[InlineData(nameof(AdmissionPaymentAppService.WaiveAsync), Permissions.Waive)]
	[InlineData(nameof(AdmissionPaymentAppService.ReverseAsync), Permissions.Reverse)]
	public void Payment_operations_require_distinct_permissions_at_both_boundaries(
		string method, string permission)
	{
		foreach (var boundary in new[] { typeof(AdmissionPaymentAppService), typeof(AdmissionPaymentsController) })
			boundary.GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>()
				.Select(item => item.Policy).ShouldContain(permission);
	}

	[Fact]
	public void Managed_payment_contract_is_additive_and_runtime_ready()
	{
		typeof(AdmissionPaymentsController).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe(
			"api/student/enrollment/admissions/payments");
		typeof(AdmissionApplicationStageDto).GetProperty("PaymentStageEnabled")!.PropertyType.ShouldBe(typeof(bool));
		typeof(AdmissionPaymentStageDto).GetProperty("Transactions").ShouldNotBeNull();
		typeof(ReviewAdmissionPaymentDto).GetProperty("IdempotencyKey").ShouldNotBeNull();
		var metadata = AdmissionWorkflowStageTypeRegistry.Find(AdmissionWorkflowStageType.Payment)!;
		metadata.IsRuntimeReady.ShouldBeTrue();
		metadata.SupportedConfiguration.ShouldContain("payment");
	}
}
