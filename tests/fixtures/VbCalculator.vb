' VbCalculator.vb — fixture for the TokenSaver VB.NET test suite.
' Mirrors the shape of Calculator.cs so the same test patterns apply.
Imports System

Namespace Fixtures

    ' A weighted-average calculator with a bias, written in VB.NET.
    ' Comments are intentionally heavy so the minifier has material to strip.
    Public Class VbCalculator

        ' The fixed additive bias applied after computing the average.
        Private ReadOnly _bias As Double

        Public Property LastMean As Double

        Public Sub New(bias As Double)
            _bias = bias
        End Sub

        Public Function Run(values() As Double, weights() As Double) As Double
            ' Guard: lengths must match for the weighted average to be meaningful.
            If values.Length <> weights.Length Then
                Throw New ArgumentException("values and weights must be the same length")
            End If

            Dim total = WeightedSum(values, weights)
            Dim weight = Sum(weights)
            Dim raw = If(weight = 0, 0.0, total / weight)
            Dim biased = ApplyBias(raw)
            LastMean = Math.Max(0, biased)
            Return LastMean
        End Function

        Private Shared Function WeightedSum(values() As Double, weights() As Double) As Double
            Dim s As Double = 0
            For i = 0 To values.Length - 1
                s += values(i) * weights(i)
            Next
            Return s
        End Function

        Private Shared Function Sum(xs() As Double) As Double
            Dim s As Double = 0
            For i = 0 To xs.Length - 1
                s += xs(i)
            Next
            Return s
        End Function

        ' Add the configured bias to x.
        Private Function ApplyBias(x As Double) As Double
            REM This is the bias application step
            Return x + _bias
        End Function

    End Class

End Namespace
