using IMApi.Data;
using IMApi.Options;
using IMApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IMApi.Controllers;

public record ArticleTexts(
    string Artikelnummer,
    Dictionary<string, string> Texts,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

public record TextsUpdate(Dictionary<string, string?> Texts);

public record ImageDescription(string FileName, string Name, string? Artikelnummer, string? Language, Dictionary<string, string> Texts);

/// <summary>Texts rendered into the product images, per Artikelnummer and language.</summary>
[ApiController]
[Route("api")]
[OpenCloudAuth]
public class TextsController(ImApiDbContext db, IOptions<ImApiOptions> options) : ControllerBase
{
    private IReadOnlyList<string> Languages => options.Value.EffectiveLanguages;

    /// <summary>Lists the articles with texts, optionally filtered by Artikelnummer or text.</summary>
    [HttpGet("texts")]
    public async Task<List<ArticleTexts>> List(string? q, int skip = 0, int take = 100, CancellationToken ct = default)
    {
        var query = db.ImageTexts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(t => t.Artikelnummer.Contains(q) || t.Text.Contains(q));
        var numbers = await query.Select(t => t.Artikelnummer).Distinct().OrderBy(n => n)
            .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
        var rows = await db.ImageTexts.AsNoTracking().Where(t => numbers.Contains(t.Artikelnummer)).ToListAsync(ct);
        return numbers.Select(n => ToDto(n, rows.Where(r => r.Artikelnummer == n))).ToList();
    }

    [HttpGet("texts/{artikelnummer}")]
    public async Task<ArticleTexts> Get(string artikelnummer, CancellationToken ct) =>
        ToDto(artikelnummer, await db.ImageTexts.AsNoTracking()
            .Where(t => t.Artikelnummer == artikelnummer).ToListAsync(ct));

    /// <summary>Sets the texts of an article; an empty or null text removes that language.</summary>
    [HttpPut("texts/{artikelnummer}")]
    public async Task<ActionResult<ArticleTexts>> Put(string artikelnummer, TextsUpdate update, CancellationToken ct)
    {
        if (!IsArtikelnummer(artikelnummer))
            return Problem(title: "Ungültige Artikelnummer", statusCode: StatusCodes.Status400BadRequest);
        var unknown = update.Texts.Keys.Select(k => k.ToUpperInvariant()).Except(Languages).ToList();
        if (unknown.Count > 0)
            return Problem(title: "Unbekannte Sprachen: " + string.Join(", ", unknown), statusCode: StatusCodes.Status400BadRequest);
        if (update.Texts.Values.Any(t => t?.Length > 500))
            return Problem(title: "Texte dürfen höchstens 500 Zeichen lang sein", statusCode: StatusCodes.Status400BadRequest);

        var user = OpenCloudAuthFilter.CurrentUser(HttpContext);
        var existing = await db.ImageTexts.Where(t => t.Artikelnummer == artikelnummer).ToListAsync(ct);
        foreach (var (key, value) in update.Texts)
        {
            var language = key.ToUpperInvariant();
            var row = existing.FirstOrDefault(t => t.Language == language);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (row is not null) db.ImageTexts.Remove(row);
                continue;
            }
            if (row is null)
            {
                row = new ImageText { Artikelnummer = artikelnummer, Language = language };
                db.ImageTexts.Add(row);
            }
            row.Text = value.Trim();
            row.UpdatedAt = DateTimeOffset.UtcNow;
            row.UpdatedBy = user.DisplayName ?? user.UserName ?? user.Id;
        }
        await db.SaveChangesAsync(ct);
        return await Get(artikelnummer, ct);
    }

    [HttpDelete("texts/{artikelnummer}")]
    public async Task<IActionResult> Delete(string artikelnummer, CancellationToken ct)
    {
        await db.ImageTexts.Where(t => t.Artikelnummer == artikelnummer).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    /// <summary>What the file names say (Artikelnummer, index, language) plus the stored texts.</summary>
    [HttpGet("describe")]
    public async Task<List<ImageDescription>> Describe([FromQuery(Name = "name")] List<string> names, CancellationToken ct)
    {
        var images = names.Take(200).Select(n => ImageName.Parse(n, Languages)).ToList();
        var numbers = images.Select(i => i.Artikelnummer).OfType<string>().Distinct().ToList();
        var rows = await db.ImageTexts.AsNoTracking().Where(t => numbers.Contains(t.Artikelnummer)).ToListAsync(ct);
        return images.Select(i => new ImageDescription(i.FileName, i.Name, i.Artikelnummer, i.Language,
            rows.Where(r => r.Artikelnummer == i.Artikelnummer).ToDictionary(r => r.Language, r => r.Text))).ToList();
    }

    private static bool IsArtikelnummer(string value) => value.Length is > 0 and <= 64 && value.All(char.IsAsciiDigit);

    private static ArticleTexts ToDto(string artikelnummer, IEnumerable<ImageText> rows)
    {
        var list = rows.ToList();
        var latest = list.MaxBy(r => r.UpdatedAt);
        return new ArticleTexts(artikelnummer, list.ToDictionary(r => r.Language, r => r.Text), latest?.UpdatedAt, latest?.UpdatedBy);
    }
}
