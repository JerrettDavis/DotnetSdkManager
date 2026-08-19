using System.Globalization;

namespace DotnetSdkManager.Models;

public sealed class DotnetSdkVersion : IComparable<DotnetSdkVersion>, IEquatable<DotnetSdkVersion>
{
    private readonly int[] _numbers;
    private readonly string[] _prereleaseIdentifiers;

    private DotnetSdkVersion(string original, int[] numbers, string[] prereleaseIdentifiers)
    {
        Original = original;
        _numbers = numbers;
        _prereleaseIdentifiers = prereleaseIdentifiers;
    }

    public string Original { get; }

    public int Major => _numbers.Length > 0 ? _numbers[0] : 0;

    public int Minor => _numbers.Length > 1 ? _numbers[1] : 0;

    public bool IsPrerelease => _prereleaseIdentifiers.Length > 0;

    public string Channel => $"{Major.ToString(CultureInfo.InvariantCulture)}.{Minor.ToString(CultureInfo.InvariantCulture)}";

    public static DotnetSdkVersion Parse(string value)
    {
        if (!TryParse(value, out var version))
        {
            throw new FormatException($"'{value}' is not a valid .NET SDK version.");
        }

        return version;
    }

    public static bool TryParse(string? value, out DotnetSdkVersion version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        var dash = trimmed.IndexOf('-');
        var numericPart = dash >= 0 ? trimmed[..dash] : trimmed;
        var prereleasePart = dash >= 0 ? trimmed[(dash + 1)..] : string.Empty;
        var numericTokens = numericPart.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (numericTokens.Length < 2)
        {
            return false;
        }

        var numbers = new int[numericTokens.Length];
        for (var i = 0; i < numericTokens.Length; i++)
        {
            if (!int.TryParse(numericTokens[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]) || numbers[i] < 0)
            {
                return false;
            }
        }

        var prerelease = string.IsNullOrEmpty(prereleasePart)
            ? []
            : prereleasePart.Split(['.', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        version = new DotnetSdkVersion(trimmed, numbers, prerelease);
        return true;
    }

    public int CompareTo(DotnetSdkVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var max = Math.Max(_numbers.Length, other._numbers.Length);
        for (var i = 0; i < max; i++)
        {
            var left = i < _numbers.Length ? _numbers[i] : 0;
            var right = i < other._numbers.Length ? other._numbers[i] : 0;
            var numberComparison = left.CompareTo(right);
            if (numberComparison != 0)
            {
                return numberComparison;
            }
        }

        if (!IsPrerelease && other.IsPrerelease)
        {
            return 1;
        }

        if (IsPrerelease && !other.IsPrerelease)
        {
            return -1;
        }

        var prereleaseCount = Math.Max(_prereleaseIdentifiers.Length, other._prereleaseIdentifiers.Length);
        for (var i = 0; i < prereleaseCount; i++)
        {
            if (i >= _prereleaseIdentifiers.Length)
            {
                return -1;
            }

            if (i >= other._prereleaseIdentifiers.Length)
            {
                return 1;
            }

            var comparison = CompareIdentifier(_prereleaseIdentifiers[i], other._prereleaseIdentifiers[i]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return StringComparer.OrdinalIgnoreCase.Compare(Original, other.Original);
    }

    public bool Equals(DotnetSdkVersion? other) => other is not null && CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is DotnetSdkVersion other && Equals(other);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Original);

    public override string ToString() => Original;

    private static int CompareIdentifier(string left, string right)
    {
        var leftNumeric = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightNumeric = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);

        if (leftNumeric && rightNumeric)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (leftNumeric)
        {
            return -1;
        }

        if (rightNumeric)
        {
            return 1;
        }

        return StringComparer.OrdinalIgnoreCase.Compare(left, right);
    }
}
