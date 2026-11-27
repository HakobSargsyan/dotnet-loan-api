namespace LoanApp;

public static class Metrics
{
    public static (int TP, int FP, int FN, int TN) Confusion(
        IReadOnlyList<double> scores, IReadOnlyList<bool> labels, double threshold)
    {
        int tp = 0, fp = 0, fn = 0, tn = 0;

        for (int i = 0; i < scores.Count; i++)
        {
            bool predicted = scores[i] >= threshold;
            bool actual = labels[i];

            if (predicted && actual)   tp++;   // call, fire
            if (predicted && !actual)  fp++;   // call, not fire
            if (!predicted && actual)  fn++;   // not call, fire
            if (!predicted && !actual) tn++;   // call, not fire

            Console.WriteLine($"i={i} score={scores[i]} predicted={predicted} actual={actual} → TP={tp} FP={fp} FN={fn} TN={tn}");
        }

        return (tp, fp, fn, tn);
    }
    public static double Precision(int tp, int fp) => throw new NotImplementedException();
    public static double Recall(int tp, int fn) => throw new NotImplementedException();
    public static double F1(double precision, double recall) => throw new NotImplementedException();
    public static double Auc(IReadOnlyList<double> scores, IReadOnlyList<bool> labels) => throw new NotImplementedException();
    public static double Gini(double auc) => 2 * auc - 1;
}