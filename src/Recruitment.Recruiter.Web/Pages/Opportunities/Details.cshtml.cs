using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Recruitment.Recruiter.Web.Data;

namespace Recruitment.Recruiter.Web.Pages.Opportunities;

public class DetailsModel(RecruiterDbContext db) : PageModel
{
    public JobOpportunity Opportunity { get; private set; } = default!;
    public List<ResumeDocument> Resumes { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var o = await db.JobOpportunities.AsNoTracking().Include(x => x.RequiredSkills)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (o is null) return NotFound();
        Opportunity = o;

        Resumes = await db.ResumeDocuments.AsNoTracking().Include(r => r.Candidate)
            .Where(r => r.OpportunityId == id)
            .OrderByDescending(r => r.SubmittedAt).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var o = await db.JobOpportunities.FindAsync(id);
        if (o is null) return NotFound();
        o.IsActive = !o.IsActive;
        o.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Success"] = o.IsActive ? "Oportunidade reativada." : "Oportunidade desativada.";
        return RedirectToPage(new { id });
    }
}
