using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionApplicantIdentityModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Reservation_is_required_immutable_globally_unique_and_has_no_Student_foreign_key()
	{
		var context = GetRequiredService<ShikshaMitraDbContext>();
		var applicant = context.GetService<IDesignTimeModel>().Model
			.FindEntityType(typeof(AdmissionApplicant)).ShouldNotBeNull();
		var reservation = applicant.FindProperty(nameof(AdmissionApplicant.ReservedStudentId)).ShouldNotBeNull();

		reservation.IsNullable.ShouldBeFalse();
		reservation.ValueGenerated.ShouldBe(ValueGenerated.Never);
		reservation.GetDefaultValueSql().ShouldBeNull();
		reservation.FindAnnotation(RelationalAnnotationNames.DefaultValue).ShouldBeNull();
		reservation.GetAfterSaveBehavior().ShouldBe(PropertySaveBehavior.Throw);
		applicant.GetForeignKeys().ShouldNotContain(key => key.Properties.Contains(reservation));
		var index = applicant.GetIndexes().Single(candidate =>
			candidate.GetDatabaseName() == "UX_AdmissionApplicants_ReservedStudentId");
		index.IsUnique.ShouldBeTrue();
		index.Properties.Select(property => property.Name)
			.ShouldBe(new[] { nameof(AdmissionApplicant.ReservedStudentId) });
		index.GetFilter().ShouldBeNull();
		applicant.GetCheckConstraints().ShouldContain(check =>
			check.Name == "CK_AdmissionApplicants_ReservedStudentId" &&
			check.Sql.Contains(nameof(AdmissionApplicant.ReservedStudentId)) &&
			check.Sql.Contains(Guid.Empty.ToString()));
	}

	[Fact]
	public async Task Repository_profile_and_Student_link_updates_keep_the_persisted_reservation()
	{
		var repository = GetRequiredService<IRepository<AdmissionApplicant, Guid>>();
		var reservedStudentId = Guid.NewGuid();
		var applicant = await InsertApplicantAsync(reservedStudentId, null);
		var studentId = Guid.NewGuid();

		// Exercise detached repository updates, as well as fresh database reads.
		applicant.UpdateProfile("Updated", null, "Rao", new DateTime(2018, 2, 3), "Female", city: "Pune");
		await WithUnitOfWorkAsync(() => repository.UpdateAsync(applicant, autoSave: true));
		await WithUnitOfWorkAsync(async () =>
		{
			var reloaded = await repository.GetAsync(applicant.Id);
			reloaded.FirstName.ShouldBe("Updated");
			reloaded.City.ShouldBe("Pune");
			reloaded.ReservedStudentId.ShouldBe(reservedStudentId);
			reloaded.ExistingStudentId.ShouldBeNull();
			reloaded.LinkStudent(studentId);
			await repository.UpdateAsync(reloaded, autoSave: true);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var reloaded = await repository.GetAsync(applicant.Id);
			reloaded.ExistingStudentId.ShouldBe(studentId);
			reloaded.ReservedStudentId.ShouldBe(reservedStudentId);
		});
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task Reservation_cannot_be_reused_across_tenants_or_after_soft_deletion(bool tenantOwned, bool softDeleted)
	{
		var reservedStudentId = Guid.NewGuid();
		Guid? originalTenantId = tenantOwned ? Guid.NewGuid() : null;
		var original = await InsertApplicantAsync(reservedStudentId, originalTenantId);
		if (softDeleted)
		{
			using var tenant = GetRequiredService<ICurrentTenant>().Change(originalTenantId);
			await WithUnitOfWorkAsync(() =>
				GetRequiredService<IRepository<AdmissionApplicant, Guid>>().DeleteAsync(original.Id, autoSave: true));
		}

		var error = await Should.ThrowAsync<DbUpdateException>(() =>
			InsertApplicantAsync(reservedStudentId, Guid.NewGuid()));

		error.InnerException.ShouldBeOfType<SqliteException>().SqliteExtendedErrorCode.ShouldBe(2067);
		error.InnerException!.Message.ShouldContain("ReservedStudentId");
	}

	[Fact]
	public async Task Database_rejects_empty_reservations_even_when_domain_validation_is_bypassed()
	{
		var applicant = CreateApplicant(Guid.NewGuid(), null);
		typeof(AdmissionApplicant).GetProperty(nameof(AdmissionApplicant.ReservedStudentId))!
			.SetValue(applicant, Guid.Empty);

		var error = await Should.ThrowAsync<DbUpdateException>(() => WithUnitOfWorkAsync(() =>
			GetRequiredService<IRepository<AdmissionApplicant, Guid>>().InsertAsync(applicant, autoSave: true)));

		error.InnerException.ShouldBeOfType<SqliteException>().SqliteExtendedErrorCode.ShouldBe(275);
		error.InnerException!.Message.ShouldContain("CK_AdmissionApplicants_ReservedStudentId");
	}

	[Fact]
	public async Task Changing_a_tracked_reservation_is_rejected_and_does_not_change_the_database()
	{
		var reservedStudentId = Guid.NewGuid();
		var applicant = await InsertApplicantAsync(reservedStudentId, null);
		var provider = GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>();
		var repository = GetRequiredService<IRepository<AdmissionApplicant, Guid>>();

		var error = await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(async () =>
		{
			var reloaded = await repository.GetAsync(applicant.Id);
			typeof(AdmissionApplicant).GetProperty(nameof(AdmissionApplicant.ReservedStudentId))!
				.SetValue(reloaded, Guid.NewGuid());
			await (await provider.GetDbContextAsync()).SaveChangesAsync();
		}));

		error.Message.ShouldContain(nameof(AdmissionApplicant.ReservedStudentId));
		await WithUnitOfWorkAsync(async () =>
			(await repository.GetAsync(applicant.Id)).ReservedStudentId.ShouldBe(reservedStudentId));
	}

	private async Task<AdmissionApplicant> InsertApplicantAsync(Guid reservedStudentId, Guid? tenantId)
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		return await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionApplicant, Guid>>()
			.InsertAsync(CreateApplicant(reservedStudentId, tenantId), autoSave: true));
	}

	private static AdmissionApplicant CreateApplicant(Guid reservedStudentId, Guid? tenantId) =>
		new(Guid.NewGuid(), tenantId, Guid.NewGuid(), "Asha", null, "Rao",
			new DateTime(2018, 1, 1), "Female", null, reservedStudentId);
}
