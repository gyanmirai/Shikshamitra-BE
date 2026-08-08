using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmExamSlotConcurrency_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ShikshaMitraDbContext _dbContext;

	public AdmissionCrmExamSlotConcurrency_Tests()
	{
		_dbContext = GetRequiredService<ShikshaMitraDbContext>();
	}

	[Fact]
	public void Exam_slot_concurrency_stamp_must_be_an_ef_concurrency_token()
	{
		var entityType = _dbContext.Model.FindEntityType(typeof(AdmissionCrmExamSlot));
		var concurrencyStamp = entityType?.FindProperty(nameof(AdmissionCrmExamSlot.ConcurrencyStamp));

		entityType.ShouldNotBeNull();
		concurrencyStamp.ShouldNotBeNull();
		concurrencyStamp.IsConcurrencyToken.ShouldBeTrue();
	}

	[Fact]
	public void Registration_application_concurrency_stamp_must_be_an_ef_concurrency_token()
	{
		var entityType = _dbContext.Model.FindEntityType(typeof(AdmissionCrmRegistrationApplication));
		var concurrencyStamp = entityType?.FindProperty(nameof(AdmissionCrmRegistrationApplication.ConcurrencyStamp));

		entityType.ShouldNotBeNull();
		concurrencyStamp.ShouldNotBeNull();
		concurrencyStamp.IsConcurrencyToken.ShouldBeTrue();
	}
}
