using Microsoft.AspNetCore.SignalR;

namespace Valuator;

public class RankHub : Hub
{
    public async Task Send(string textId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, textId);
    }
}