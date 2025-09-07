using HashiCorpIntegration.Data;
using HashiCorpIntegration.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HashiCorpIntegration.Controllers;

public class ProductController : Controller
{
    private readonly IApplicationDbContextFactory _contextFactory;

    public ProductController(IApplicationDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // GET: Product
    public async Task<IActionResult> Index()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var products = await context.Products.Include(p => p.Category).ToListAsync();
        return View(products);
    }

    // GET: Product/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        using var context = await _contextFactory.CreateDbContextAsync();
        var product = await context.Products.Include(p => p.Category)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (product == null) return NotFound();

        return View(product);
    }

    // GET: Product/Create
    public async Task<IActionResult> Create()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        ViewData["CategoryId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            context.Categories, "Id", "Name"
        );
        return View();
    }

    // POST: Product/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Price,Description,CategoryId,StockQuantity,ImageUrl,CreatedDate")] Product product)
    {
        if (ModelState.IsValid)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.Add(product);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        using (var context = await _contextFactory.CreateDbContextAsync())
        {
            ViewData["CategoryId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                context.Categories, "Id", "Name", product.CategoryId
            );
        }
        return View(product);
    }

    // GET: Product/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        using var context = await _contextFactory.CreateDbContextAsync();
        var product = await context.Products.FindAsync(id);
        if (product == null) return NotFound();

        ViewData["CategoryId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            context.Categories, "Id", "Name", product.CategoryId
        );
        return View(product);
    }

    // POST: Product/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Price,Description,CategoryId,StockQuantity,ImageUrl,CreatedDate")] Product product)
    {
        if (id != product.Id) return NotFound();

        if (ModelState.IsValid)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            try
            {
                context.Update(product);
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!context.Products.Any(e => e.Id == product.Id))
                    return NotFound();
                else
                    throw;
            }
            return RedirectToAction(nameof(Index));
        }

        using (var context = await _contextFactory.CreateDbContextAsync())
        {
            ViewData["CategoryId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                context.Categories, "Id", "Name", product.CategoryId
            );
        }
        return View(product);
    }

    // GET: Product/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        using var context = await _contextFactory.CreateDbContextAsync();
        var product = await context.Products.Include(p => p.Category)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (product == null) return NotFound();

        return View(product);
    }

    // POST: Product/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var product = await context.Products.FindAsync(id);
        if (product != null)
        {
            context.Products.Remove(product);
            await context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
