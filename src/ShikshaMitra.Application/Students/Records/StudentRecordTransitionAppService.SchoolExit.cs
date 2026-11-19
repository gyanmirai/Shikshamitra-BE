using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

public partial class StudentRecordTransitionAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.View)]
	public async Task<StudentSchoolExitSummaryDto> GetSchoolExitSummaryAsync(Guid studentId, Guid academicYearId, string outcome = "Withdraw", DateTime? effectiveDate = null)
	{
		if (!StudentSchoolExitOutstanding.IsExit(outcome)) throw new BusinessException("Students:Records:InvalidTransition");
		var record = await access.EnsureReadableAsync(studentId);
		var source = await readiness.GetSourceAsync(studentId, academicYearId, writable: false);
		if (!await AsyncExecuter.AnyAsync((await enrollmentScope.GetQueryableAsync()).Where(x => x.Id == source.Enrollment.Id))) throw new Volo.Abp.Authorization.AbpAuthorizationException();
		var policy = (await policies.ResolveAsync(academicYearId)).Policy;
		var roles = await access.GetDatabaseRoleNamesAsync();
		var masked = record.Policy.IsMasked("Identity", roles) || policy.IsMasked("Identity", roles);
		var result = new StudentSchoolExitSummaryDto
		{
			StudentId = studentId, AcademicYearId = academicYearId,
			StudentName = masked ? null : string.Join(" ", new[] { source.Student.FirstName, source.Student.MiddleName, source.Student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
			StudentConcurrencyStamp = source.Student.ConcurrencyStamp, EnrollmentConcurrencyStamp = source.Enrollment.ConcurrencyStamp,
			CanSubmit = record.Access == "Full" && record.PlacementEditable && source.Enrollment.IsActive && source.Student.Status != StudentLifeCycleStatusEnum.Left && await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)
		};
		if (await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View))
		{
			try
			{
				var clearance = await LazyServiceProvider.LazyGetRequiredService<StudentTransitionFinanceClearanceReader>().GetAsync(studentId, academicYearId,
					outcome == "Withdraw" ? effectiveDate ?? AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now) : null, policy.Transitions.OutcomeChecks[outcome].RequireFinanceClearance, true);
				result.Fees = new() { Status = clearance.AssignmentMissing || !clearance.Cleared && !clearance.OutstandingOnly ? "Unavailable" : clearance.OutstandingOnly ? "Pending" : "Clear", Detail = clearance.Detail, OutstandingAmount = clearance.OutstandingAmount };
			}
			catch (BusinessException) { result.Fees = new() { Status = "Unavailable", Detail = "Finance evidence must be reviewed in Finance before the balance can be shown." }; }
			catch (Volo.Abp.Authorization.AbpAuthorizationException) { result.Fees = new() { Status = "Restricted", Detail = "Finance access is restricted for this student." }; }
		}
		else result.Fees = new() { Status = "Restricted", Detail = "Finance access is required to view amounts. Exit readiness is checked separately." };
		var memberQuery = await LazyServiceProvider.LazyGetRequiredService<IRepository<LibraryMember, Guid>>().GetQueryableAsync();
		var issueQuery = await LazyServiceProvider.LazyGetRequiredService<IRepository<BookIssue, Guid>>().GetQueryableAsync();
		var count = await AsyncExecuter.CountAsync(issueQuery.Where(x => x.TenantId == CurrentTenant.Id && x.MemberRole == 0 && x.Status != BookIssueStatus.Returned &&
			(x.MemberId == studentId || memberQuery.Any(m => m.TenantId == CurrentTenant.Id && m.Id == x.MemberId && m.StudentId == studentId))));
		result.Library = new() { Status = count > 0 ? "Pending" : "Clear", PendingCount = count, Detail = count > 0 ? "Outstanding books remain in Library; exit approval does not return them." : "No outstanding book issues are recorded." };
		if (!masked && await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View))
		{
			var documents = await LazyServiceProvider.LazyGetRequiredService<StudentDocumentAppService>().GetDocumentListAsync(studentId);
			result.Documents = new() { Status = documents.Count > 0 ? "Available" : "NotApplicable", Detail = "Uploaded document evidence only. Physical original return is not tracked here.",
				Items = documents.Select(x => new StudentSchoolExitDocumentDto { Name = x.DocumentTypeName, Status = x.IsUploaded ? x.StorageStatus.ToString() : "NotUploaded" }).ToList() };
		}
		else result.Documents = new() { Status = "Restricted", Detail = "Student document access is restricted." };
		var pending = await transitions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == academicYearId &&
			(x.Outcome == "Withdraw" || x.Outcome == "Transfer" || x.Outcome == "Graduate") && (x.Status == "Submitted" || x.Status == "Approved" || x.Status == "Scheduled" || x.Status == "Exception"));
		foreach (var item in pending.OrderByDescending(x => x.CreationTime)) result.PendingTransitions.Add(await MapAsync(await GetScopedAsync(item.Id)));
		return result;
	}
}
