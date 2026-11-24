using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Recruitment.Recruiter.Web.Data;

namespace Recruitment.Recruiter.Web.Pages.Opportunities;

public class EditModel(RecruiterDbContext db) : PageModel
{
    public static readonly string[] EmploymentTypes = ["Full-time", "Part-time", "Contract", "Internship", "Temporary"];

    public class SkillInput
    {
        [StringLength(100)] public string? Name { get; set; }
        // Default false: unchecked checkboxes are not posted.
        public bool IsMandatory { get; set; }
    }

    public class OpportunityInput
    {
        [Required(ErrorMessage = "O título é obrigatório."), StringLength(160)]
        [Display(Name = "Título")] public string Title { get; set; } = "";

        [StringLength(100), Display(Name = "Departamento")] public string? Department { get; set; }
        [StringLength(160), Display(Name = "Local")] public string? Location { get; set; }

        [Required, StringLength(40), Display(Name = "Tipo de contrato")]
        public string EmploymentType { get; set; } = "Full-time";

        [Required(ErrorMessage = "A descrição é obrigatória."), Display(Name = "Descrição")]
        public string Description { get; set; } = "";

        [Display(Name = "Requisitos")] public string? Requirements { get; set; }
        [Display(Name = "Oportunidade ativa")] public bool IsActive { get; set; } = true;

        public List<SkillInput> Skills { get; set; } = [];
    }

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public OpportunityInput Input { get; set; } = new();

    public bool IsNew => Id is null;

    public async Task<IActionResult> OnGetAsync()
    {
        if (IsNew)
        {
            Input.Skills.Add(new SkillInput { IsMandatory = true });
            return Page();
        }

        var o = await db.JobOpportunities.AsNoTracking().Include(x => x.RequiredSkills)
            .FirstOrDefaultAsync(x => x.Id == Id);
        if (o is null) return NotFound();

        Input = new OpportunityInput
        {
            Title = o.Title, Department = o.Department, Location = o.Location,
            EmploymentType = o.EmploymentType, Description = o.Description,
            Requirements = o.Requirements, IsActive = o.IsActive,
            Skills = o.RequiredSkills.OrderBy(s => s.Id)
                .Select(s => new SkillInput { Name = s.Name, IsMandatory = s.IsMandatory }).ToList(),
        };
        if (Input.Skills.Count == 0) Input.Skills.Add(new SkillInput { IsMandatory = true });
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var skills = Input.Skills
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .GroupBy(s => s.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new RequiredSkill { Name = g.Key, IsMandatory = g.Any(x => x.IsMandatory) })
            .ToList();

        if (!ModelState.IsValid)
        {
            if (Input.Skills.Count == 0) Input.Skills.Add(new SkillInput { IsMandatory = true });
            return Page();
        }

        JobOpportunity entity;
        if (IsNew)
        {
            entity = new JobOpportunity { CreatedAt = DateTime.UtcNow };
            db.JobOpportunities.Add(entity);
        }
        else
        {
            entity = await db.JobOpportunities.Include(x => x.RequiredSkills).FirstOrDefaultAsync(x => x.Id == Id)
                     ?? throw new BadHttpRequestException("Oportunidade não encontrada.", 404);
            entity.UpdatedAt = DateTime.UtcNow;
            db.RequiredSkills.RemoveRange(entity.RequiredSkills);
        }

        entity.Title = Input.Title.Trim();
        entity.Department = Input.Department?.Trim() ?? "";
        entity.Location = Input.Location?.Trim() ?? "";
        entity.EmploymentType = Input.EmploymentType;
        entity.Description = Input.Description.Trim();
        entity.Requirements = Input.Requirements?.Trim() ?? "";
        entity.IsActive = Input.IsActive;
        entity.RequiredSkills = skills;

        await db.SaveChangesAsync();
        TempData["Success"] = IsNew ? "Oportunidade criada." : "Oportunidade atualizada.";
        return RedirectToPage("Details", new { id = entity.Id });
    }
}

