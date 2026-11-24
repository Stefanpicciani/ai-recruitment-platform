using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Recruitment.Recruiter.Web.Data;
using Recruitment.Recruiter.Web.Storage;

namespace Recruitment.Recruiter.Web.Pages.Resumes;

[RequestSizeLimit(ResumeRules.MaxFileSizeBytes + 1024 * 1024)]
public class SubmitModel(RecruiterDbContext db, IResumeStorage storage, ILogger<SubmitModel> logger) : PageModel
{
    public class SubmitInput
    {
        [Required(ErrorMessage = "Selecione uma oportunidade."), Display(Name = "Oportunidade")]
        public int? OpportunityId { get; set; }

        [Required(ErrorMessage = "O nome é obrigatório."), StringLength(160), Display(Name = "Nome completo")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "O email é obrigatório."), EmailAddress(ErrorMessage = "Email inválido."), StringLength(254)]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        [StringLength(40), Phone(ErrorMessage = "Telefone inválido."), Display(Name = "Telefone")]
        public string? Phone { get; set; }

        [Display(Name = "Currículo (PDF, DOC, DOCX · máx. 10 MB)")]
        public IFormFile? File { get; set; }
    }

    [BindProperty] public SubmitInput Input { get; set; } = new();
    public SelectList Opportunities { get; private set; } = default!;

    public async Task OnGetAsync(int? opportunityId)
    {
        Input.OpportunityId = opportunityId;
        await LoadOpportunitiesAsync();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ResumeRules.Validate(Input.File) is { } fileError)
            ModelState.AddModelError("Input.File", fileError);

        if (Input.OpportunityId is int oppId && !await db.JobOpportunities.AnyAsync(o => o.Id == oppId && o.IsActive, ct))
            ModelState.AddModelError("Input.OpportunityId", "A oportunidade selecionada não existe ou está inativa.");

        if (!ModelState.IsValid)
        {
            await LoadOpportunitiesAsync();
            return Page();
        }

        var file = Input.File!;
        var originalName = Path.GetFileName(file.FileName);
        var contentType = ResumeRules.ContentTypeFor(originalName);
        var email = Input.Email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        StoredResume stored;
        await using (var stream = file.OpenReadStream())
        {
            stored = await storage.SaveAsync(stream, originalName, contentType, ct);
        }

        // Upsert candidate by email.
        var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.Email == email, ct);
        if (candidate is null)
        {
            candidate = new Candidate { Email = email, CreatedAt = now };
            db.Candidates.Add(candidate);
        }
        else
        {
            candidate.UpdatedAt = now;
        }
        candidate.FullName = Input.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(Input.Phone)) candidate.Phone = Input.Phone.Trim();

        db.ResumeDocuments.Add(new ResumeDocument
        {
            Candidate = candidate,
            OpportunityId = Input.OpportunityId!.Value,
            OriginalFileName = originalName,
            StoredFileName = stored.StoredFileName,
            ContentType = contentType,
            FileSize = file.Length,
            StoredPath = stored.StoredPath,
            SubmittedAt = now,
        });

        // Flat submission log (one row per submission), kept in sync with ResumeDocuments.
        db.ResumeSubmissions.Add(new ResumeSubmission
        {
            CandidateName = candidate.FullName,
            Email = email,
            OpportunityId = Input.OpportunityId!.Value,
            OriginalFileName = originalName,
            StoredPath = stored.StoredPath,
            SubmittedAt = now,
        });

        try
        {
            await db.SaveChangesAsync(ct); // single SaveChanges = single transaction
        }
        catch
        {
            // Nothing was written to the database: don't leave the file behind either.
            await storage.DeleteAsync(stored.StoredPath, CancellationToken.None);
            throw;
        }
        logger.LogInformation("Resume submitted for opportunity {OpportunityId}, candidate {CandidateId}", Input.OpportunityId, candidate.Id);

        TempData["Success"] = $"Currículo de {candidate.FullName} submetido com sucesso.";
        return RedirectToPage("Submit", new { opportunityId = Input.OpportunityId });
    }

    private async Task LoadOpportunitiesAsync()
    {
        var items = await db.JobOpportunities.AsNoTracking().Where(o => o.IsActive)
            .OrderBy(o => o.Title).Select(o => new { o.Id, o.Title, o.Department }).ToListAsync();
        Opportunities = new SelectList(
            items.Select(o => new { o.Id, Text = string.IsNullOrEmpty(o.Department) ? o.Title : $"{o.Title} — {o.Department}" }),
            "Id", "Text", Input.OpportunityId);
    }
}

