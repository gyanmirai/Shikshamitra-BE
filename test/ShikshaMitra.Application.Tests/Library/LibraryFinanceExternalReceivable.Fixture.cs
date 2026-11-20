using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private Task<Fixture> SeedAsync(FinanceExternalPayerKind payerKind, bool departed,
		bool grantFinanceAdd = true, bool allowSourceScope = true, DateOnly? reviewedDueDate = null) => WithUnitOfWorkAsync(async () =>
	{
		var school = await GetRequiredService<Volo.Abp.TenantManagement.ITenantManager>()
			.CreateAsync($"Library finance school {Guid.NewGuid():N}");
		await GetRequiredService<Volo.Abp.TenantManagement.ITenantRepository>().InsertAsync(school, autoSave: true);
		var tenantId = school.Id;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var actorId = Guid.NewGuid();
		var role = new IdentityRole(Guid.NewGuid(), $"external-finance-{actorId:N}", tenantId);
		await GetRequiredService<IIdentityRoleRepository>().InsertAsync(role, true);
		var actor = new IdentityUser(actorId, $"cashier-{actorId:N}", $"{actorId:N}@example.test", tenantId);
		actor.AddRole(role.Id);
		await GetRequiredService<IIdentityUserRepository>().InsertAsync(actor, true);
		var permissions = GetRequiredService<IPermissionManager>();
		await permissions.SetAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View, "R", role.Name, true);
		await permissions.SetAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View, "R", role.Name, true);
		await permissions.SetAsync(ShikshaMitraSubmodulePermissions.Finance.Auditing.View, "R", role.Name, true);
		if (grantFinanceAdd)
			await permissions.SetAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add, "R", role.Name, true);

		var year = new AcademicYear(Guid.NewGuid(), tenantId, "Library assessment year",
			new DateTime(2026, 4, 1), new DateTime(2027, 3, 31));
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
		Guid? scopeYear = null;
		if (!allowSourceScope)
		{
			var otherYear = new AcademicYear(Guid.NewGuid(), tenantId, "Unrelated year",
				new DateTime(2025, 4, 1), new DateTime(2026, 3, 31));
			await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(otherYear, true);
			scopeYear = otherYear.Id;
		}
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(
			Guid.NewGuid(), tenantId, userId: actorId, academicYearId: scopeYear,
			description: "External receivable test authority"), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actorId);

		Guid personId;
		if (payerKind == FinanceExternalPayerKind.Student)
		{
			var board = new EducationBoard(Guid.NewGuid(), tenantId, EducationBoardType.CBSE, year.Id);
			await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
			var medium = new EducationMedium(Guid.NewGuid(), tenantId, "English", board.Id);
			await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
			var stage = new EducationMediumStage(Guid.NewGuid(), tenantId, "Primary", null, medium.Id);
			await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenantId, "Library", null, "Borrower",
				year.Id, board.Id, medium.Id, stage.Id, null, null,
				new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Female,
				primaryContactNo: "9999999999", email: "borrower@example.test");
			student.ChangeStatus(departed ? StudentLifeCycleStatusEnum.Left : StudentLifeCycleStatusEnum.Enrolled);
			student.AdditionalDetail!.IsLibraryMember = !departed;
			await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
			personId = student.Id;
		}
		else
		{
			var suffix = Guid.NewGuid().ToString("N");
			var employee = new Employee(Guid.NewGuid(), tenantId, null, "LIB" + suffix,
				new EmployeeBasicInfo(ProfilePictureBlobName: null, LastName: "Borrower", FirstName: "Staff",
					MiddleName: null, ShortName: null,
					EmployeeStatusType: departed ? EmployeeStatusType.Inactive : EmployeeStatusType.Active,
					DateOfBirth: null, FamilyCast: null, CastType: null, GenderType: null, Religion: null,
					MaritalStatus: null, BloodGroup: null, MobileNo: null, SecondaryMobileNo: null,
					Email: $"{suffix}@example.test", PanNo: suffix[..10].ToUpperInvariant(), AdharNo: suffix[10..22],
					Address1: null, Address2: null, Address3: null, Department: null, Designation: "Librarian",
					DateOfJoining: null, EmployeeGroup: EmployeeGroupType.NonTeaching, Qualification: null,
					RoleId: role.Id, IdentificationMark1: null, City: null, State: null, Country: null,
					PinCode: null, ShiftId: null), null, null);
			await GetRequiredService<IRepository<Employee, Guid>>().InsertAsync(employee, true);
			personId = employee.Id;
		}

		var libraryCategory = new LibraryChargeCategory(Guid.NewGuid(), tenantId, year.Id,
			"Damage assessment", 100m, false, "Reviewed physical damage");
		await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(libraryCategory, true);
		var dueDate = reviewedDueDate ?? new DateOnly(2026, 11, 24);
		var assessmentDate = dueDate.AddDays(-7);
		var assessment = new LibraryCharge(Guid.NewGuid(), personId,
			payerKind == FinanceExternalPayerKind.Student ? 0 : 1, libraryCategory.Id, null, 100m,
			assessmentDate.ToDateTime(TimeOnly.MinValue), dueDate.ToDateTime(TimeOnly.MinValue), tenantId)
			{ Remarks = "Reviewed physical damage" };
		await GetRequiredService<IRepository<LibraryCharge, Guid>>().InsertAsync(assessment, true);
		var income = new TransactionCategory(Guid.NewGuid(), tenantId, year.Id, TransactionType.Income,
			"Library recoveries", null);
		await GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income, true);
		var categories = new Guid[2];
		for (var i = 0; i < categories.Length; i++)
		{
			var category = new ChargeCategory(Guid.NewGuid(), tenantId, year.Id, $"Library charge {i}", true, null);
			category.SetAccountingMapping(income.Id);
			await GetRequiredService<IRepository<ChargeCategory, Guid>>().InsertAsync(category, true);
			categories[i] = category.Id;
		}
		return new Fixture(tenantId, actorId, year.Id, personId, payerKind, assessment.Id,
			assessment.ConcurrencyStamp, categories[0], categories[1], assessmentDate, dueDate);
	});

	private IDisposable SignIn(Fixture fixture) => GetRequiredService<ICurrentPrincipalAccessor>().Change(
		new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, fixture.ActorId.ToString()),
			new Claim(AbpClaimTypes.TenantId, fixture.TenantId.ToString())], "LibraryFinanceTest")));

	private static PostFinanceExternalReceivableInput Post(Fixture fixture) => new()
	{
		Source = new() { Kind = FinanceExternalSourceKind.LibraryAssessment,
			SourceId = fixture.AssessmentId, SourceVersion = fixture.Version },
		FinanceChargeCategoryId = fixture.FinanceCategoryId, IdempotencyKey = Guid.NewGuid().ToString("N")
	};

	private IFinanceExternalReceivableAppService Service(Fixture fixture, bool useStoredSource = false)
	{
		var services = new FinanceServices(ServiceProvider, fixture, useStoredSource);
		var service = ActivatorUtilities.CreateInstance<FinanceExternalReceivableAppService>(services);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		return service;
	}

	private FinanceAuditTrailAppService AuditService(Fixture fixture)
	{
		var services = new FinanceServices(ServiceProvider, fixture);
		var service = ActivatorUtilities.CreateInstance<FinanceAuditTrailAppService>(services);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		return service;
	}

	private Task<Fixture> AcademicAuditReaderAsync(Fixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		var userId = Guid.NewGuid();
		var role = new IdentityRole(Guid.NewGuid(), $"academic-auditor-{userId:N}", fixture.TenantId);
		await GetRequiredService<IIdentityRoleRepository>().InsertAsync(role, true);
		var user = new IdentityUser(userId, $"auditor-{userId:N}", $"{userId:N}@example.test", fixture.TenantId);
		user.AddRole(role.Id);
		await GetRequiredService<IIdentityUserRepository>().InsertAsync(user, true);
		await GetRequiredService<IPermissionManager>().SetAsync(
			ShikshaMitraSubmodulePermissions.Finance.Auditing.View, "R", role.Name, true);
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
			new UserScopeAssignment(Guid.NewGuid(), fixture.TenantId, userId: userId,
				academicYearId: fixture.YearId, description: "Academic year audit access without schoolwide staff scope"), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
		return fixture with { ActorId = userId };
	});

	private sealed record Fixture(Guid TenantId, Guid ActorId, Guid YearId, Guid PersonId,
		FinanceExternalPayerKind PayerKind, Guid AssessmentId, string Version,
		Guid FinanceCategoryId, Guid OtherFinanceCategoryId, DateOnly AssessmentDate, DateOnly DueDate);

	private sealed class FinanceServices(IServiceProvider inner, Fixture fixture, bool useStoredSource = false) : IServiceProvider
	{
		private readonly IPermissionChecker _checker = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(inner);
		public object? GetService(Type type) => type == typeof(IPermissionChecker) ? _checker
			: type == typeof(IFinanceExternalSourceScope)
				? ActivatorUtilities.CreateInstance<LibraryFinanceSourceScope>(inner, _checker)
			: type == typeof(IFinanceExternalReceivableSourceReader) || type == typeof(IFinanceExternalReceivableSourceBinding)
				? useStoredSource ? ActivatorUtilities.CreateInstance<LibraryFinanceSourceReader>(inner, _checker)
					: new ConfirmedFixtureSourceReader(inner, fixture)
			: inner.GetService(type);
	}

	private sealed class ConfirmedFixtureSourceReader(IServiceProvider provider, Fixture fixture)
		: IFinanceExternalReceivableSourceReader, IFinanceExternalReceivableSourceBinding
	{
		public async Task<FinanceExternalReceivableSourceFacts> ReadAuthorizedAsync(FinanceExternalSourceReference source)
		{
			var charge = await provider.GetRequiredService<IRepository<LibraryCharge, Guid>>().FindAsync(source.SourceId)
				?? throw new BusinessException(LibraryErrorCodes.LibraryChargeNotFound);
			if (charge.TenantId != provider.GetRequiredService<ICurrentTenant>().Id || source.Kind != FinanceExternalSourceKind.LibraryAssessment)
				throw new BusinessException(LibraryErrorCodes.LibraryChargeNotFound);
			await provider.GetRequiredService<ILibraryAccessService>().EnsureMemberAsync(charge.MemberId);
			return new FinanceExternalReceivableSourceFacts(new() { Kind = source.Kind, SourceId = charge.Id,
				SourceVersion = charge.GetAssessmentVersion() }, fixture.TenantId, fixture.YearId,
				new() { Kind = fixture.PayerKind, DisplayName = "Reviewed borrower",
					StudentId = fixture.PayerKind == FinanceExternalPayerKind.Student ? fixture.PersonId : null,
					EmployeeId = fixture.PayerKind == FinanceExternalPayerKind.Employee ? fixture.PersonId : null },
				charge.Amount, "INR", fixture.AssessmentDate, fixture.DueDate, true,
				charge.Remarks!, "Explicit fixture school-date confirmation")
			{
				AcademicYearConfirmed = true,
				AcademicYearEvidenceJson = JsonSerializer.Serialize(new
				{
					AcademicYearId = fixture.YearId, SourceId = charge.Id, ConfirmedBy = fixture.ActorId,
					Provenance = "ExplicitAssessmentContext", Reason = "Explicit fixture transaction-year confirmation"
				})
			};
		}

		public async Task BindAsync(FinanceExternalSourceReference source, Guid sourceLinkId)
		{
			await ReadAuthorizedAsync(source);
			var charges = provider.GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var charge = await charges.GetAsync(source.SourceId);
			var link = await provider.GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>().GetAsync(sourceLinkId);
			if (link.TenantId != fixture.TenantId || link.SourceId != charge.Id || link.SourceVersion != source.SourceVersion)
				throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
			charge.BindFinanceSource(link.Id, link.SourceVersion);
			await charges.UpdateAsync(charge, false);
		}
	}
}
