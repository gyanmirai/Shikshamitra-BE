using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using ShikshaMitra.Seeders;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students;

public class StudentSeederIdentity_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Student_user_link_is_optional_unique_and_restricted_to_identity_users()
	{
		var context = GetRequiredService<ShikshaMitraDbContext>();
		var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Student)).ShouldNotBeNull();
		var userId = entity.FindProperty(nameof(Student.UserId)).ShouldNotBeNull();

		userId.IsNullable.ShouldBeTrue();
		entity.GetIndexes().Single(index => index.Properties.Contains(userId)).IsUnique.ShouldBeTrue();
		var foreignKey = entity.GetForeignKeys().Single(key => key.Properties.Contains(userId));
		foreignKey.PrincipalEntityType.ClrType.ShouldBe(typeof(IdentityUser));
	}

	[Fact]
	public async Task Seeder_creates_an_active_Student_role_login_and_links_it_to_the_student()
	{
		var tenantId = Guid.NewGuid();
		var currentTenant = GetRequiredService<ICurrentTenant>();
		using (currentTenant.Change(tenantId))
		{
			var student = new Student(Guid.NewGuid(), tenantId, "Aanya", null, "Patel",
				Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
			await WithUnitOfWorkAsync(async () =>
			{
				var roleManager = GetRequiredService<IdentityRoleManager>();
				(await roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), "Student", tenantId)))
					.Succeeded.ShouldBeTrue();
				await GetRequiredService<StudentSeederContributor>().CreateUserForStudentAsync(student, "Student", "English", "Class 6");
			});

			student.UserId.ShouldNotBeNull();
			student.UserId.ShouldBe(student.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var user = await GetRequiredService<IIdentityUserRepository>().FindAsync(student.UserId!.Value);
				user.ShouldNotBeNull();
				user.Id.ShouldBe(student.Id);
				user.TenantId.ShouldBe(tenantId);
				user.IsActive.ShouldBeTrue();
				user.ShouldChangePasswordOnNextLogin.ShouldBeFalse();
				user.UserName.ShouldBe("student-english-class-6-aanya-patel");
				user.Email.ShouldBe("student-english-class-6-aanya-patel@example.invalid");
				var manager = GetRequiredService<IdentityUserManager>();
				(await manager.IsInRoleAsync(user, "Student")).ShouldBeTrue();
				(await manager.IsInRoleAsync(user, "admin")).ShouldBeFalse();
				(await manager.CheckPasswordAsync(user, UserSeederContributor.DefaultPassword)).ShouldBeTrue();
			});
		}
	}

	[Theory]
	[InlineData("English", "english")]
	[InlineData("Class 6", "class-6")]
	[InlineData("Aanya  Patel", "aanya-patel")]
	public void Demo_student_username_segments_are_safe_and_readable(string input, string expected)
	{
		StudentSeederContributor.UsernameSegment(input).ShouldBe(expected);
	}
}
