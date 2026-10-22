using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShikshaMitra.Academic.Mobile;
using Volo.Abp;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	private async Task<int> SaveSubmissionChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
	{
		try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
		catch (DbUpdateException exception) when (exception.InnerException is PostgresException {
			SqlState: PostgresErrorCodes.UniqueViolation,
			ConstraintName: "IX_StudentSubmissions_Student_Homework" or "IX_StudentSubmissions_Student_Assignment" })
		{
			// Includes older upload endpoints racing with a mobile first submission.
			throw new BusinessException(MobileSubmissionErrorCodes.SubmissionConflict, innerException: exception);
		}
	}
}
