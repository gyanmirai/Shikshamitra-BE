using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Charges;

[ExposeServices(typeof(IFinanceExternalReceivableSourceReader), typeof(IFinanceExternalReceivableSourceBinding))]
public partial class LibraryFinanceSourceReader(
	IRepository<LibraryCharge, Guid> charges,
	IRepository<LibraryMember, Guid> memberships,
	IRepository<LibraryChargeCategory, Guid> categories,
	IRepository<FinanceExternalReceivableSourceLink, Guid> links,
	ILibraryAccessService access,
	LibraryStoredLoanScope storedLoans,
	IUserScopeAccessService scopeAccess,
	IRepository<Employee, Guid> employees,
	IRepository<Student, Guid> students,
	IPermissionChecker permissions,
	ICurrentTenant tenant,
	IAsyncQueryableExecuter queries)
	: IFinanceExternalReceivableSourceReader, IFinanceExternalReceivableSourceBinding, ITransientDependency
{
	public async Task<FinanceExternalReceivableSourceFacts> ReadAuthorizedAsync(FinanceExternalSourceReference source)
	{
		FinanceExternalReceivableSourceGuard.ValidateReference(source);
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View))
			throw new AbpAuthorizationException("Library assessment access denied.");
		var charge = await charges.FindAsync(source.SourceId, includeDetails: false);
		if (charge == null || !tenant.Id.HasValue || charge.TenantId != tenant.Id)
			throw new BusinessException(LibraryErrorCodes.LibraryChargeNotFound);
		var payer = await ReadPayerAsync(charge);
		if (charge.PaymentStatus != ChargePaymentStatus.Pending && !charge.FinanceSourceLinkId.HasValue)
			throw new BusinessException(LibraryFinanceErrorCodes.LegacySettlementRequiresReview,
				"Preserve this legacy settlement declaration. Reconcile actual Finance evidence before linking it.");
		var (reason, reasonOrigin) = await ReadReasonAsync(charge);
		return new FinanceExternalReceivableSourceFacts(new()
		{
			Kind = FinanceExternalSourceKind.LibraryAssessment, SourceId = charge.Id,
			SourceVersion = charge.GetAssessmentVersion()
		}, tenant.Id.Value, charge.AcademicYearId ?? Guid.Empty, payer, charge.Amount, charge.Currency ?? string.Empty,
			charge.SchoolChargeDate ?? default, charge.SchoolDueDate ?? default,
			Confirmed(charge.ChargeDateProvenance) && Confirmed(charge.DueDateProvenance) &&
			charge.SchoolChargeDate.HasValue && charge.SchoolDueDate.HasValue,
			reason, charge.SourceDateEvidenceJson ?? string.Empty)
		{
			AcademicYearConfirmed = charge.AcademicYearId.HasValue &&
				charge.AcademicYearProvenance != LibraryAcademicYearProvenance.Unconfirmed &&
				Enum.IsDefined(charge.AcademicYearProvenance),
			AcademicYearEvidenceJson = charge.AcademicYearEvidenceJson ?? string.Empty,
			ReasonOrigin = reasonOrigin
		};
	}

	public async Task BindAsync(FinanceExternalSourceReference source, Guid sourceLinkId)
	{
		FinanceExternalReceivableSourceGuard.ValidateReference(source);
		var link = await links.FindAsync(sourceLinkId, includeDetails: false);
		if (link == null || link.TenantId != tenant.Id || link.SourceKind != source.Kind ||
			link.SourceId != source.SourceId || link.SourceVersion != source.SourceVersion)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		var facts = await ReadAuthorizedAsync(source);
		var accepted = facts with { Source = new()
		{
			Kind = link.SourceKind, SourceId = link.SourceId, SourceVersion = link.SourceVersion
		} };
		FinanceExternalReceivableSourceGuard.ValidateFacts(source, accepted, tenant.Id);
		if (FinanceExternalReceivableFingerprint.Source(accepted) != link.SourceFingerprint)
			throw new BusinessException(LibraryFinanceErrorCodes.SourceChanged);
		var charge = await charges.GetAsync(source.SourceId, includeDetails: false);
		charge.BindFinanceSource(link.Id, link.SourceVersion);
		await charges.UpdateAsync(charge, autoSave: false);
	}

	private async Task<FinanceExternalPayerDto> ReadPayerAsync(LibraryCharge charge)
	{
		if (charge.MemberRole == 0)
		{
			var memberId = charge.MemberId;
			var aliases = await memberships.GetQueryableAsync();
			var aliasStudentId = await queries.FirstOrDefaultAsync(aliases
				.Where(row => row.TenantId == tenant.Id && row.Id == memberId).Select(row => (Guid?)row.StudentId));
			var studentId = aliasStudentId ?? memberId;
			IQueryable<Student> readableStudents;
			if (charge.BookIssueId.HasValue)
			{
				var scope = await scopeAccess.GetCurrentUserScopeAsync();
				var readableLoans = await storedLoans.GetReadableLoansAsync(scope);
				if (!await queries.AnyAsync(readableLoans.Where(loan => loan.Id == charge.BookIssueId.Value &&
					loan.TenantId == charge.TenantId && loan.MemberRole == 0 &&
					(loan.MemberId == studentId || aliases.Any(alias => alias.TenantId == tenant.Id &&
						alias.Id == loan.MemberId && alias.StudentId == studentId)))))
					throw new BusinessException(UserScopeErrorCodes.AccessDenied);
				readableStudents = (await students.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
			}
			else readableStudents = await access.GetStudentsAsync();
			var student = await queries.FirstOrDefaultAsync(readableStudents
				.Where(row => row.Id == studentId)
				.Select(row => new { row.Id, row.FirstName, row.MiddleName, row.LastName }));
			if (student == null && !charge.BookIssueId.HasValue)
				throw new BusinessException(UserScopeErrorCodes.AccessDenied);
			return new() { Kind = FinanceExternalPayerKind.Student, StudentId = studentId,
				DisplayName = student == null ? string.Empty : Name(student.FirstName, student.MiddleName, student.LastName) };
		}
		if (charge.MemberRole is 1 or 2)
		{
			IQueryable<Employee> readableEmployees;
			if (charge.BookIssueId.HasValue)
			{
				var scope = await scopeAccess.GetCurrentUserScopeAsync();
				var readableLoans = await storedLoans.GetReadableLoansAsync(scope);
				if (!await queries.AnyAsync(readableLoans.Where(loan =>
					loan.Id == charge.BookIssueId.Value && loan.TenantId == charge.TenantId &&
					loan.MemberRole == 1 && loan.MemberId == charge.MemberId)))
					throw new BusinessException(UserScopeErrorCodes.AccessDenied);
				readableEmployees = (await employees.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
			}
			else readableEmployees = await access.GetEmployeesAsync();
			var employee = await queries.FirstOrDefaultAsync(readableEmployees
				.Where(row => row.Id == charge.MemberId)
				.Select(row => new { row.Id, row.FirstName, row.MiddleName, row.LastName }));
			if (employee == null && !charge.BookIssueId.HasValue)
				throw new BusinessException(UserScopeErrorCodes.AccessDenied);
			return new() { Kind = FinanceExternalPayerKind.Employee, EmployeeId = charge.MemberId,
				DisplayName = employee == null ? string.Empty : Name(employee.FirstName, employee.MiddleName, employee.LastName) };
		}
		throw new BusinessException(LibraryFinanceErrorCodes.InvalidPayer);
	}

	private static bool Confirmed(LibraryCivilDateProvenance provenance) => provenance is
		LibraryCivilDateProvenance.ExplicitSchoolDate or LibraryCivilDateProvenance.ReviewedLegacy;
	private static string Name(params string?[] parts) => string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
}
