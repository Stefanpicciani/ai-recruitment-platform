using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Recruitment.Recruiter.Web.Data;

namespace Recruitment.Recruiter.Web.Pages;

public class IndexModel(RecruiterDbContext db) : PageModel
{
    public int ActiveOpportunities { get; private set; }
    public int Candidates { get; private set; }
    public int Resumes { get; private set; }
    public List<ResumeDocument> RecentResumes { get; private set; } = [];

    public async Task OnGetAsync()
    {
        ActiveOpportunities = await db.JobOpportunities.CountAsync(o => o.IsActive);
        Candidates = await db.Candidates.CountAsync();
        Resumes = await db.ResumeDocuments.CountAsync();
        RecentResumes = await db.ResumeDocuments.AsNoTracking()
            .Include(r => r.Candidate).Include(r => r.Opportunity)
            .OrderByDescending(r => r.SubmittedAt).Take(10).ToListAsync();
    }
}
