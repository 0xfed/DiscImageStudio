using DiscImageStudio.Localization;
namespace DvdImageSolver.Encoding;

public readonly record struct ModulationBoundary(byte State, int Dsv, bool IsLand)
{
    public static ModulationBoundary Validate(byte state, int dsv, bool isLand)
    {
        if (state is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(state), L.T("EFMPlus state must be 1..4."));
        }

        return new ModulationBoundary(state, dsv, isLand);
    }

    public string LevelName => IsLand ? "land" : "pit";
}
