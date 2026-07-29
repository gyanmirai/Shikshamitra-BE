using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Service for sending student-related emails.
/// </summary>
public interface IStudentEmailService
{
    /// <summary>
    /// Sends a welcome email with a one-time account setup link upon admission approval.
    /// </summary>
    /// <param name="email">Recipient email address</param>
    /// <param name="studentName">Full name of the student</param>
    /// <param name="username">Login username</param>
    /// <param name="userId">Identity user identifier</param>
    /// <param name="resetToken">One-time password reset token</param>
    /// <param name="tenantId">Tenant identifier used by the public reset-password route</param>
    /// <returns>Task representing the async operation</returns>
    Task SendAdmissionAccountSetupEmailAsync(
        string email,
        string studentName,
        string username,
        Guid userId,
        string resetToken,
        Guid? tenantId);
}
