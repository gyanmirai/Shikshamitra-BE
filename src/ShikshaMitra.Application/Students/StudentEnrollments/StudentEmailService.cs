using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;

namespace ShikshaMitra.Students.StudentEnrollments;

/// <summary>
/// Implementation of IStudentEmailService for student communications.
/// </summary>
public class StudentEmailService : IStudentEmailService, ITransientDependency
{
    private readonly IEmailSender emailSender;
    private readonly ILogger<StudentEmailService> logger;

    public StudentEmailService(
        IEmailSender emailSender,
        ILogger<StudentEmailService> logger)
    {
        this.emailSender = emailSender;
        this.logger = logger;
    }

    public async Task SendAdmissionApprovalEmailAsync(
        string email,
        string studentName,
        string username,
        string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                logger.LogWarning("Cannot send admission approval email: Email address is empty for student {StudentName}", studentName);
                return;
            }

            var htmlBody = BuildAdmissionApprovalEmailBody(studentName, username, password);

            await emailSender.SendAsync(
                to: email,
                subject: "Congratulations! Your Admission is Approved - ShikshaMitra",
                body: htmlBody,
                isBodyHtml: true);
                
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send admission approval email to: {Email}, Student: {StudentName}. Error: {ErrorMessage}",
                email,
                studentName,
                ex.Message);
        }
    }

    private string BuildAdmissionApprovalEmailBody(string studentName, string username, string password)
    {
        var encodedUsername = HtmlEncoder.Default.Encode(username);
        var encodedPassword = HtmlEncoder.Default.Encode(password);

        return $@"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <style>
                body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }}
                .container {{ max-width: 600px; margin: 20px auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; background-color: #ffffff; }}
                .header {{ background-color: #27ae60; color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
                .content {{ padding: 30px; border: 1px solid #f0f0f0; border-top: none; }}
                .credentials {{ background-color: #f1f8f3; padding: 20px; margin: 20px 0; border-left: 5px solid #27ae60; border-radius: 4px; }}
                .label {{ font-weight: bold; display: inline-block; width: 100px; }}
                .value {{ color: #27ae60; font-family: 'Courier New', monospace; font-weight: bold; font-size: 1.1em; }}
                .action-box {{ text-align: center; margin-top: 30px; }}
                .button {{ background-color: #27ae60; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; font-weight: bold; }}
                .footer {{ margin-top: 30px; font-size: 12px; color: #7f8c8d; text-align: center; border-top: 1px solid #eee; padding-top: 20px; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='header'>
                    <h1 style='margin:0;'>Admission Approved!</h1>
                </div>
                <div class='content'>
                    <p>Dear <strong>{HtmlEncoder.Default.Encode(studentName)}</strong>,</p>
                    <p>We are delighted to inform you that your admission to <strong>ShikshaMitra</strong> has been officially approved! We are excited to have you join our academic community.</p>
                    
                    <p>Your student account has been created. You can now access the student portal using the following credentials:</p>
            
                    <div class='credentials'>
                        <p style='margin: 5px 0;'><span class='label'>Username:</span> <span class='value'>{encodedUsername}</span></p>
                        <p style='margin: 5px 0;'><span class='label'>Password:</span> <span class='value'>{encodedPassword}</span></p>
                    </div>
            
                    <p><strong>Note:</strong> For security reasons, please change your password immediately upon your first login.</p>
            
                    <div class='action-box'>
                        <a href='#' class='button'>Login to Student Portal</a>
                    </div>
            
                    <p style='margin-top: 30px;'>If you have any questions, please contact the admissions department.</p>
            
                    <p>Warm regards,<br/>
                    <strong>The Admissions Team<br/>ShikshaMitra</strong></p>
                </div>
                <div class='footer'>
                    <p>&copy; {DateTime.Now.Year} ShikshaMitra. All rights reserved.</p>
                    <p>This is an automated message generated by the system. Please do not reply to this email.</p>
                </div>
            </div>
        </body>
        </html>";
    }
}
