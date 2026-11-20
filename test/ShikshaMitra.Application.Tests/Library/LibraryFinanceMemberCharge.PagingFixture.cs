using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private Task<MemberChargePageMeasurement> MeasureMemberChargePageAsync(Fixture owner, int pageSize,
		RecordingMemberChargeDetails details) => WithUnitOfWorkAsync(async () =>
	{
		using var activity = new Activity("Actual paged member-charge Finance hydration").SetIdFormat(ActivityIdFormat.W3C).Start();
		using var capture = new LibraryLookupSqlCapture(activity.TraceId);
		var page = await MemberChargeReadService(owner, details).GetMemberChargesAsync(owner.PersonId, 1, pageSize);
		var commands = capture.Commands.ToArray();
		return new MemberChargePageMeasurement(page,
			commands.Count(sql => sql.Contains("\"Finance_", StringComparison.Ordinal)),
			commands.Count(sql => sql.Contains("\"AppLibraryCharges\"", StringComparison.Ordinal)),
			commands.Any(sql => sql.Contains("\"Finance_ChargeCollectionRecords\"", StringComparison.Ordinal)));
	});

	private Task<Guid> SeedOtherMemberChargeStudentAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var students = GetRequiredService<IRepository<Student, Guid>>();
		var target = await students.GetAsync(owner.PersonId, includeDetails: false);
		target.AcademicBoardId.ShouldNotBeNull(); target.AcademicMediumStageId.ShouldNotBeNull();
		var other = Student.CreateDirectRegistration(Guid.NewGuid(), owner.TenantId, "Other", null, "Payer",
			owner.YearId, target.AcademicBoardId!.Value, target.AcademicMediumId, target.AcademicMediumStageId!.Value,
			target.StandardId, target.DivisionId, new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero),
			GenderEnum.Female, primaryContactNo: "9777777777", email: "bulk-other-member@example.test");
		other.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
		await students.InsertAsync(other, true);
		var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
		var template = await charges.GetAsync(owner.AssessmentId);
		await charges.InsertAsync(new LibraryCharge(Guid.NewGuid(), other.Id, 0, template.ChargeCategoryId, null,
			17.75m, owner.AssessmentDate.ToDateTime(TimeOnly.MinValue), owner.DueDate.ToDateTime(TimeOnly.MinValue), owner.TenantId), true);
		return other.Id;
	});

	private Task AssertMemberChargeBulkSourcesAsync(Fixture owner, Guid[] sourceIds, Guid otherPersonId) => WithUnitOfWorkAsync(async () =>
	{
		(await GetRequiredService<ILibraryAccessService>().EnsureMemberAsync(otherPersonId)).ShouldBe(otherPersonId);
		var query = await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.MemberCharges,
			AssessmentIds: sourceIds));
		var rows = await GetRequiredService<IAsyncQueryableExecuter>().ToListAsync(query);
		rows.Count.ShouldBe(21);
		rows.ShouldAllBe(row => row.Evidence == FinanceExternalPositionEvidence.Verified && row.Settled == 40.25m);
		var assessments = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetListAsync();
		assessments.Count(row => row.MemberId == owner.PersonId).ShouldBe(21);
		assessments.Count(row => row.MemberId == otherPersonId).ShouldBe(1);
	});

	private sealed class RecordingMemberChargeDetails(IFinanceExternalPositionReader inner, bool rejectCalls = false)
		: IFinanceExternalPositionReader
	{
		public List<Guid[]> Calls { get; } = [];
		public Task<IReadOnlyList<FinanceExternalSourcePosition>> ReadAsync(
			IReadOnlyList<FinanceExternalPositionSource> authorizedSources, CancellationToken cancellationToken = default)
		{
			Calls.Add(authorizedSources.Select(source => source.SourceId).ToArray());
			if (rejectCalls) throw new InvalidOperationException("Empty member pages must not load Finance detail.");
			return inner.ReadAsync(authorizedSources, cancellationToken);
		}
	}

	private sealed record MemberChargePageMeasurement(PaginatedResponse<MemberChargeItemDto> Page,
		int FinanceStatements, int SourceStatements, bool HasActualAllocationQuery);
}
