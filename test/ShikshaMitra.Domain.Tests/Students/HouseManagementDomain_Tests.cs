using System;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.Records.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class HouseManagementDomain_Tests
{
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void House_name_must_not_be_blank(string name)
	{
		var house = new HouseSettings(Guid.NewGuid(), "Falcons", "#123456", Guid.NewGuid());
		Assert.Equal(HouseManagementErrorCodes.InvalidInput, Assert.Throws<BusinessException>(() => house.UpdateDetails(name, "#123456")).Code);
	}

	[Fact]
	public void Moving_house_leader_vacates_house_role_and_preserves_independent_division_leader()
	{
		var tenant = Guid.NewGuid();
		var standard = Guid.NewGuid();
		var division = new Division(Guid.NewGuid(), tenant, "A", standard, 50, "");
		var enrollment = StudentEnrollment.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(), standard, division.Id);
		var source = new HouseSettings(Guid.NewGuid(), "Falcons", "#123456", Guid.NewGuid())
		{
			TenantId = tenant
		};
		enrollment.AssignHouse(source.Id);
		source.ReplaceLeader(enrollment);
		division.ReplaceLeader(enrollment, division.ConcurrencyStamp);
		source.MemberDeparted(enrollment.Id);
		enrollment.AssignHouse(Guid.NewGuid());
		Assert.Null(source.LeaderEnrollmentId);
		Assert.Equal(enrollment.Id, division.LeaderEnrollmentId);
	}

	[Fact]
	public void House_leadership_rejects_non_member_inactive_and_other_tenant_enrollments()
	{
		var tenant = Guid.NewGuid();
		var house = new HouseSettings(Guid.NewGuid(), "Falcons", "#123456", Guid.NewGuid())
		{
			TenantId = tenant
		};
		var enrollment = StudentEnrollment.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		Assert.Throws<BusinessException>(() => house.ReplaceLeader(enrollment));
		enrollment.AssignHouse(house.Id);
		enrollment.Deactivate();
		Assert.Throws<BusinessException>(() => house.ReplaceLeader(enrollment));
		var otherTenant = StudentEnrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		otherTenant.AssignHouse(house.Id);
		Assert.Throws<BusinessException>(() => house.ReplaceLeader(otherTenant));
		Assert.Null(house.LeaderEnrollmentId);
	}

	[Fact]
	public void Division_leadership_needs_no_house_but_rejects_other_divisions_and_stale_replacement()
	{
		var tenant = Guid.NewGuid();
		var standard = Guid.NewGuid();
		var division = new Division(Guid.NewGuid(), tenant, "A", standard, 50, "");
		var enrollment = StudentEnrollment.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(), standard, division.Id);
		division.ReplaceLeader(enrollment, division.ConcurrencyStamp);
		Assert.Equal(enrollment.Id, division.LeaderEnrollmentId);
		Assert.Throws<BusinessException>(() => division.ReplaceLeader(null, "stale"));
		var outsider = StudentEnrollment.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(), standard, Guid.NewGuid());
		Assert.Throws<BusinessException>(() => division.ReplaceLeader(outsider, division.ConcurrencyStamp));
		Assert.Equal(enrollment.Id, division.LeaderEnrollmentId);
		division.ReplaceLeader(null, division.ConcurrencyStamp);
		Assert.Null(division.LeaderEnrollmentId);
	}

	[Fact]
	public void House_leader_replacement_has_one_slot_and_can_be_vacant()
	{
		var tenant = Guid.NewGuid();
		var house = new HouseSettings(Guid.NewGuid(), "Falcons", "#123456", Guid.NewGuid())
		{
			TenantId = tenant
		};
		var first = StudentEnrollment.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);
		var next = StudentEnrollment.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), first.AcademicYearId, Guid.NewGuid(), null);
		first.AssignHouse(house.Id);
		next.AssignHouse(house.Id);
		house.ReplaceLeader(first);
		house.ReplaceLeader(next);
		Assert.Equal(next.Id, house.LeaderEnrollmentId);
		house.MemberDeparted(first.Id);
		Assert.Equal(next.Id, house.LeaderEnrollmentId);
		house.ReplaceLeader(null);
		Assert.Null(house.LeaderEnrollmentId);
		Assert.Throws<BusinessException>(() => house.EnsureVersion("stale"));
	}
}
