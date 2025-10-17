using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AI_AI_Agent.Application.Services
{
    public enum ApprovalStatus { Pending, Approved, Denied }

    public record ApprovalRequest(string Id, string Kind, string Summary, string PayloadPath, DateTimeOffset CreatedAt, ApprovalStatus Status);

    public interface IApprovalService
    {
        Task<ApprovalRequest> CreateAsync(string kind, string summary, string payloadJson);
        Task<bool> ApproveAsync(string id);
        Task<bool> DenyAsync(string id);
        Task<ApprovalRequest?> GetAsync(string id);
        Task<IReadOnlyList<ApprovalRequest>> ListAsync(ApprovalStatus? status = null);
    }
}
