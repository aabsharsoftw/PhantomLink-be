using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PhantomPulse.Infrastructure.Realtime;

[Authorize]
public class InboxHub : Hub
{
    public async Task JoinTenant(string tenantId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, tenantId);
}
