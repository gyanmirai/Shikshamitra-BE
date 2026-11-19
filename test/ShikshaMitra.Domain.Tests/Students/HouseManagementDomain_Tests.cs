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
		division.ReplaceHouseLeader(enrollment, division.ConcurrencyStamp);
		division.ReplaceLeader(enrollment, division.ConcurrencyStamp);
		Assert.True(division.HouseLeaderDeparted(enrollment.Id));
		enrollment.AssignHouse(Guid.NewGuid());
		Assert.Null(division.HouseLeaderEnrollmentId);
		Assert.Equal(enrollment.Id, division.LeaderEnrollmentId);
	}

	[Fact]
	public void House_leadership_rejects_non_member_inactive_and_other_tenant_enrollments()
	{
		var tenant = Guid.NewGuid();
		var standard = Guid.NewGuid();
		var division = new Division(Guid.NewGuid(), tenant, "A", standard, 50, "");
		var enrollment = StudentEnrollment.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(), standard, division.Id);
		Assert.Throws<BusinessException>(() => division.ReplaceHouseLeader(enrollment, division.ConcurrencyStamp));
		enrollment.AssignHouse(Guid.NewGuid());
		enrollment.Deactivate();
		Assert.Throws<BusinessException>(() => division.ReplaceHouseLeader(enrollment, division.ConcurrencyStamp));
		var otherTenant = StudentEnrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), standard, division.Id);
		otherTenant.AssignHouse(Guid.NewGuid());
		Assert.Throws<BusinessException>(() => division.ReplaceHouseLeader(otherTenant, division.ConcurrencyStamp));
		var otherDivision = StudentEnrollment.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(), standard, Guid.NewGuid());
		otherDivision.AssignHouse(Guid.NewGuid());
		Assert.Throws<BusinessException>(() => division.ReplaceHouseLeader(otherDivision, division.ConcurrencyStamp));
		Assert.Null(division.HouseLeaderEnrollmentId);
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
	public void Division_has_one_house_leader_slot_across_houses_and_can_clear_both_roles_on_departure()
	{
		var tenant = Guid.NewGuid();
		var standard = Guid.NewGuid();
		var division = new Division(Guid.NewGuid(), tenant, "A", standard, 50, "");
		var first = StudentEnrollment.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(), standard, division.Id);
		var next = StudentEnrollment.Create(Guid.NewGuid(), tenant, Guid.NewGuid(), first.AcademicYearId, standard, division.Id);
		first.AssignHouse(Guid.NewGuid());
		next.AssignHouse(Guid.NewGuid());
		division.ReplaceHouseLeader(first, division.ConcurrencyStamp);
		Assert.Equal(HouseManagementErrorCodes.StalePreview,
			Assert.Throws<BusinessException>(() => division.ReplaceHouseLeader(next, "stale")).Code);
		division.ReplaceHouseLeader(next, division.ConcurrencyStamp);
		division.ReplaceLeader(next, division.ConcurrencyStamp);
		Assert.False(division.HouseLeaderDeparted(first.Id));
		Assert.Equal(next.Id, division.HouseLeaderEnrollmentId);
		division.RosterMemberDeparted(next.Id);
		Assert.Null(division.HouseLeaderEnrollmentId);
		Assert.Null(division.LeaderEnrollmentId);
	}
}
