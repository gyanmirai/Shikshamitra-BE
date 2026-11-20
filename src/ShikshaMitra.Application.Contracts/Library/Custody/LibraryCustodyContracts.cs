using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Custody;

public interface ILibraryCustodyAppService : IApplicationService
{
	Task<LibraryCustodyOptionsDto> GetOptionsAsync(Guid academicYearId);
	Task<List<LibraryCustodyCopyOptionDto>> FindCopiesAsync(string search);
	Task<LibraryCustodyPreviewDto> PreviewHandoffAsync(LibraryCustodyHandoffInput input);
	Task<LibraryCustodyHandoffDto> HandoffAsync(LibraryCustodyHandoffCommand command);
	Task<LibraryCustodyPreviewDto> PreviewReturnAsync(Guid id, LibraryCustodyReturnInput input);
	Task<LibraryCustodyReceiptDto> ReturnAsync(Guid id, LibraryCustodyReturnCommand command);
	Task<LibraryCustodyDetailDto> GetAsync(Guid id);
	Task<PaginatedResponse<LibraryCustodyListItemDto>> GetListAsync(int page = 1, int pageSize = 25);
}

public sealed record LibraryCustodyHandoffInput(Guid AcademicYearId, Guid EmployeeId, Guid? StandardId, Guid? RoomId,
	string Purpose, DateOnly HandedOffOn, DateOnly? ExpectedReturnOn, Guid[] CopyIds);
public sealed record LibraryCustodyHandoffCommand(LibraryCustodyHandoffInput Handoff, string Revision, string IdempotencyKey);
public sealed record LibraryCustodyReturnLineInput(Guid CopyId, BookCopyStatus Condition);
public sealed record LibraryCustodyReturnInput(DateOnly ReceivedOn, LibraryCustodyReturnLineInput[] Copies, string? Notes);
public sealed record LibraryCustodyReturnCommand(LibraryCustodyReturnInput Return, string Revision, string IdempotencyKey);
public sealed class LibraryCustodyLabelDto
{
	public Guid Id { get; init; }
	public string Name { get; init; } = string.Empty;
}
public sealed record LibraryCustodyOptionsDto(List<LibraryCustodyLabelDto> Years, List<LibraryCustodyLabelDto> Employees,
	List<LibraryCustodyLabelDto> Classes, List<LibraryCustodyLabelDto> Rooms);
public sealed record LibraryCustodyCopyOptionDto(Guid CopyId, Guid BookId, string Title, string AccessionNumber, BookCopyStatus Status);
public sealed record LibraryCustodyErrorDto(Guid? CopyId, string Code, string Message);
public sealed record LibraryCustodyPreviewDto(string? Revision, List<LibraryCustodyCopyOptionDto> Copies, List<LibraryCustodyErrorDto> Errors);
public sealed record LibraryCustodyHandoffDto(Guid Id, Guid AcademicYearId, Guid EmployeeId, string Custodian, Guid? StandardId,
	string? ClassName, Guid? RoomId, string? RoomName, string Purpose, DateOnly HandedOffOn, DateOnly? ExpectedReturnOn,
	Guid ActorId, DateTimeOffset RecordedAt, int CopyCount);
public sealed record LibraryCustodyItemDto(Guid CopyId, Guid BookId, string Title, string AccessionNumber,
	Guid? ReturnReceiptId, BookCopyStatus? ReturnedCondition, Guid HomeSectionId, Guid HomeShelfId);
public sealed record LibraryCustodyReceiptDto(Guid Id, Guid HandoffId, DateOnly ReceivedOn, string? Notes,
	Guid ActorId, DateTimeOffset RecordedAt, List<LibraryCustodyItemDto> Copies);
public sealed record LibraryCustodyDetailDto(LibraryCustodyHandoffDto Handoff, int PendingCount,
	List<LibraryCustodyItemDto> Copies, List<LibraryCustodyReceiptDto> Receipts);
public sealed record LibraryCustodyListItemDto(LibraryCustodyHandoffDto Handoff, int PendingCount);
