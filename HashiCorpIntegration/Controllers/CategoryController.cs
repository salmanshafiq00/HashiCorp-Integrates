using HashiCorpIntegration.Data;
using HashiCorpIntegration.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HashiCorpIntegration.Controllers;

public class CategoryController : Controller
{
    private readonly IApplicationDbContextFactory _contextFactory;

    public CategoryController(IApplicationDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // GET: Category
    public async Task<IActionResult> Index()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return View(await context.Categories.ToListAsync());
    }

    // GET: Category/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        using var context = await _contextFactory.CreateDbContextAsync();
        var category = await context.Categories.FindAsync(id);
        if (category == null) return NotFound();
        return View(category);
    }

    // GET: Category/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Category/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name")] Category category)
    {
        if (ModelState.IsValid)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.Add(category);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    // GET: Category/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        using var context = await _contextFactory.CreateDbContextAsync();
        var category = await context.Categories.FindAsync(id);
        if (category == null) return NotFound();
        return View(category);
    }

    // POST: Category/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Category category)
    {
        if (id != category.Id) return NotFound();

        if (ModelState.IsValid)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            try
            {
                context.Update(category);
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!context.Categories.Any(e => e.Id == category.Id))
                    return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    // GET: Category/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        using var context = await _contextFactory.CreateDbContextAsync();
        var category = await context.Categories.FindAsync(id);
        if (category == null) return NotFound();
        return View(category);
    }

    // POST: Category/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var category = await context.Categories.FindAsync(id);
        if (category != null)
        {
            context.Categories.Remove(category);
            await context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}