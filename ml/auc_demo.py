"""
AUC vs precision demo (pure Python, no libraries).

Part 1: the 6-customer example, AUC calculated two ways (pairs and ROC area).
Part 2: the SAME model on portfolios with different default rates:
        AUC / Gini stay the same, precision changes a lot.

Run:  python3 ml/auc_demo.py
"""
import random

# ---------------------------------------------------------------------------
# Metrics
# ---------------------------------------------------------------------------

def auc_by_pairs(scores, labels):
    """For every (bad, good) pair: did the model give the bad one a higher score?
    O(n^2): fine for small data only."""
    bad = [s for s, y in zip(scores, labels) if y == 1]
    good = [s for s, y in zip(scores, labels) if y == 0]
    wins = 0.0
    for b in bad:
        for g in good:
            if b > g:
                wins += 1
            elif b == g:
                wins += 0.5
    return wins / (len(bad) * len(good))


def roc_points(scores, labels):
    """Lower the threshold one customer at a time: bad = step up, good = step right."""
    pairs = sorted(zip(scores, labels), reverse=True)
    n_bad = sum(labels)
    n_good = len(labels) - n_bad
    tp = fp = 0
    points = [(0.0, 0.0)]  # (FPR, TPR)
    for _, y in pairs:
        if y == 1:
            tp += 1
        else:
            fp += 1
        points.append((fp / n_good, tp / n_bad))
    return points


def area_under(points):
    """Trapezoid rule over (x, y) points."""
    area = 0.0
    for (x1, y1), (x2, y2) in zip(points, points[1:]):
        area += (x2 - x1) * (y1 + y2) / 2
    return area


def auc_fast(scores, labels):
    """Same answer as auc_by_pairs, but O(n log n) using ranks (Mann-Whitney U).
    Needed for 1,000,000 rows. (Ignores ties: fine for continuous scores.)"""
    order = sorted(range(len(scores)), key=lambda i: scores[i])
    rank_sum = 0
    n_bad = 0
    for rank, i in enumerate(order, start=1):
        if labels[i] == 1:
            rank_sum += rank
            n_bad += 1
    n_good = len(scores) - n_bad
    return (rank_sum - n_bad * (n_bad + 1) / 2) / (n_bad * n_good)


def average_precision(scores, labels):
    """PR-AUC: walk from highest score down, average the precision at every bad customer."""
    pairs = sorted(zip(scores, labels), reverse=True)
    n_bad = sum(labels)
    tp = 0
    total = 0.0
    for k, (_, y) in enumerate(pairs, start=1):
        if y == 1:
            tp += 1
            total += tp / k
    return total / n_bad


def at_threshold(scores, labels, threshold):
    """Reject everyone with score >= threshold."""
    tp = fp = fn = tn = 0
    for s, y in zip(scores, labels):
        rejected = s >= threshold
        if y == 1 and rejected:
            tp += 1
        elif y == 0 and rejected:
            fp += 1
        elif y == 1:
            fn += 1
        else:
            tn += 1
    return {
        "TP": tp, "FP": fp, "FN": fn, "TN": tn,
        "TPR(recall)": tp / (tp + fn),
        "FPR": fp / (fp + tn),
        "precision": tp / (tp + fp) if tp + fp else 0.0,
    }


# ---------------------------------------------------------------------------
# Part 1: the 6-customer example
# ---------------------------------------------------------------------------

print("=" * 70)
print("PART 1: 6 customers (1 = bad, did not repay; 0 = good)")
print("=" * 70)

scores = [0.9, 0.8, 0.7, 0.6, 0.4, 0.2]
labels = [1,   1,   0,   1,   0,   0]

for s, y in zip(scores, labels):
    print(f"  score {s:.1f}  ->  {'B (bad)' if y else 'G (good)'}")

pts = roc_points(scores, labels)
print("\nROC points (FPR, TPR):", [(round(x, 2), round(y, 2)) for x, y in pts])
print(f"AUC by pairs      : {auc_by_pairs(scores, labels):.4f}   (8/9 = {8/9:.4f})")
print(f"AUC by ROC area   : {area_under(pts):.4f}")
print(f"AUC fast (ranks)  : {auc_fast(scores, labels):.4f}")
print(f"Gini = 2*AUC - 1  : {2 * auc_by_pairs(scores, labels) - 1:.4f}")

# ---------------------------------------------------------------------------
# Part 2: same model, different default rates
# ---------------------------------------------------------------------------
# The "model": bad customers get a risk score around 1.0, good ones around 0.0,
# both with the same spread. This never changes below: only the mix of people does.

random.seed(42)


def simulate(n, default_rate):
    labels = [1 if random.random() < default_rate else 0 for _ in range(n)]
    scores = [random.gauss(1.0 if y else 0.0, 1.0) for y in labels]
    return scores, labels


THRESHOLD = 1.0  # reject everyone with risk score >= 1.0

print()
print("=" * 70)
print(f"PART 2: SAME model, threshold = {THRESHOLD}, different portfolios")
print("=" * 70)
header = (f"{'portfolio':<24}{'bad':>8}{'AUC':>8}{'Gini':>8}{'recall':>8}{'FPR':>7}"
          f"{'precision':>11}{'PR-AUC':>9}{'false alarms per real':>24}")
print(header)
print("-" * len(header))

for name, n, rate in [
    ("consumer loans (20%)", 100_000, 0.20),
    ("mortgages (5%)", 100_000, 0.05),
    ("fraud / AML (0.1%)", 1_000_000, 0.001),
]:
    s, y = simulate(n, rate)
    auc = auc_fast(s, y)
    m = at_threshold(s, y, THRESHOLD)
    ap = average_precision(s, y)
    print(f"{name:<24}{sum(y):>8}{auc:>8.3f}{2 * auc - 1:>8.3f}"
          f"{m['TPR(recall)']:>8.2f}{m['FPR']:>7.2f}{m['precision']:>11.3f}{ap:>9.3f}"
          f"{m['FP'] / m['TP']:>24.1f}")

print()
print("Read it like this:")
print("  AUC, Gini, recall, FPR  -> almost identical in every row (the model did not change)")
print("  precision, PR-AUC       -> collapse as bad customers get rarer")
print("  fraud row: for every real case flagged, count the false alarms -> FP / TP")
