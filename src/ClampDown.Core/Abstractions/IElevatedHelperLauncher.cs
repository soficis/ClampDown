namespace ClampDown.Core.Abstractions;

public interface IElevatedHelperLauncher
{
    bool TryStart(out string? errorMessage);
}
