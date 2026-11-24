using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Recruitment.Recruiter.Web.Data;

namespace Recruitment.Recruiter.Web.Pages.Candidates;

public class DetailsModel(RecruiterDbContext db) : PageModel
{
    public Candidate Candidate { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var c = await db.Candidates.AsNoTracking()
            .Include(x => x.Resumes).ThenInclude(r => r.Opportunity)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        Candidate = c;
        return Page();
    }
}
