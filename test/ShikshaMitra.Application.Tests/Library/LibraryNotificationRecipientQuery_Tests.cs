using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Notifications;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCoreWorkflow_Tests
{
	[Fact]
	public async Task Notification_Recipient_Resolution_Should_Read_Contact_Facts_Without_The_Student_Record_Graph()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		using var activity = new Activity("Library notification recipient query regression").SetIdFormat(ActivityIdFormat.W3C).Start();
		using var commands = new LibraryLookupSqlCapture(activity.TraceId, sql =>
		{
			if (!sql.Contains("FROM \"AppStudents\"", StringComparison.Ordinal)) return;
			sql.ShouldNotContain("DateOfBirth");
			sql.ShouldNotContain("AadharNo");
			sql.ShouldNotContain("AppStudentDocuments");
			sql.ShouldNotContain("StudentBankDetails");
			sql.ShouldNotContain("AppAcademicYears");
			sql.ShouldNotContain("AppEducationBoards");
		});
		var resolver = GetRequiredService<LibraryNotificationRecipientResolver>();
		var recipient = await WithUnitOfWorkAsync(() => resolver.ResolveMemberAsync(context.StudentId, 0));
		recipient.MemberId.ShouldBe(context.StudentId);
		recipient.IsActiveLibraryMember.ShouldBeTrue();
		var destinations = await WithUnitOfWorkAsync(() => resolver.ResolveDestinationsAsync(recipient, LibraryNotificationChannels.Email));
		destinations.ShouldNotBeEmpty();
		var studentCommands = commands.Commands.Where(sql => sql.Contains("FROM \"AppStudents\"", StringComparison.Ordinal)).ToList();
		studentCommands.ShouldNotBeEmpty("The test must observe the real recipient queries.");
		foreach (var sql in studentCommands)
		{
			sql.ShouldNotContain("DateOfBirth");
			sql.ShouldNotContain("AadharNo");
			sql.ShouldNotContain("AppStudentDocuments");
			sql.ShouldNotContain("StudentBankDetails");
			sql.ShouldNotContain("AppAcademicYears");
			sql.ShouldNotContain("AppEducationBoards");
		}
	}
}
