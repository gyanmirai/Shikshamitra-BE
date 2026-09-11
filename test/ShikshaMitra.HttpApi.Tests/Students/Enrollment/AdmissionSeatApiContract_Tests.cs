using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionSeats;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionSeatApiContract_Tests
{
	[Theory]
	[InlineData(nameof(AdmissionSeatAppService.GetAsync), Permissions.View)]
	[InlineData(nameof(AdmissionSeatAppService.ReserveAsync), Permissions.Reserve)]
	[InlineData(nameof(AdmissionSeatAppService.ConfirmAsync), Permissions.Confirm)]
	[InlineData(nameof(AdmissionSeatAppService.RenewAsync), Permissions.Renew)]
	[InlineData(nameof(AdmissionSeatAppService.ReleaseAsync), Permissions.Release)]
	[InlineData(nameof(AdmissionSeatAppService.WaitlistAsync), Permissions.Waitlist)]
	[InlineData(nameof(AdmissionSeatAppService.UpdateCapacityAsync), Permissions.ManageCapacity)]
	public void Seat_operations_require_distinct_permissions_at_every_boundary(string method, string permission)
	{
		foreach (var boundary in new[] { typeof(IAdmissionSeatAppService), typeof(AdmissionSeatAppService), typeof(AdmissionSeatsController) })
			boundary.GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>()
				.Select(item => item.Policy).ShouldContain(permission);
	}

	[Fact]
	public void Managed_seat_contract_is_additive_and_runtime_ready()
	{
		typeof(AdmissionSeatsController).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe(
			"api/student/enrollment/admissions/seats");
		typeof(AdmissionApplicationStageDto).GetProperty("SeatStageEnabled")!.PropertyType.ShouldBe(typeof(bool));
		typeof(AdmissionSeatStageDto).GetProperty(nameof(AdmissionSeatStageDto.RemainingCapacity)).ShouldNotBeNull();
		typeof(ReserveAdmissionSeatDto).GetProperty(nameof(ReserveAdmissionSeatDto.IdempotencyKey)).ShouldNotBeNull();
		var metadata = AdmissionWorkflowStageTypeRegistry.Find(AdmissionWorkflowStageType.Seat)!;
		metadata.IsRuntimeReady.ShouldBeTrue();
		metadata.SupportedConfiguration.ShouldContain("seat");
	}
}
