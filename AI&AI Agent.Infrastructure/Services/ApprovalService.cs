using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Services;

namespace AI_AI_Agent.Infrastructure.Services
{
    public sealed class ApprovalService : IApprovalService
    {
        private readonly ConcurrentDictionary<string, ApprovalRequest> _store = new();
        private readonly string _dir;

        public ApprovalService()
        {
            _dir = Path.Combine(AppContext.BaseDirectory, "workspace", "approvals");
            Directory.CreateDirectory(_dir);
        }

        public Task<ApprovalRequest> CreateAsync(string kind, string summary, string payloadJson)
        {
            var id = Guid.NewGuid().ToString("N");
            var path = Path.Combine(_dir, id + ".json");
            File.WriteAllText(path, payloadJson);
            var req = new ApprovalRequest(id, kind, summary, path, DateTimeOffset.UtcNow, ApprovalStatus.Pending);
            _store[id] = req;
            return Task.FromResult(req);
        }

        public Task<bool> ApproveAsync(string id)
        {
            if (_store.TryGetValue(id, out var req))
            {
                _store[id] = req with { Status = ApprovalStatus.Approved };
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> DenyAsync(string id)
        {
            if (_store.TryGetValue(id, out var req))
            {
                _store[id] = req with { Status = ApprovalStatus.Denied };
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<ApprovalRequest?> GetAsync(string id)
        {
            _store.TryGetValue(id, out var req);
            return Task.FromResult<ApprovalRequest?>(req);
        }

        public Task<IReadOnlyList<ApprovalRequest>> ListAsync(ApprovalStatus? status = null)
        {
            var list = _store.Values.AsEnumerable();
            if (status.HasValue) list = list.Where(x => x.Status == status.Value);
            return Task.FromResult<IReadOnlyList<ApprovalRequest>>(list.OrderByDescending(x => x.CreatedAt).ToList());
        }
    }
}
