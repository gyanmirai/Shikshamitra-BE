using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFeeStructureReader_Tests
{
	[Theory]
	[InlineData(true, 1)]
	[InlineData(false, 0)]
	public async Task Only_published_matching_year_class_board_medium_structures_with_active_heads_are_candidates(bool headActive, int count)
	{
		var year = Guid.NewGuid(); var board = Guid.NewGuid(); var medium = Guid.NewGuid(); var standard = Guid.NewGuid();
		var cycle = new AdmissionCycle(Guid.NewGuid(), null, "QA", "QA", year, DateTime.Today, DateTime.Today.AddDays(30));
		var offering = new AdmissionOffering(Guid.NewGuid(), null, cycle.Id, null, board, medium, Guid.NewGuid(), standard, "QA", true, null, null);
		var application = new AdmissionApplication(Guid.NewGuid(), null, "QA", Guid.NewGuid(), null, cycle.Id, offering.Id, Guid.NewGuid(), Guid.NewGuid());
		var metadata = new FeeStructureMetadata(Guid.NewGuid(), null, year, board, medium, null, "QA");
		var wrongYear = new FeeStructureMetadata(Guid.NewGuid(), null, Guid.NewGuid(), board, medium, null, "Wrong year");
		var wrongBoard = new FeeStructureMetadata(Guid.NewGuid(), null, year, Guid.NewGuid(), medium, null, "Wrong board");
		FeeStructure Structure(Guid metadataId, Guid classId, bool published) {
			var row = new FeeStructure(Guid.NewGuid(), null, metadataId, classId, "QA"); if (published) row.Lock(); return row; }
		var matching = Structure(metadata.Id, standard, true);
		var rows = new[] { matching, Structure(metadata.Id, standard, false), Structure(metadata.Id, Guid.NewGuid(), true),
			Structure(wrongYear.Id, standard, true), Structure(wrongBoard.Id, standard, true) };
		var head = new FeeHead(Guid.NewGuid(), null, year, Guid.NewGuid(), "Form", null, false, null, Guid.NewGuid(), headActive);
		var details = rows.Select(row => new FeeStructureDetail(Guid.NewGuid(), null, row.Id, head.Id, 200m, true, false, null, null));
		var offerings = Substitute.For<IRepository<AdmissionOffering, Guid>>(); offerings.GetAsync(offering.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(offering);
		var cycles = Substitute.For<IRepository<AdmissionCycle, Guid>>(); cycles.GetAsync(cycle.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(cycle);
		var reader = new AdmissionFeeStructureReader(offerings, cycles, Repository([metadata, wrongYear, wrongBoard]), Repository(rows), Repository(details), Repository([head]));
		var result = await reader.GetCandidatesAsync(application);
		result.Count.ShouldBe(count);
		if (headActive) {
			result.Single().StructureId.ShouldBe(matching.Id); result.Single().Heads.Single().ConfiguredAmount.ShouldBe(200m);
			result.Single().Heads.Single().IsOptional.ShouldBeFalse(); }
	}

	private static IRepository<T, Guid> Repository<T>(IEnumerable<T> rows) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetListAsync(Arg.Any<Expression<Func<T, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => rows.Where(call.Arg<Expression<Func<T, bool>>>().Compile()).ToList());
		return repository;
	}
}
