using Microsoft.AspNetCore.SignalR;

namespace WorkManagementSystem.API.Hubs
{
    public class DiscussionHub : Hub
    {
        public async Task JoinTaskGroup(Guid taskId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, taskId.ToString());
        }

        public async Task LeaveTaskGroup(Guid taskId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, taskId.ToString());
        }
    }
}
