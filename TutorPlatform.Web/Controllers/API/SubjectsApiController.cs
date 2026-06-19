using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Controllers.Api;

// DTO để không lộ navigation property / tránh vòng lặp JSON
public class SubjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Level { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class SubjectCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Level { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

[ApiController]
[Route("api/subjects")]
public class SubjectsApiController : ControllerBase
{
    private readonly AppDbContext _db;

    public SubjectsApiController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/subjects?activeOnly=true
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SubjectDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        var query = _db.Subjects.AsQueryable();
        if (activeOnly)
            query = query.Where(s => s.IsActive);

        var data = await query
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Level = s.Level,
                IsActive = s.IsActive
            })
            .ToListAsync();

        return Ok(data);
    }

    // GET: api/subjects/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SubjectDto>> GetById(int id)
    {
        var s = await _db.Subjects.FindAsync(id);
        if (s == null)
            return NotFound(new { message = $"Không tìm thấy môn học có Id = {id}" });

        return Ok(new SubjectDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Level = s.Level,
            IsActive = s.IsActive
        });
    }

    // POST: api/subjects
    [HttpPost]
    public async Task<ActionResult<SubjectDto>> Create([FromBody] SubjectCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Tên môn học không được để trống." });

        var subject = new Subject
        {
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Level = dto.Level,
            IsActive = dto.IsActive
        };

        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();

        var result = new SubjectDto
        {
            Id = subject.Id,
            Name = subject.Name,
            Description = subject.Description,
            Level = subject.Level,
            IsActive = subject.IsActive
        };

        return CreatedAtAction(nameof(GetById), new { id = subject.Id }, result);
    }

    // PUT: api/subjects/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SubjectCreateDto dto)
    {
        var subject = await _db.Subjects.FindAsync(id);
        if (subject == null)
            return NotFound(new { message = $"Không tìm thấy môn học có Id = {id}" });

        subject.Name = dto.Name.Trim();
        subject.Description = dto.Description;
        subject.Level = dto.Level;
        subject.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/subjects/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var subject = await _db.Subjects.FindAsync(id);
        if (subject == null)
            return NotFound(new { message = $"Không tìm thấy môn học có Id = {id}" });

        _db.Subjects.Remove(subject);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}