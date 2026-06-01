namespace Tractor80.Core;

public enum PlayerPosition
{
    South = 0,
    East = 1,
    North = 2,
    West = 3
}

public enum Team
{
    NorthSouth,
    EastWest
}

public static class PlayerPositionExtensions
{
    public static PlayerPosition NextAntiClockwise(this PlayerPosition position)
    {
        return position switch
        {
            PlayerPosition.South => PlayerPosition.East,
            PlayerPosition.East => PlayerPosition.North,
            PlayerPosition.North => PlayerPosition.West,
            PlayerPosition.West => PlayerPosition.South,
            _ => PlayerPosition.South
        };
    }

    public static Team Team(this PlayerPosition position)
    {
        return position is PlayerPosition.North or PlayerPosition.South
            ? Core.Team.NorthSouth
            : Core.Team.EastWest;
    }

    public static bool IsPartnerOf(this PlayerPosition position, PlayerPosition other)
    {
        return position.Team() == other.Team();
    }

    public static string DisplayName(this PlayerPosition position)
    {
        return position switch
        {
            PlayerPosition.South => "You",
            PlayerPosition.North => "North",
            PlayerPosition.East => "East",
            PlayerPosition.West => "West",
            _ => position.ToString()
        };
    }
}

