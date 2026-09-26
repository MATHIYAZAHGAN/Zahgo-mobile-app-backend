using Microsoft.AspNetCore.SignalR;

namespace ZahSellerAI.API.Hubs;

public interface IAIProcessingClient
{
    Task ProcessingStarted(string jobId, string productId);
    Task ProcessingProgress(string jobId, string currentStep, int progress);
    Task BackgroundRemovalCompleted(string jobId, string backgroundRemovedImageUrl);
    Task ImageGenerationCompleted(string jobId, string mainProductImageUrl);
    Task CatalogGenerationCompleted(string jobId, object catalogData);
    Task ProcessingCompleted(string jobId, object finalProductResult);
    Task ProcessingFailed(string jobId, string errorMessage);
}

public class AIProcessingHub : Hub<IAIProcessingClient>
{
    public async Task JoinJobGroup(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"job_{jobId}");
    }

    public async Task LeaveJobGroup(string jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"job_{jobId}");
    }
}
