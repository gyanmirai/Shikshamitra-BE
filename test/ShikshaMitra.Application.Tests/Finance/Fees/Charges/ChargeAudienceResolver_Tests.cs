using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class ChargeAudienceResolver_Tests
{
	private readonly IStudentRepository _studentRepository = Substitute.For<IStudentRepository>();
	private readonly IRepository<StudentFeeStructure, Guid> _studentFeeStructureRepository =
		Substitute.For<IRepository<StudentFeeStructure, Guid>>();
	private readonly ChargeAudienceResolver _resolver;

	public ChargeAudienceResolver_Tests()
	{
		_resolver = new ChargeAudienceResolver(_studentRepository, _studentFeeStructureRepository);
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
		SetAssignedStudents(studentId);

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
		SetAssignedStudents();

		var audience = new ChargeAudience(
			new AcademicScope(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]),
			new StudentSelection(StudentSelectionMode.Explicit, [Guid.NewGuid()]));

		await Should.ThrowAsync<BusinessException>(() => _resolver.ResolveAsync(audience, Guid.NewGuid()));
	}

	[Fact]
	public async Task ResolveAsync_ExplicitMode_Throws_When_Fee_Structure_Is_Not_Assigned()
	{
		var academicYearId = Guid.NewGuid();
		var studentId = Guid.NewGuid();
		_studentRepository.GetActiveStudentsInScopeAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>?>())
			.Returns([CreateStudent(studentId)]);
		SetAssignedStudents();

		var audience = new ChargeAudience(
			new AcademicScope(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]),
			new StudentSelection(StudentSelectionMode.Explicit, [studentId]));

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_resolver.ResolveAsync(audience, academicYearId));

		exception.Code.ShouldBe(FinanceChargeErrorCodes.StudentFeeStructureNotAssigned);
	}

	[Fact]
	public async Task ResolveAsync_AllInScope_Returns_Only_Students_With_Fee_Structure_Assignment()
	{
		var assignedStudentId = Guid.NewGuid();
		var unassignedStudentId = Guid.NewGuid();
		_studentRepository.GetActiveStudentsInScopeAsync(
			Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>?>())
			.Returns([CreateStudent(assignedStudentId), CreateStudent(unassignedStudentId)]);
		SetAssignedStudents(assignedStudentId);

		var audience = new ChargeAudience(
			new AcademicScope(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]),
			new StudentSelection(StudentSelectionMode.AllInScope));

		var result = await _resolver.ResolveAsync(audience, Guid.NewGuid());

		result.ShouldBe([assignedStudentId]);
	}

	private void SetAssignedStudents(params Guid[] studentIds)
	{
		var assignments = studentIds
			.Select(studentId => new StudentFeeStructure(
				Guid.NewGuid(), Guid.NewGuid(), studentId, Guid.NewGuid(), Guid.NewGuid()))
			.ToList();
		_studentFeeStructureRepository.GetListAsync(
			Arg.Any<Expression<Func<StudentFeeStructure, bool>>>(),
			Arg.Any<bool>(),
			Arg.Any<CancellationToken>())
			.Returns(assignments);
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
