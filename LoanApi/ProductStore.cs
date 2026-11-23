namespace LoanApi;
using Microsoft.Extensions.AI;
using System.Numerics.Tensors;

public class ProductStore
{
    private readonly List<ProductChunk> _products =
    [
      new("consumer-loan", "Consumer loan: up to 5,000,000 AMD, 12–60 months, 14% annual interest rate.", ReadOnlyMemory<float>.Empty),

      new("mortgage-loan", "Mortgage loan: up to 50,000,000 AMD, 5–30 years, starting from 9.5% annual interest rate.", ReadOnlyMemory<float>.Empty),

      new("debit-card", "Debit card: annual maintenance fee 2,000 AMD, free ATM withdrawals at partner banks, supports AMD, USD, and EUR.", ReadOnlyMemory<float>.Empty),

      new("savings-account", "Savings account: 3.5% annual interest rate, no minimum balance, interest calculated daily and paid monthly.", ReadOnlyMemory<float>.Empty),

      new("business-loan", "Business loan: up to 20,000,000 AMD, 12–84 months, starting from 13% annual interest rate.", ReadOnlyMemory<float>.Empty)
    ];

    public IReadOnlyList<ProductChunk> Products => _products;


    public async Task IngestAsync(IEmbeddingGenerator<string, Embedding<float>> gen, CancellationToken ct){
        var embeddings = await gen.GenerateAsync(Products.Select(product => product.Text), cancellationToken: ct);
        for (var i = 0 ; i< embeddings.Count ; i++){
            _products[i] = _products[i] with {Vector = embeddings[i].Vector};
        }
    }

    public IEnumerable<(ProductChunk Chunk, float Score)> Search(ReadOnlyMemory<float> query, int top = 3){
        var results = new List<(ProductChunk Chunk, float Score)>();
        for (var i = 0 ; i< Products.Count ; i++){
            float score = TensorPrimitives.CosineSimilarity(query.Span, Products[i].Vector.Span);
            results.Add(( Products[i], score));
        }

        return results.OrderByDescending(r => r.Score).Take(top);

    }
}