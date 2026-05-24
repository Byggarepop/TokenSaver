namespace RoslynLean.Tests.Fixtures;

public interface ICalculator
{
    double Compute(double x, double y);

    string Label { get; }

    // default implementation — body must appear in FocusType (non-private)
    double ComputeWithDefault(double x, double y) => Compute(x, y) * 2.0;

    // explicitly private — signature only in FocusType
    private double Scale(double v) => v / 100.0;
}
