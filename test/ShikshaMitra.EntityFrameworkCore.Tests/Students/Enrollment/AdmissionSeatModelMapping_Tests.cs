using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionSeatModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Seat_pool_and_reservation_constraints_are_enforced()
	{
		var model = GetRequiredService<ShikshaMitraDbContext>().Model;
		var pool = model.FindEntityType(typeof(AdmissionSeatPool)).ShouldNotBeNull();
		var poolIndex = pool.GetIndexes().Single(item => item.Properties.Select(property => property.Name)
			.SequenceEqual(new[] { "TenantId", "AdmissionOfferingId" }));
		poolIndex.IsUnique.ShouldBeTrue();
		poolIndex.GetDatabaseName().ShouldBe("UX_AdmissionSeatPools_Offering");

		var reservation = model.FindEntityType(typeof(AdmissionSeatReservation)).ShouldNotBeNull();
		reservation.FindProperty(nameof(AdmissionSeatReservation.LastReason))!.GetMaxLength().ShouldBe(512);
		var reservationIndex = reservation.GetIndexes().Single(item => item.Properties.Select(property => property.Name)
			.SequenceEqual(new[] { "TenantId", "ApplicationId", "StageInstanceId" }));
		reservationIndex.IsUnique.ShouldBeTrue();
		reservationIndex.GetDatabaseName().ShouldBe("UX_AdmissionSeatReservations_ApplicationStage");
	}
}
