using AI_AI_Agent.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI_AI_Agent.API.Controllers
{
    [ApiController]
    [Route("api/models")]
    public sealed class ModelsController : ControllerBase
    {
        private readonly IModelRegistry _registry;
        public ModelsController(IModelRegistry registry) => _registry = registry;

        [HttpGet]
        [AllowAnonymous]
        public IActionResult List() => Ok(_registry.List());
    }
}
