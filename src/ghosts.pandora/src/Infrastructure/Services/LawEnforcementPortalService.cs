using Microsoft.EntityFrameworkCore;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface ILawEnforcementPortalService
{
    Task<LawEnforcementRequest> CreateRequestAsync(string requestingAgency, string caseNumber,
        string requestType, string subject, string details);
    Task<List<LawEnforcementRequest>> GetAllRequestsAsync();
    Task<LawEnforcementRequest> GetRequestByIdAsync(int id);
    Task<LawEnforcementRequest> UpdateRequestStatusAsync(int id, string status, string response = null);
    Task<bool> DeleteRequestAsync(int id);
}

public class LawEnforcementPortalService(DataContext context) : ILawEnforcementPortalService
{
    public async Task<LawEnforcementRequest> CreateRequestAsync(string requestingAgency, string caseNumber,
        string requestType, string subject, string details)
    {
        var request = new LawEnforcementRequest
        {
            RequestingAgency = requestingAgency,
            CaseNumber = caseNumber,
            RequestType = requestType,
            Subject = subject,
            Details = details,
            Status = "Pending",
            CreatedUtc = DateTime.UtcNow
        };

        context.LawEnforcementRequests.Add(request);
        await context.SaveChangesAsync();

        return request;
    }

    public async Task<List<LawEnforcementRequest>> GetAllRequestsAsync()
    {
        return await context.LawEnforcementRequests
            .OrderByDescending(r => r.CreatedUtc)
            .ToListAsync();
    }

    public async Task<LawEnforcementRequest> GetRequestByIdAsync(int id)
    {
        return await context.LawEnforcementRequests
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<LawEnforcementRequest> UpdateRequestStatusAsync(int id, string status, string response = null)
    {
        var request = await GetRequestByIdAsync(id);
        if (request == null)
            return null;

        request.Status = status;

        if (response != null)
            request.Response = response;

        if (status == "Completed" || status == "Closed")
            request.CompletedUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return request;
    }

    public async Task<bool> DeleteRequestAsync(int id)
    {
        var request = await GetRequestByIdAsync(id);
        if (request == null)
            return false;

        context.LawEnforcementRequests.Remove(request);
        await context.SaveChangesAsync();
        return true;
    }
}
