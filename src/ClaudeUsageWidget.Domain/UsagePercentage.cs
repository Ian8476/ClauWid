namespace ClaudeUsageWidget.Domain;

/// <summary>
/// Un porcentaje de consumo garantizado dentro de [0, 100].
/// Existe para que ninguna capa superior tenga que volver a validar el rango.
/// </summary>
public readonly record struct UsagePercentage
{
    public const double Minimum = 0d;
    public const double Maximum = 100d;

    public double Value { get; }

    private UsagePercentage(double value) => Value = value;

    public static UsagePercentage FromRaw(double raw)
    {
        if (double.IsNaN(raw) || double.IsInfinity(raw))
        {
            throw new ArgumentOutOfRangeException(
                nameof(raw), raw, "El porcentaje de consumo debe ser un numero finito.");
        }

        return new UsagePercentage(Math.Clamp(raw, Minimum, Maximum));
    }

    public double AsFraction => Value / Maximum;

    /// <summary>The whole percent shown to the user.</summary>
    public double Rounded => Math.Round(Value, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Compares the rounded value, not the raw one: 49.6 reads as "50%", so it has to
    /// count as 50 too, or the label and the band it falls into would disagree.
    /// </summary>
    public bool IsAtLeast(double threshold) => Rounded >= threshold;

    public override string ToString() => Rounded.ToString("0") + "%";
}
