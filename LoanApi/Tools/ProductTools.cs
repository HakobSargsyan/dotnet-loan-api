namespace LoanApi;
using System.ComponentModel;
using Microsoft.Extensions.AI;

public class ProductTools
{
    private readonly ProductStore _store;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _gen;

    public ProductTools(ProductStore store, IEmbeddingGenerator<string, Embedding<float>> gen)
    {
        _store = store;
        _gen = gen;
    }

    [Description("Searches the bank's product catalog: loans, mortgages, cards, savings. Returns rates, limits and terms.")]
    public async Task<string> SearchProducts(
        [Description("What to search for, in English, e.g. 'mortgage interest rate'")] string query,
        CancellationToken ct)
    {
        var embedding = await _gen.GenerateAsync([query], cancellationToken: ct);
        var hits = _store.Search(embedding[0].Vector);

        var filtered = hits
            .Where(h => h.Score > 0.5)
            .ToList();

        string facts = string.Join("\n", filtered.Select(h => $"[{h.Chunk.Source}] {h.Chunk.Text}"));

        Console.WriteLine($"[tool] {facts}");

       if (filtered.Count == 0)
       {
           return "No matching products found.";
       }

       return facts;
    }
}