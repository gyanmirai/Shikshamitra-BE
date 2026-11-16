using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.HomeworkAssignment;

internal sealed class EvaluationSubmissionRead
{
	public Guid Id { get; init; }
	public Guid StudentId { get; init; }
	public Guid? TenantId { get; init; }
	public DateTimeOffset? SubmissionDate { get; init; }
	public DateTime CreationTime { get; init; }
	public SubmissionStatus Status { get; init; }
	public decimal? Marks { get; init; }
	public decimal? TotalMarks { get; init; }
	public string? Feedback { get; init; }
	public string? Remarks { get; init; }
	public string? SubmittedContent { get; init; }
	public string? SubmittedDocumentUrl { get; init; }
	public string? OriginalFileName { get; init; }
	public string? MobileAttachmentsJson { get; init; }

	internal static async Task<IQueryable<StudentSubmissionEntity>> KeepLatestAsync(
		IQueryable<StudentSubmissionEntity> selectedStudentQuery, IAsyncQueryableExecuter asyncExecuter)
	{
		var candidates = await asyncExecuter.ToListAsync(selectedStudentQuery.Select(row => new
		{
			row.Id, row.SubmissionDate, row.CreationTime
		}));
		var latestId = candidates.OrderByDescending(row => row.SubmissionDate ?? DateTimeOffset.MinValue)
			.ThenByDescending(row => row.CreationTime).FirstOrDefault()?.Id;
		return selectedStudentQuery.Where(row => latestId.HasValue && row.Id == latestId.Value).Take(1);
	}

	internal static IQueryable<EvaluationSubmissionRead> Project(IQueryable<StudentSubmissionEntity> query, bool lightweight)
	{
		if (lightweight) return query.Select(row => new EvaluationSubmissionRead
		{
			Id = row.Id, StudentId = row.StudentId, TenantId = row.TenantId,
			SubmissionDate = row.SubmissionDate, CreationTime = row.CreationTime, Status = row.Status,
			Marks = row.Marks, TotalMarks = row.TotalMarks, Feedback = row.Feedback, Remarks = row.Remarks
		});
		return query.Select(row => new EvaluationSubmissionRead
		{
			Id = row.Id, StudentId = row.StudentId, TenantId = row.TenantId,
			SubmissionDate = row.SubmissionDate, CreationTime = row.CreationTime, Status = row.Status,
			Marks = row.Marks, TotalMarks = row.TotalMarks, Feedback = row.Feedback, Remarks = row.Remarks,
			SubmittedContent = row.SubmittedContent, SubmittedDocumentUrl = row.SubmittedDocumentUrl,
			OriginalFileName = row.OriginalFileName, MobileAttachmentsJson = row.MobileAttachmentsJson
		});
	}
}
