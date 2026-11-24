using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Recruitment.Recruiter.Web.Data;

namespace Recruitment.Recruiter.Web.Pages.Candidates;

public class IndexModel(RecruiterDbContext db) : PageModel
{
    public record Row(int Id, string FullName, string Email, string? Phone, int Resumes, DateTime? LastSubmission);

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    public List<Row> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var query = db.Candidates.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            query = query.Where(c => c.FullName.Contains(q) || c.Email.Contains(q));
        }

        Items = await query
            .OrderByDescending(c => c.Resumes.Max(r => (DateTime?)r.SubmittedAt))
            .ThenBy(c => c.FullName)
            .Take(500)
            .Select(c => new Row(c.Id, c.FullName, c.Email, c.Phone, c.Resumes.Count,
                c.Resumes.Max(r => (DateTime?)r.SubmittedAt)))
            .ToListAsync();
    }
}
