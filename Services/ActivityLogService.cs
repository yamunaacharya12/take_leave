using System.Threading.Tasks;
using Take_Leave.Data;   // ASSUMPTION: adjust to your actual namespace for ApplicationDbContext
using Take_Leave.Models;

namespace Take_Leave.Services
{
    // ASSUMPTION: your EF Core DbContext class is called ApplicationDbContext.
    // If it's named differently (e.g. AppDbContext, TakeLeaveContext), rename
    // every "ApplicationDbContext" below to match.
    public class ActivityLogService : IActivityLogService
    {
        private readonly ApplicationDbContext _context;

        public ActivityLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(
            string category,
            string message,
            ActivityLevel level = ActivityLevel.Info,
            string performedByName = "Admin",
            string? performedByUserId = null,
            int? leaveRequestId = null,
            string? recipientUserId = null)
        {
            var log = new ActivityLog
            {
                Category = category,
                Message = message,
                Level = level,
                PerformedByName = performedByName,
                PerformedByUserId = performedByUserId,
                LeaveRequestId = leaveRequestId,
                RecipientUserId = recipientUserId,
                CreatedDate = DateTime.Now
            };

            _context.ActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
