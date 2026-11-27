using LoanApp;

try{
    var d = new LoanRequest("1", 5000);
    var smp = new SimpleCalculator();
    var anc = new AnnuityCalculator(0.12m);
    var fb = new FakeCreditBureau();

    List<ILoanCalculator> calcs = [smp, anc];

    foreach (var calc in calcs)
    {
        decimal p = calc.MonthlyPayment(d);
        Console.WriteLine($"{calc.GetType().Name}: {p:N2}");
    }

    Console.WriteLine(new LoanRequest("1", 5000m) == new LoanRequest("1", 5000m));

    var longer = d with { Months = 24 };
    Console.WriteLine(longer);
    Console.WriteLine(d);


    var annuity = anc;
    Console.WriteLine($"12 months: {annuity.MonthlyPayment(d):N2}");
    Console.WriteLine($"24 months: {annuity.MonthlyPayment(longer):N2}");


    List<LoanRequest> loans =
    [
        new("C1", 5000m, 12),
        new("C2", 20000m, 24),
        new("C3", 1500m, 6),
        new("C1", 8000m, 36),
        new("C4", 12000m, 24),
    ];

    ///loans.ForEach(loan => Console.WriteLine(loan.Amount));
    var filtered = loans.Where(loan => loan.Amount > 5000m).OrderByDescending(loan => loan.Amount);
    var filtered_amount = loans.Where(loan => string.Equals(loan.CustomerId, "C1")).Sum(loan => loan.Amount);

    var sw = System.Diagnostics.Stopwatch.StartNew();
    foreach (var loan in filtered) {
     try
     {
        await fb.GetCreditScoreAsync(loan.CustomerId);
        Console.WriteLine($"{loan.CustomerId}: {loan.Email ?? "no email"}");
     } catch (CreditBureauUnavailableException e) {
         Console.WriteLine($"Failed: {e.Message}");
     }
    }
    Console.WriteLine($"Sequential: {sw.ElapsedMilliseconds} ms");
    Console.WriteLine(filtered_amount);

    foreach (var group in loans.GroupBy(l => l.Months))
    {
        Console.WriteLine($"{group.Key} months: {group.Count()} loans, total {group.Sum(l => l.Amount)}");
    }


    async Task<string> FetchUserAsync(int id)
    {
        await Task.Delay(1000);
        return $"user-{id}";
    }

    var u = await FetchUserAsync(1);
    Console.WriteLine(u);


    sw.Restart();
    List<Task<decimal>> tasks = filtered
        .Select(loan => fb.GetCreditScoreAsync(loan.CustomerId))
        .ToList();

    try
    {
        decimal[] scores = await Task.WhenAll(tasks);
    }
    catch{
        var succeeded = tasks
            .Where(t => t.IsCompletedSuccessfully)
            .Select(t => t.Result);

        Console.WriteLine($"Succeeded: {string.Join(", ", succeeded)}");
    }
    Console.WriteLine($"Parallel: {sw.ElapsedMilliseconds} ms");


    double[] scores2 = [0.9, 0.8, 0.7, 0.6, 0.4, 0.2];
    bool[] labels   = [true, true, false, true, false, false];

    var c = Metrics.Confusion(scores2, labels, 0.5);
    Console.WriteLine($"TP={c.TP} FP={c.FP} FN={c.FN} TN={c.TN}");

}catch(ArgumentOutOfRangeException e) {
    Console.WriteLine(e);
}


