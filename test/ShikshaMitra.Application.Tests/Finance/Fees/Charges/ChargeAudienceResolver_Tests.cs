using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Finance.Charges;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class ChargeAudienceResolver_Tests
{
	private readonly IStudentRepository _studentRepository = Substitute.For<IStudentRepository>();
	private readonly ChargeAudienceResolver _resolver;

	public ChargeAudienceResolver_Tests()
	{
		_resolver = new ChargeAudienceResolver(_studentRepository);
	}

	[Fact]
	public async Task ResolveAsync_ExplicitMode_Returns_Subset()
	{
		var studentId = Guid.NewGuid();
		_studentRepository.GetActiveStudentsInScopeAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>?>())
			.Returns([
				CreateStudent(studentId)
			]);

		var audience = new ChargeAudience(
			new AcademicScope(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]),
			new StudentSelection(StudentSelectionMode.Explicit, [studentId]));

		var result = await _resolver.ResolveAsync(audience, Guid.NewGuid());

		result.Count.ShouldBe(1);
		result[0].ShouldBe(studentId);
	}

	[Fact]
	public async Task ResolveAsync_ExplicitMode_Throws_When_Student_Out_Of_Scope()
	{
		_studentRepository.GetActiveStudentsInScopeAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>?>())
			.Returns([CreateStudent(Guid.NewGuid())]);

		var audience = new ChargeAudience(
			new AcademicScope(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]),
			new StudentSelection(StudentSelectionMode.Explicit, [Guid.NewGuid()]));

		await Should.ThrowAsync<BusinessException>(() => _resolver.ResolveAsync(audience, Guid.NewGuid()));
	}

	private static Student CreateStudent(Guid id)
	{
		return new Student(
			id,
			Guid.NewGuid(),
			"Test",
			null,
			"Student",
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid());
	}
}
