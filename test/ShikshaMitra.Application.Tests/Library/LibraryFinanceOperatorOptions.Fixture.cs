using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Shouldly;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private async Task<OperatorChoicesFixture> SeedOperatorChoicesAsync(FinanceExternalPayerKind kind)
	{
		var typed = await SeedTypedSettlementAsync(kind);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(typed.Owner.TenantId);
		using var actor = SignIn(typed.Owner);
		return await WithUnitOfWorkAsync(async () =>
		{
			var owner = typed.Owner;
			var search = $"CHOICE-{Guid.NewGuid():N}"[..15];
			var library = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var template = await library.GetAsync(owner.AssessmentId);
			// An independently reviewed imported assessment remains postable after the
			// real payer leaves. Its actual source year/dates are explicit evidence,
			// never inferred from a current enrollment or a fabricated fee assignment.
			var source = LibraryCharge.CreateConfirmed(Guid.NewGuid(), owner.PersonId, kind == FinanceExternalPayerKind.Student ? 0 : 1,
				template.ChargeCategoryId, null, 100.75m, owner.AssessmentDate, owner.DueDate, owner.YearId,
				LibraryAcademicYearProvenance.ReviewedLegacy,
				JsonSerializer.Serialize(new { SourceRegister = search, AcademicYearId = owner.YearId, ReviewedBy = owner.ActorId,
					Reason = "Reviewed the actual imported assessment year from the school register." }),
				JsonSerializer.Serialize(new { SourceRegister = search, owner.AssessmentDate, owner.DueDate, ReviewedBy = owner.ActorId,
					Reason = "Reviewed the actual recorded school assessment dates." }),
				"Reviewed imported Library assessment for operator options.", owner.TenantId);
			await library.InsertAsync(source, true);
			var scopedOwner = owner with { AssessmentId = source.Id, Version = source.GetAssessmentVersion() };
			var reference = Post(scopedOwner).Source;
			var categories = GetRequiredService<IRepository<ChargeCategory, Guid>>();
			var existingCategory = await categories.GetAsync(owner.FinanceCategoryId);
			ChargeCategory Category(Guid schoolId, Guid yearId, string suffix, bool active = true, bool mapped = true)
			{
				var category = new ChargeCategory(Guid.NewGuid(), schoolId, yearId, $"{search} {suffix}", true, null, active);
				if (mapped) category.SetAccountingMapping(existingCategory.IncomeCategoryId!.Value);
				return category;
			}
			var otherYear = new AcademicYear(Guid.NewGuid(), owner.TenantId, "Other actual options year",
				new DateTime(2025, 4, 1), new DateTime(2026, 3, 31));
			await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(otherYear, true);
			var otherSchool = await GetRequiredService<Volo.Abp.TenantManagement.ITenantManager>().CreateAsync($"Other options school {Guid.NewGuid():N}");
			await GetRequiredService<Volo.Abp.TenantManagement.ITenantRepository>().InsertAsync(otherSchool, true);
			var visibleA = Category(owner.TenantId, owner.YearId, "A");
			var visibleB = Category(owner.TenantId, owner.YearId, "B");
			var unmapped = Category(owner.TenantId, owner.YearId, "C needs accounting", mapped: false);
			await categories.InsertManyAsync([visibleA, visibleB, unmapped,
				Category(owner.TenantId, owner.YearId, "Inactive", active: false), Category(owner.TenantId, otherYear.Id, "Other year")], true);
			using (GetRequiredService<ICurrentTenant>().Change(otherSchool.Id))
				// Deliberately retain the source-year ID in foreign-owned metadata:
				// an imported/misassigned setting must still be fenced by its tenant.
				await categories.InsertAsync(Category(otherSchool.Id, owner.YearId, "Foreign school", mapped: false), true);
			var versions = GetRequiredService<IRepository<FinancePolicyVersion, Guid>>();
			var baseline = await versions.GetAsync(typed.CollectionPolicyVersionId);
			async Task<FinancePolicyVersion> Policy(Guid schoolId, Guid yearId, string suffix, CollectionPolicyStatus status)
			{
				var snapshot = CollectionPolicySnapshot.Deserialize(baseline.SnapshotJson);
				snapshot.PolicyName = $"{search} {suffix}"; snapshot.PolicyCode = Guid.NewGuid().ToString("N")[..16]; snapshot.Status = status;
				var policy = new CollectionPolicy(Guid.NewGuid(), schoolId, yearId, snapshot.ToUpdateModel());
				await GetRequiredService<IRepository<CollectionPolicy, Guid>>().InsertAsync(policy, true);
				var version = new FinancePolicyVersion(Guid.NewGuid(), schoolId, "collection", policy.Id, yearId,
					policy.Version, policy.PolicyName, status.ToString(), policy.ToSnapshot().Serialize(), "created", "Actual options fixture version");
				await versions.InsertAsync(version, true);
				return version;
			}
			var versionA = await Policy(owner.TenantId, owner.YearId, "A", CollectionPolicyStatus.Active);
			var versionB = await Policy(owner.TenantId, owner.YearId, "B", CollectionPolicyStatus.Active);
			await Policy(owner.TenantId, owner.YearId, "Draft", CollectionPolicyStatus.Draft);
			await Policy(owner.TenantId, otherYear.Id, "Other year", CollectionPolicyStatus.Active);
			using (GetRequiredService<ICurrentTenant>().Change(otherSchool.Id))
				await Policy(otherSchool.Id, owner.YearId, "Foreign school", CollectionPolicyStatus.Active);
			return new OperatorChoicesFixture(scopedOwner, reference, search, [visibleA.Id, visibleB.Id, unmapped.Id], unmapped.Id,
				[versionA.Id, versionB.Id], await GetRequiredService<IRepository<Charge, Guid>>().CountAsync(),
				await GetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>().CountAsync());
		});
	}

	private Task AssertOperatorChoicesAreReadOnlyAsync(OperatorChoicesFixture context) => WithUnitOfWorkAsync(async () =>
	{
		((long)await GetRequiredService<IRepository<Charge, Guid>>().CountAsync()).ShouldBe(context.ChargeCount);
		((long)await GetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>().CountAsync()).ShouldBe(context.CommandCount);
		(await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().CountAsync()).ShouldBe(0);
		(await GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().CountAsync()).ShouldBe(0);
		var source = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(context.Source.SourceId);
		source.FinanceSourceLinkId.ShouldBeNull(); source.GetAssessmentVersion().ShouldBe(context.Source.SourceVersion);
		source.Amount.ShouldBe(100.75m); source.AcademicYearId.ShouldBe(context.Owner.YearId);
	});

	private sealed record OperatorChoicesFixture(Fixture Owner, FinanceExternalSourceReference Source, string Search,
		Guid[] VisibleCategories, Guid UnmappedCategoryId, Guid[] VisiblePolicyVersions, long ChargeCount, long CommandCount);
}
