using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Service for sending student-related emails.
/// </summary>
public interface IStudentEmailService
{
    /// <summary>
    /// Sends a welcome email with credentials to the student/parent upon admission approval.
    /// </summary>
    /// <param name="email">Recipient email address</param>
    /// <param name="studentName">Full name of the student</param>
    /// <param name="username">Login username</param>
    /// <param name="password">Login password</param>
    /// <returns>Task representing the async operation</returns>
    Task SendAdmissionApprovalEmailAsync(
        string email,
        string studentName,
        string username,
        string password);
}
