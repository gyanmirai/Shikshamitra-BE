using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	private Task<string> RecipientIdentifierAsync(Context context) => WithUnitOfWorkAsync(async () =>
		(await GetRequiredService<IRepository<LibraryMember, Guid>>().GetAsync(context.MemberId)).MemberIdentifier);

	private async Task<PaginatedResponse<LibraryNotificationRecipientOptionDto>> SearchRecipientAsync(Context context) =>
		await Services().Get<LibraryNotificationRecipientLookupAppService>().SearchAsync(new() { Search = await RecipientIdentifierAsync(context) });

	private Task DeactivateRecipientAsync(Context context) => WithUnitOfWorkAsync(async () =>
	{
		var members = GetRequiredService<IRepository<LibraryMember, Guid>>();
		var member = await members.GetAsync(context.MemberId);
		member.ChangeMemberStatus(false);
		await members.UpdateAsync(member, true);
	});

	private Task SeedRecipientChargeAsync(Context context) => WithUnitOfWorkAsync(async () =>
	{
		var student = await GetRequiredService<IRepository<Student, Guid>>().GetAsync(context.StudentId);
		var category = new LibraryChargeCategory(Guid.NewGuid(), context.TenantId, student.AcademicYearId!.Value,
			"Existing charge", 10m, true, string.Empty);
		await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(category, true);
		await GetRequiredService<IRepository<LibraryCharge, Guid>>().InsertAsync(new LibraryCharge(Guid.NewGuid(), context.MemberId, 0,
			category.Id, null, 10m, new DateTime(2026, 11, 1), new DateTime(2026, 11, 18), context.TenantId), true);
	});

	private Task LimitRecipientActorToStageAsync(Context context) => WithUnitOfWorkAsync(async () =>
	{
		var student = await GetRequiredService<IRepository<Student, Guid>>().GetAsync(context.StudentId);
		var scopes = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await scopes.DeleteAsync(row => row.UserId == context.ActorId, true);
		await scopes.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), context.TenantId, userId: context.ActorId,
			academicYearId: student.AcademicYearId, educationBoardId: student.AcademicBoardId, educationMediumId: student.AcademicMediumId,
			educationMediumStageId: student.AcademicMediumStageId), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(context.ActorId);
	});

	private Task<Guid> SeedOutsideRecipientAsync(Context context) => WithUnitOfWorkAsync(async () =>
	{
		var source = await GetRequiredService<IRepository<Student, Guid>>().GetAsync(context.StudentId);
		var stage = new EducationMediumStage(Guid.NewGuid(), context.TenantId, "Other stage", null, source.AcademicMediumId);
		await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
		var outside = Student.CreateDirectRegistration(Guid.NewGuid(), context.TenantId, "Other", null, "Child",
			source.AcademicYearId!.Value, source.AcademicBoardId!.Value, source.AcademicMediumId, stage.Id, null, null,
			new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Female,
			primaryContactNo: "9999999997", email: "other@example.test");
		outside.AdditionalDetail!.IsLibraryMember = true;
		await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(outside, true);
		return outside.Id;
	});
}
