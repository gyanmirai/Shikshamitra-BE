using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Library.IssueReturn;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCoreWorkflow_Tests
{
	[Fact]
	public async Task Circulation_Should_Resolve_The_Printed_Member_Identifier_To_The_Canonical_Person()
	{
		var context = await GetLibraryContextAsync();
		var identifier = await WithUnitOfWorkAsync(async () =>
			(await _libraryMemberRepository.GetAsync(context.ActiveStudentLibraryMemberId)).MemberIdentifier);

		var result = await CirculationMembers().ResolveAsync(identifier);

		result.Id.ShouldBe(context.ActiveStudentId);
		result.Role.ShouldBe(0);
		result.IsEligibleForIssue.ShouldBeTrue();
		result.MemberIdentifier.ShouldBe(identifier);
	}

	[Fact]
	public async Task Circulation_Should_Resolve_A_Legacy_Membership_Guid_Without_Changing_The_Borrower()
	{
		var context = await GetLibraryContextAsync();

		var result = await CirculationMembers().ResolveAsync(context.ActiveStudentLibraryMemberId.ToString("D"));

		result.Id.ShouldBe(context.ActiveStudentId);
		result.AcademicYearId.ShouldBe(context.AcademicYearId);
	}

	[Fact]
	public async Task Circulation_Search_Should_Find_Printed_Identifiers_And_Explain_Inactive_Borrowing_Eligibility()
	{
		var context = await GetLibraryContextAsync();
		await DeactivateLibraryMemberAsync(context.ActiveStudentLibraryMemberId);
		var identifier = await WithUnitOfWorkAsync(async () =>
			(await _libraryMemberRepository.GetAsync(context.ActiveStudentLibraryMemberId)).MemberIdentifier);

		var result = await CirculationMembers().SearchAsync(identifier);

		result.TotalCount.ShouldBe(1);
		result.Items.Single().Id.ShouldBe(context.ActiveStudentId);
		result.Items.Single().IsEligibleForIssue.ShouldBeFalse();
		result.Items.Single().IneligibilityReason.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Circulation_Should_Reject_An_Unknown_Identifier_Instead_Of_Inventing_A_Borrower()
	{
		await GetLibraryContextAsync();
		var members = CirculationMembers();

		var error = await Should.ThrowAsync<BusinessException>(() => members.ResolveAsync($"UNKNOWN-{Guid.NewGuid():N}"));

		error.Code.ShouldBe(LibraryErrorCodes.MemberNotFound);
	}

	private ILibraryCirculationMemberAppService CirculationMembers()
	{
		var service = ServiceProvider.GetService<ILibraryCirculationMemberAppService>();
		service.ShouldNotBeNull("Circulation must support a scoped card identifier lookup under its own action rights.");
		return service!;
	}
}
