using Microsoft.EntityFrameworkCore;
using Take_Leave.Data;
using Take_Leave.Models;

namespace Take_Leave.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IEmailService _emailService;
        private readonly IWhatsAppService _whatsAppService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IEmailService emailService,
            IWhatsAppService whatsAppService,
            ApplicationDbContext context,
            ILogger<NotificationService> logger)
        {
            _emailService = emailService;
            _whatsAppService = whatsAppService;
            _context = context;
            _logger = logger;
        }

        // Real implementation — replaces the old no-op stub. Fails silently
        // (just logs) if the number is missing or Twilio isn't configured,
        // so a WhatsApp failure never blocks the rest of the notification flow.
        public async Task SendWhatsAppAsync(string mobileNumber, string message)
        {
            if (string.IsNullOrWhiteSpace(mobileNumber))
            {
                _logger.LogWarning("WhatsApp message skipped — no phone number on file.");
                return;
            }

            bool sent = await _whatsAppService.SendWhatsAppMessageAsync(mobileNumber, message);
            if (!sent)
            {
                _logger.LogWarning("WhatsApp message to {Number} could not be sent.", mobileNumber);
            }
        }

        public async Task NotifyLeaveApplicationAsync(LeaveRequest leaveRequest)
        {
            var user = await _context.Users.FindAsync(leaveRequest.UserId);
            if (user == null) return;

            var subject = "Leave Application Submitted";
            var body = $@"
                <h2>Leave Application Submitted Successfully</h2>
                <p>Dear {user.FullName},</p>
                <p>Your leave application has been submitted successfully.</p>
                <p><strong>Details:</strong></p>
                <ul>
                    <li>Type: {leaveRequest.LeaveType}</li>
                    <li>From: {leaveRequest.StartDate:MMMM dd, yyyy}</li>
                    <li>To: {leaveRequest.EndDate:MMMM dd, yyyy}</li>
                    <li>Days: {leaveRequest.TotalDays}</li>
                    <li>Reason: {leaveRequest.Reason}</li>
                </ul>
                <p>Your application is pending approval from your Department Head.</p>
                <p>You will receive notifications as it progresses.</p>
                <hr/>
                <p>Texas College Leave Management System</p>";

            await SendNotificationAsync(user, subject, body, leaveRequest.Id);
        }

        public async Task NotifyDepartmentHeadAsync(LeaveRequest leaveRequest)
        {
            var user = await _context.Users.FindAsync(leaveRequest.UserId);
            var department = await _context.Departments.FindAsync(int.Parse(leaveRequest.DepartmentId));
            var departmentHead = department?.HeadUserId != null
                ? await _context.Users.FindAsync(department.HeadUserId)
                : null;

            if (departmentHead == null)
            {
                _logger.LogWarning("No department head assigned for department {DeptId} — leave request {LeaveId} was not routed for approval notification.", leaveRequest.DepartmentId, leaveRequest.Id);
                return;
            }

            var subject = $"New Leave Application - {user?.FullName} ({department?.Name})";
            var body = $@"
                <h2>New Leave Application Needs Your Review</h2>
                <p>Dear Department Head,</p>
                <p><strong>Employee:</strong> {user?.FullName} ({user?.EmployeeId})</p>
                <p><strong>Department:</strong> {department?.Name}</p>
                <p><strong>Leave Details:</strong></p>
                <ul>
                    <li>Type: {leaveRequest.LeaveType}</li>
                    <li>From: {leaveRequest.StartDate:MMMM dd, yyyy}</li>
                    <li>To: {leaveRequest.EndDate:MMMM dd, yyyy}</li>
                    <li>Days: {leaveRequest.TotalDays}</li>
                    <li>Reason: {leaveRequest.Reason}</li>
                </ul>
                <p>Please review this application in the system.</p>
                <a href='https://yourdomain.com/DepartmentHead/ReviewLeave/{leaveRequest.Id}'>Review Application</a>";

            await SendNotificationAsync(departmentHead, subject, body, leaveRequest.Id);

            // WhatsApp — this is the urgent-path notification: department heads
            // may not check email as fast as WhatsApp, so this is sent
            // alongside the email above, not instead of it.
            string whatsAppMessage = $"New leave request from {user?.FullName} ({leaveRequest.LeaveType}, " +
                $"{leaveRequest.StartDate:MMM d} - {leaveRequest.EndDate:MMM d}, {leaveRequest.TotalDays} day(s)) " +
                $"is awaiting your approval. Please review it in the Leave Management System.";

            await SendWhatsAppAsync(departmentHead.PhoneNumber, whatsAppMessage);
        }

        public async Task NotifyAdminAsync(LeaveRequest leaveRequest)
        {
            var user = await _context.Users.FindAsync(leaveRequest.UserId);
            var admins = await _context.Users.Where(u => u.UserRole == UserRole.Admin).ToListAsync();

            var subject = $"Leave Application Awaiting Final Approval - {user?.FullName}";
            var body = $@"
                <h2>Leave Application Pending Final Approval</h2>
                <p>Dear Admin,</p>
                <p><strong>Employee:</strong> {user?.FullName} ({user?.EmployeeId})</p>
                <p><strong>Department:</strong> {leaveRequest.DepartmentId}</p>
                <p><strong>Status:</strong> Approved by Department Head</p>
                <p><strong>Leave Details:</strong></p>
                <ul>
                    <li>Type: {leaveRequest.LeaveType}</li>
                    <li>From: {leaveRequest.StartDate:MMMM dd, yyyy}</li>
                    <li>To: {leaveRequest.EndDate:MMMM dd, yyyy}</li>
                    <li>Days: {leaveRequest.TotalDays}</li>
                </ul>
                <p>Please review and take final action.</p>
                <a href='https://yourdomain.com/Admin/ReviewLeave/{leaveRequest.Id}'>Review Application</a>";

            foreach (var admin in admins)
            {
                await SendNotificationAsync(admin, subject, body, leaveRequest.Id);
            }
        }

        public async Task NotifyStatusChangeAsync(LeaveRequest leaveRequest)
        {
            var user = await _context.Users.FindAsync(leaveRequest.UserId);
            if (user == null) return;

            var statusMessage = leaveRequest.Status switch
            {
                LeaveStatus.ApprovedByDepartmentHead => "approved by your Department Head and is now pending final admin approval",
                LeaveStatus.Approved => "approved. Your leave has been granted",
                LeaveStatus.RejectedByDepartmentHead => "rejected by your Department Head",
                LeaveStatus.Rejected => "rejected by Admin",
                _ => "updated"
            };

            var comments = leaveRequest.Status.ToString().Contains("Rejected")
                ? leaveRequest.DepartmentHeadComments ?? leaveRequest.AdminComments
                : null;

            var subject = $"Leave Application {leaveRequest.Status}";
            var body = $@"
                <h2>Leave Application Status Update</h2>
                <p>Dear {user.FullName},</p>
                <p>Your leave application has been {statusMessage}.</p>
                <p><strong>Leave Details:</strong></p>
                <ul>
                    <li>Type: {leaveRequest.LeaveType}</li>
                    <li>From: {leaveRequest.StartDate:MMMM dd, yyyy}</li>
                    <li>To: {leaveRequest.EndDate:MMMM dd, yyyy}</li>
                    <li>Days: {leaveRequest.TotalDays}</li>
                </ul>
                {(string.IsNullOrEmpty(comments) ? "" : $"<p><strong>Comments:</strong> {comments}</p>")}
                <p>Thank you for using Texas College Leave Management System.</p>";

            await SendNotificationAsync(user, subject, body, leaveRequest.Id);

            // WhatsApp — staff get an immediate ping the moment their status changes,
            // rather than waiting on them to check email.
            string whatsAppMessage = $"Your leave request ({leaveRequest.LeaveType}, " +
                $"{leaveRequest.StartDate:MMM d} - {leaveRequest.EndDate:MMM d}) has been {statusMessage}." +
                (string.IsNullOrEmpty(comments) ? "" : $" Comments: {comments}");

            await SendWhatsAppAsync(user.PhoneNumber, whatsAppMessage);
        }

        public async Task NotifyLeaveBalanceAlertAsync(ApplicationUser user, LeaveType leaveType)
        {
            var balance = await _context.LeaveBalances
                .FirstOrDefaultAsync(b => b.UserId == user.Id && b.LeaveType == leaveType && b.Year == DateTime.Now.Year);

            if (balance == null || balance.RemainingQuota > 5) return;

            var subject = $"Low Leave Balance Alert - {leaveType} Leave";
            var body = $@"
                <h2>Low Leave Balance Alert</h2>
                <p>Dear {user.FullName},</p>
                <p>Your {leaveType} leave balance is running low:</p>
                <ul>
                    <li>Remaining: {balance.RemainingQuota} days</li>
                    <li>Used: {balance.UsedQuota} days</li>
                    <li>Total Quota: {balance.TotalQuota} days</li>
                </ul>
                <p>Please plan your leaves accordingly.</p>";

            await SendNotificationAsync(user, subject, body, null);
        }

        public async Task SendDailyDigestAsync()
        {
            var today = DateTime.Today;
            var pendingLeaves = await _context.LeaveRequests
                .Include(l => l.User)
                .Where(l => l.Status == LeaveStatus.Pending && l.RequestDate.Date == today)
                .ToListAsync();

            if (!pendingLeaves.Any()) return;

            var admins = await _context.Users.Where(u => u.UserRole == UserRole.Admin).ToListAsync();

            foreach (var admin in admins)
            {
                var body = GenerateDailyDigestHtml(pendingLeaves);
                await SendNotificationAsync(admin, "Daily Leave Applications Digest", body, null);
            }
        }

        public async Task SendWeeklyReportAsync()
        {
            var startDate = DateTime.Now.AddDays(-7);
            var weeklyLeaves = await _context.LeaveRequests
                .Include(l => l.User)
                .Where(l => l.RequestDate >= startDate)
                .ToListAsync();

            var admins = await _context.Users.Where(u => u.UserRole == UserRole.Admin).ToListAsync();

            foreach (var admin in admins)
            {
                var body = GenerateWeeklyReportHtml(weeklyLeaves, startDate);
                await SendNotificationAsync(admin, "Weekly Leave Report", body, null);
            }
        }

        private async Task SendNotificationAsync(ApplicationUser user, string subject, string body, int? leaveRequestId)
        {
            // Direct email routing eliminates structural channel filtering rules
            bool emailSent = await _emailService.SendEmailAsync(user.Email, subject, body);

            if (leaveRequestId.HasValue)
            {
                var log = new NotificationLog
                {
                    LeaveRequestId = leaveRequestId.Value,
                    RecipientUserId = user.Id,
                    Channel = NotificationChannel.Email, // Logs are locked into Email type natively
                    Subject = subject,
                    Message = body,
                    IsSent = emailSent,
                    SentDate = emailSent ? DateTime.UtcNow : null,
                    CreatedDate = DateTime.UtcNow
                };

                _context.NotificationLogs.Add(log);
                await _context.SaveChangesAsync();
            }
        }

        private string GenerateDailyDigestHtml(List<LeaveRequest> leaves)
        {
            var html = "<h2>Daily Leave Applications Digest</h2><table border='1' cellpadding='10'><thead><tr><th>Employee</th><th>Department</th><th>Leave Type</th><th>Duration</th><th>Days</th></tr></thead><tbody>";

            foreach (var leave in leaves)
            {
                html += $@"
                    <tr>
                        <td>{leave.User?.FullName}</td>
                        <td>{leave.DepartmentId}</td>
                        <td>{leave.LeaveType}</td>
                        <td>{leave.StartDate:MMM dd} - {leave.EndDate:MMM dd}</td>
                        <td>{leave.TotalDays}</td>
                    </tr>";
            }

            html += "</tbody></table><p>Please review these applications in the system.</p>";
            return html;
        }

        private string GenerateWeeklyReportHtml(List<LeaveRequest> leaves, DateTime startDate)
        {
            var approved = leaves.Count(l => l.Status == LeaveStatus.Approved);
            var rejected = leaves.Count(l => l.Status == LeaveStatus.Rejected);
            var pending = leaves.Count(l => l.Status == LeaveStatus.Pending);

            var html = $@"
                <h2>Weekly Leave Report ({startDate:MMMM dd} - {DateTime.Now:MMMM dd, yyyy})</h2>
                <h3>Summary</h3>
                <ul>
                    <li>Total Applications: {leaves.Count}</li>
                    <li>Approved: {approved}</li>
                    <li>Rejected: {rejected}</li>
                    <li>Pending: {pending}</li>
                </ul>
                <h3>Detailed Report</h3>
                <table border='1' cellpadding='10'>
                    <thead><tr><th>Employee</th><th>Department</th><th>Type</th><th>Duration</th><th>Days</th><th>Status</th></tr></thead>
                    <tbody>";

            foreach (var leave in leaves)
            {
                html += $@"
                    <tr>
                        <td>{leave.User?.FullName}</td>
                        <td>{leave.DepartmentId}</td>
                        <td>{leave.LeaveType}</td>
                        <td>{leave.StartDate:MMM dd} - {leave.EndDate:MMM dd}</td>
                        <td>{leave.TotalDays}</td>
                        <td>{leave.Status}</td>
                    </tr>";
            }

            html += "</tbody></table>";
            return html;
        }
    }
}