using System;
using System.Linq;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AI_AI_Agent.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApprovalsController : ControllerBase
    {
        private readonly IApprovalService _approvals;
        public ApprovalsController(IApprovalService approvals) { _approvals = approvals; }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] ApprovalStatus? status)
        {
            var items = await _approvals.ListAsync(status);
            return Ok(items.Select(x => new { x.Id, x.Kind, x.Summary, x.CreatedAt, x.Status }));
        }

        [HttpPost("{id}/approve")]
        public async Task<IActionResult> Approve(string id)
        {
            var ok = await _approvals.ApproveAsync(id);
            if (!ok) return NotFound();
            return Ok(new { id, status = ApprovalStatus.Approved.ToString() });
        }

        [HttpPost("{id}/deny")]
        public async Task<IActionResult> Deny(string id)
        {
            var ok = await _approvals.DenyAsync(id);
            if (!ok) return NotFound();
            return Ok(new { id, status = ApprovalStatus.Denied.ToString() });
        }
    }
}
