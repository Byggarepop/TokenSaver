using System;

namespace Fixtures;

/// <summary>
/// A small calculator with a heavy comment burden so the minifier has something to chew on.
/// The class is used by tests to demonstrate Focus/Minify behaviours on a realistic shape.
/// </summary>
public sealed class Calculator
{
    private readonly double _bias;

    /// <summary>The arithmetic mean of the last <see cref="Run"/> call, or zero.</summary>
    public double LastMean { get; private set; }

    /// <summary>Create a calculator with a fixed additive bias.</summary>
    public Calculator(double bias) { _bias = bias; }

    /// <summary>
    /// Compute a biased weighted average across <paramref name="values"/>,
    /// applying the configured bias and clamping to non-negative.
    /// </summary>
    public double Run(double[] values, double[] weights)
    {
        // Guard: lengths must match for the weighted average to be meaningful.
        if (values.Length != weights.Length)
            throw new ArgumentException("values and weights must be the same length");

        var total = WeightedSum(values, weights);
        var weight = Sum(weights);
        var raw = weight == 0 ? 0 : total / weight;
        var biased = ApplyBias(raw);
        LastMean = Math.Max(0, biased);
        return LastMean;
    }

    private static double WeightedSum(double[] values, double[] weights)
    {
        double s = 0;
        for (int i = 0; i < values.Length; i++)
            s += values[i] * weights[i];
        return s;
    }

    private static double Sum(double[] xs)
    {
        double s = 0;
        for (int i = 0; i < xs.Length; i++) s += xs[i];
        return s;
    }

    /// <summary>Add the configured bias.</summary>
    private double ApplyBias(double x) => x + _bias;
}
