using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Common;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.API.Controllers;

[Route("api/boards")]
public class BoardsController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public BoardsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// List boards for the school-board autocomplete.
    /// `q` — case-insensitive substring match against board name or short code. Empty → top 20.
    /// `limit` — caps how many rows to return (default 20, max 50).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 20)
    {
        if (limit <= 0 || limit > 50) limit = 20;

        var query = _unitOfWork.Boards.Query();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim().ToLower();
            query = query.Where(b =>
                b.Name.ToLower().Contains(needle) ||
                (b.ShortCode != null && b.ShortCode.ToLower().Contains(needle)));
        }

        var boards = await query
            .OrderBy(b => b.ShortCode)
            .ThenBy(b => b.Name)
            .Take(limit)
            .Select(b => new { b.Id, b.Name, b.ShortCode, b.Category })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(boards));
    }
}
