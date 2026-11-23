using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Records;

public interface IStudentRecordAppAccessAppService : IApplicationService
{
	Task<StudentRecordAppAccessDto> GetAsync(Guid studentId);
	Task<StudentRecordAppAccessDto> UpdateAsync(Guid studentId, string recipientKey, UpdateStudentRecordAppAccessDto input);
}

public sealed record StudentRecordAppAccessDto(Guid StudentId, IReadOnlyList<StudentAppAccessRecipientDto> Recipients,
	IReadOnlyList<StudentAppAccessHistoryDto> History);

public sealed record StudentAppAccessRecipientDto(string RecipientKey, string Relationship, string? DisplayName,
	string Status, bool CanUpdate, string ConcurrencyStamp);

public sealed record StudentAppAccessHistoryDto(Guid Id, string Relationship, string? DisplayName, bool Enabled,
	string Reason, DateTime ChangedAt, string? ChangedByName);

public sealed class UpdateStudentRecordAppAccessDto
{
	public required bool Enabled { get; init; }
	public required string Reason { get; init; }
	public required string ConcurrencyStamp { get; init; }
}
