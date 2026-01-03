using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("POS Server API is running");
    }
}