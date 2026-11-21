using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCoreWorkflow_Tests
{
	[Fact]
	public async Task Issue_Should_Respect_The_Current_Inactive_Membership_Instead_Of_An_Older_Active_Alias()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("LATEST-OFF"))).Single();
		await AddLaterMembershipAsync(context, false);
		var resolved = await GetRequiredService<ILibraryCirculationMemberAppService>().ResolveAsync(context.ActiveStudentId.ToString());
		resolved.IsEligibleForIssue.ShouldBeFalse();

		var error = await Should.ThrowAsync<BusinessException>(() => IssueCanonicalAsync(copy.Id, context.ActiveStudentId, new(2026, 11, 17), new(2026, 11, 24)));

		error.Code.ShouldBe(LibraryErrorCodes.MemberInactive);
	}

	[Fact]
	public async Task Issue_Should_Use_A_Current_Active_Membership_While_Retaining_An_Older_Inactive_Alias()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await GetLibraryContextAsync();
		var copy = (await CreateCopiesAsync(context, UniqueAccession("LATEST-ON"))).Single();
		await DeactivateLibraryMemberAsync(context.ActiveStudentLibraryMemberId);
		await AddLaterMembershipAsync(context, true);
		var resolved = await GetRequiredService<ILibraryCirculationMemberAppService>().ResolveAsync(context.ActiveStudentId.ToString());
		resolved.IsEligibleForIssue.ShouldBeTrue();

		var issued = await IssueCanonicalAsync(copy.Id, context.ActiveStudentId, new(2026, 11, 17), new(2026, 11, 24));
		issued.Id.ShouldNotBeNullOrWhiteSpace();
	}

	private async Task AddLaterMembershipAsync(LibraryTestContext context, bool active)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var older = await _libraryMemberRepository.GetAsync(context.ActiveStudentLibraryMemberId);
			older.CreationTime = DateTime.UtcNow.AddDays(-2);
			await _libraryMemberRepository.UpdateAsync(older, true);
			await _libraryMemberRepository.InsertAsync(new LibraryMember(Guid.NewGuid(), older.TenantId,
				context.ActiveStudentId, UniqueAccession("MEM-CURRENT")[..20], active)
			{ CreationTime = DateTime.UtcNow.AddDays(-1) }, true);
		});
	}
}
