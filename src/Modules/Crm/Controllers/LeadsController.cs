using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhantomPulse.Crm.Controllers;
using PhantomPulse.Crm.Dtos.Requests;
using PhantomPulse.Crm.Dtos.Responses;
using PhantomPulse.Crm.Entities;
using PhantomPulse.Crm.Services;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Crm.Controllers;

[Authorize]
[ApiController]
[Route("crm/leads")]
public class LeadsController(LeadService leads, ContactService contacts) : ControllerBase
{
    /// <summary>GET /api/crm/leads?search=sana&amp;tag=hot-lead&amp;status=open&amp;page=1&amp;pageSize=25</summary>
    [RequirePermission("contacts.view")]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? tag,
        [FromQuery] string? status,
        [FromQuery] PaginationQuery query,
        CancellationToken ct)
    {
        var rows   = await leads.GetLeadsAsync(search, tag, status, ct);
        var shaped = rows.Select(MapLead).ToList();
        var page   = Pagination.Slice(shaped, query);
        return Ok(ApiResponse<PagedData<LeadResponse>>.Ok(page, "Leads fetched"));
    }

    /// <summary>GET /api/crm/leads/{id}</summary>
    [RequirePermission("contacts.view")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var c = await contacts.GetByIdAsync(id, ct);
        if (c is null)
            return NotFound(ApiResponse<LeadResponse>.Fail(
                "Lead not found", new ApiError("not_found", "Lead not found")));

        return Ok(ApiResponse<LeadResponse>.Ok(MapLead(c), "Lead fetched"));
    }

    /// <summary>POST /api/crm/leads</summary>
    [RequirePermission("contacts.create")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateLeadRequest req, CancellationToken ct)
    {
        var c = await leads.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id = c.Id },
            ApiResponse<LeadResponse>.Ok(MapLead(c), "Lead created"));
    }

    /// <summary>DELETE /api/crm/leads/{id}</summary>
    [RequirePermission("contacts.delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await contacts.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "Lead deleted"));
    }

    /// <summary>POST /api/crm/leads/{id}/tags  — body: { "tag": "hot-lead" }</summary>
    [RequirePermission("contacts.edit")]
    [HttpPost("{id:guid}/tags")]
    public async Task<IActionResult> AddTag(Guid id, AddTagRequest req, CancellationToken ct)
    {
        await contacts.AddTagAsync(id, req.Tag, ct);
        return Ok(ApiResponse<object>.Ok(new { id, req.Tag }, "Tag added"));
    }

    /// <summary>DELETE /api/crm/leads/{id}/tags?tag=hot-lead</summary>
    [RequirePermission("contacts.edit")]
    [HttpDelete("{id:guid}/tags")]
    public async Task<IActionResult> RemoveTag(Guid id, [FromQuery] string tag, CancellationToken ct)
    {
        await contacts.RemoveTagAsync(id, tag, ct);
        return Ok(ApiResponse<object>.Ok(new { id, tag }, "Tag removed"));
    }

    /// <summary>GET /api/crm/leads/{id}/notes</summary>
    [RequirePermission("contacts.view")]
    [HttpGet("{id:guid}/notes")]
    public async Task<IActionResult> GetNotes(Guid id, CancellationToken ct)
    {
        var notes = await contacts.GetNotesAsync(id, ct);
        var resp  = notes.Select(n => new ContactNoteResponse(n.Id, n.Body, n.CreatedAt)).ToList();
        return Ok(ApiResponse<List<ContactNoteResponse>>.Ok(resp, "Notes fetched"));
    }

    /// <summary>POST /api/crm/leads/{id}/notes  — body: { "body": "..." }</summary>
    [RequirePermission("contacts.edit")]
    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> AddNote(Guid id, AddNoteRequest req, CancellationToken ct)
    {
        var n    = await contacts.AddNoteAsync(id, req.Body, ct);
        var resp = new ContactNoteResponse(n.Id, n.Body, n.CreatedAt);
        return Ok(ApiResponse<ContactNoteResponse>.Ok(resp, "Note added"));
    }

    /// <summary>DELETE /api/crm/leads/{id}/notes/{noteId}</summary>
    [RequirePermission("contacts.edit")]
    [HttpDelete("{id:guid}/notes/{noteId:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id, Guid noteId, CancellationToken ct)
    {
        await contacts.DeleteNoteAsync(noteId, ct);
        return Ok(ApiResponse<object>.Ok(new { id, noteId }, "Note deleted"));
    }

    /// <summary>PATCH /api/crm/leads/{id}/score  — body: { "delta": 5 }</summary>
    [RequirePermission("contacts.edit")]
    [HttpPatch("{id:guid}/score")]
    public async Task<IActionResult> UpdateScore(Guid id, UpdateScoreRequest req, CancellationToken ct)
    {
        var c = await leads.UpdateScoreAsync(id, req.Delta, ct);
        return Ok(ApiResponse<LeadResponse>.Ok(MapLead(c), "Score updated"));
    }

    /// <summary>PATCH /api/crm/leads/{id}/status  — body: { "status": "won" }</summary>
    [RequirePermission("contacts.edit")]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateStatusRequest req, CancellationToken ct)
    {
        var c = await leads.UpdateStatusAsync(id, req.Status, ct);
        return Ok(ApiResponse<LeadResponse>.Ok(MapLead(c), "Status updated"));
    }

    /// <summary>POST /api/crm/leads/import — multipart/form-data: file (.csv), mapping (JSON), channel, createSmartList, smartListName</summary>
    [RequirePermission("contacts.create")]
    [HttpPost("import")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Import(
        IFormFile file,
        [FromForm] string mapping,
        [FromForm] string channel = "all",
        [FromForm] bool createSmartList = false,
        [FromForm] string smartListName = "",
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail(
                "No file provided", new ApiError("no_file", "A CSV file is required")));

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Fail(
                "Invalid file type", new ApiError("invalid_file", "Only CSV files are accepted")));

        Dictionary<string, string> columnMapping;
        try   { columnMapping = JsonSerializer.Deserialize<Dictionary<string, string>>(mapping) ?? []; }
        catch { return BadRequest(ApiResponse<object>.Fail(
                    "Invalid mapping", new ApiError("invalid_mapping", "Column mapping is not valid JSON"))); }

        var result = await leads.ImportAsync(file, columnMapping, channel, createSmartList, smartListName, ct);
        return Ok(ApiResponse<ImportResultWithSmartList>.Ok(result,
            $"Import complete: {result.Imported} imported, {result.Skipped} skipped, {result.Failed} failed"));
    }

    /// <summary>GET /api/crm/leads/imports — list import batches for this tenant</summary>
    [RequirePermission("contacts.view")]
    [HttpGet("imports")]
    public async Task<IActionResult> GetImportHistory(CancellationToken ct)
    {
        var batches = await leads.GetImportHistoryAsync(ct);
        var resp    = batches.Select(b => new ImportBatchResponse(
            b.Id, b.FileName, b.Channel,
            b.Total, b.Imported, b.Skipped, b.Failed,
            b.Status, b.CreatedAt)).ToList();
        return Ok(ApiResponse<List<ImportBatchResponse>>.Ok(resp, "Import history fetched"));
    }

    /// <summary>DELETE /api/crm/leads/imports/{id} — revert (soft-delete contacts from that batch)</summary>
    [RequirePermission("contacts.delete")]
    [HttpDelete("imports/{id:guid}")]
    public async Task<IActionResult> RevertImport(Guid id, CancellationToken ct)
    {
        await leads.RevertImportAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "Import reverted — all contacts from this batch have been removed"));
    }

    // ── Mapper ────────────────────────────────────────────────────────────────

    private static LeadResponse MapLead(Contact c)
    {
        var name          = $"{c.FirstName} {c.LastName}".Trim();
        var primaryEmail  = c.Emails.FirstOrDefault(e => e.IsPrimary)?.Email
                         ?? c.Emails.FirstOrDefault()?.Email ?? "";
        var primaryPhone  = c.Phones.FirstOrDefault(p => p.IsPrimary)?.Phone
                         ?? c.Phones.FirstOrDefault()?.Phone ?? "";

        return new LeadResponse(
            c.Id, c.FirstName, c.LastName, name,
            primaryEmail, primaryPhone,
            c.Emails.Select(ContactsController.MapEmail).ToList(),
            c.Phones.Select(ContactsController.MapPhone).ToList(),
            c.Company, c.Title,
            c.Tags, c.OwnerName, BuildInitials(c.OwnerName),
            c.Source, c.Score, c.Status, c.Notes,
            c.CreatedAt, c.LastActivityAt);
    }

    private static string BuildInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            : name[..Math.Min(2, name.Length)].ToUpper();
    }
}
