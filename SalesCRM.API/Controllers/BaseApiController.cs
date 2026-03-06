using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SalesCRM.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    protected string UserRole => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
}
