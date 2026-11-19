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
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFeeStructureReader_Tests
{
	[Theory]
	[InlineData(true, 1, DateTimeKind.Utc)]
	[InlineData(true, 1, DateTimeKind.Local)]
	[InlineData(true, 1, DateTimeKind.Unspecified)]
	[InlineData(false, 0, DateTimeKind.Utc)]
	public async Task Published_scoped_candidates_and_snapshot_preserve_the_clock_instant(bool headActive, int count, DateTimeKind clockKind)
	{
		var year = Guid.NewGuid(); var board = Guid.NewGuid(); var medium = Guid.NewGuid(); var standard = Guid.NewGuid();
		var cycle = new AdmissionCycle(Guid.NewGuid(), null, "QA", "QA", year, DateTime.Today, DateTime.Today.AddDays(30));
		var offering = new AdmissionOffering(Guid.NewGuid(), null, cycle.Id, null, board, medium, Guid.NewGuid(), standard, "QA", true, null, null);
		var application = new AdmissionApplication(Guid.NewGuid(), null, "QA", Guid.NewGuid(), null, cycle.Id, offering.Id, Guid.NewGuid(), Guid.NewGuid());
		var metadata = new FeeStructureMetadata(Guid.NewGuid(), null, year, board, medium, null, "QA");
		var wrongYear = new FeeStructureMetadata(Guid.NewGuid(), null, Guid.NewGuid(), board, medium, null, "Wrong year");
		var wrongBoard = new FeeStructureMetadata(Guid.NewGuid(), null, year, Guid.NewGuid(), medium, null, "Wrong board");
		var wrongMedium = new FeeStructureMetadata(Guid.NewGuid(), null, year, board, Guid.NewGuid(), null, "Wrong medium");
		var wrongTenant = new FeeStructureMetadata(Guid.NewGuid(), Guid.NewGuid(), year, board, medium, null, "Other tenant");
		FeeStructure Structure(Guid metadataId, Guid classId, bool published) {
			var row = new FeeStructure(Guid.NewGuid(), null, metadataId, classId, "QA"); if (published) row.Lock(); return row; }
		var matching = Structure(metadata.Id, standard, true);
		var rows = new[] { matching, Structure(metadata.Id, standard, false), Structure(metadata.Id, Guid.NewGuid(), true),
			Structure(wrongYear.Id, standard, true), Structure(wrongBoard.Id, standard, true),
			Structure(wrongMedium.Id, standard, true), Structure(wrongTenant.Id, standard, true) };
		var head = new FeeHead(Guid.NewGuid(), null, year, Guid.NewGuid(), "Form", null, false, null, Guid.NewGuid(), headActive);
		var details = rows.Select(row => new FeeStructureDetail(Guid.NewGuid(), null, row.Id, head.Id, 200m, true, false, null, null));
		var offerings = Substitute.For<IRepository<AdmissionOffering, Guid>>(); offerings.GetAsync(offering.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(offering);
		var cycles = Substitute.For<IRepository<AdmissionCycle, Guid>>(); cycles.GetAsync(cycle.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(cycle);
		var reader = new AdmissionFeeStructureReader(offerings, cycles, Repository([metadata, wrongYear, wrongBoard, wrongMedium, wrongTenant]), Repository(rows), Repository(details), Repository([head]));
		var instant = new DateTime(2026, 10, 8, 17, 38, 25, DateTimeKind.Utc);
		var clockValue = clockKind == DateTimeKind.Utc ? instant : DateTime.SpecifyKind(instant.ToLocalTime(), clockKind);
		var clock = Substitute.For<IClock>(); clock.Now.Returns(clockValue);
		var services = Substitute.For<IAbpLazyServiceProvider>(); services.LazyGetRequiredService<IClock>().Returns(clock);
		reader.LazyServiceProvider = services;
		var result = await reader.GetCandidatesAsync(application);
		result.Count.ShouldBe(count);
		if (headActive) {
			result.Single().StructureId.ShouldBe(matching.Id); result.Single().Heads.Single().ConfiguredAmount.ShouldBe(200m);
			result.Single().Heads.Single().IsOptional.ShouldBeFalse();
			var snapshot = await reader.CreateSnapshotAsync(application, Guid.NewGuid(), "INR", new() { StructureId = matching.Id }, Guid.NewGuid());
			snapshot.SavedAt.UtcDateTime.ShouldBe(instant); snapshot.SavedAt.Offset.ShouldBe(TimeSpan.Zero);
		}
	}

	private static IRepository<T, Guid> Repository<T>(IEnumerable<T> rows) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetListAsync(Arg.Any<Expression<Func<T, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call => rows.Where(call.Arg<Expression<Func<T, bool>>>().Compile()).ToList());
		return repository;
	}
}
