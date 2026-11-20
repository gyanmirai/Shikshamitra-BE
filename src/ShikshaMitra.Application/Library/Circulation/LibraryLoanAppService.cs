using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Setting.ChargeCategory;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library.Circulation;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
public partial class LibraryLoanAppService(IRepository<BookIssue, Guid> loans, IRepository<BookCopy, Guid> copies,
	ILibraryAccessService access, IPermissionChecker permissions,
	LibraryCopyEventRecorder copyEvents, LibraryReservationEligibilityReader eligibility,
	IRepository<LibraryLoanCommand, Guid> commands, IRepository<LibraryLoanEvent, Guid> events,
	ILibraryInventoryLock inventoryLock, ILibraryIssuePlanAppService plans, LibraryCalendarEvidenceReader calendars,
	LibraryReservationCoordinator reservations, LibraryCirculationMemberReader borrowers,
	IRepository<Book, Guid> books, LibraryNotificationReminderService notifications,
	IRepository<LibraryCharge, Guid> charges, IRepository<LibraryChargeCategory, Guid> chargeCategories,
	IRepository<BookReservation, Guid> reservationRequests, LibraryStaffScopeCaptureService staffCaptures,
	LibraryStoredLoanScope recordedLoanScope, LibrarySchoolContextReader schoolContexts) : ApplicationService, ILibraryLoanAppService
{
	public Task<LibraryLoanDto> GetAsync(Guid loanId) => ReadLoanForGetAsync(loanId);
	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue)]
	[UnitOfWork(true)]
	public Task<LibraryLoanCommandResultDto> CheckoutAsync(LibraryCheckoutRequest input) => CheckoutCoreAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return)]
	[UnitOfWork(true)]
	public Task<LibraryLoanCommandResultDto> ReturnAsync(Guid loanId, LibraryLoanReturnRequest input) => ReturnCoreAsync(loanId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Renew)]
	public Task<LibraryLoanRenewalPlanDto> PreviewRenewalAsync(Guid loanId, LibraryLoanRenewalPlanRequest input) => BuildRenewalPlanAsync(loanId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Renew)]
	[UnitOfWork(true)]
	public Task<LibraryLoanCommandResultDto> RenewAsync(Guid loanId, LibraryLoanRenewRequest input) => RenewCoreAsync(loanId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Library.IssueReturn.ReviewDates)]
	[UnitOfWork(true)]
	public Task<LibraryLoanCommandResultDto> ReviewDatesAsync(Guid loanId, LibraryLoanDateReviewRequest input) => ReviewDatesCoreAsync(loanId, input);
}
