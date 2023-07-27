using Microsoft.AspNetCore.Mvc;

namespace MangaDexWatcher.Api.Controllers;

using Database.Services;
using Shared;

[ApiController]
public class ChapterController : ControllerBase
{
    private readonly IDbService _db;

    public ChapterController(IDbService db)
    {
        _db = db;
    }

    [HttpGet, Route("api/chapters/not-indexed")]
    [ProducesDefaultResponseType(typeof(MangaCache[]))]
    public async Task<IActionResult> GetChapters([FromQuery] int length = 100)
    {
        var chapters = await _db.ChaptersByStates(length, ChapterState.NotIndexed, ChapterState.Unknown);
        return Ok(chapters);
    }

    [HttpGet, Route("api/chapter/{id}/indexed")]
    public async Task<IActionResult> SetIndexed([FromRoute] long id)
    {
        await _db.SetChapterState(id, ChapterState.Indexed);
        return Ok();
    }

    [HttpGet, Route("api/chapter/{id}/errored")]
    public async Task<IActionResult> SetErrored([FromRoute] long id)
    {
        await _db.SetChapterState(id, ChapterState.ErrorIndexing);
        return Ok();
    }
}