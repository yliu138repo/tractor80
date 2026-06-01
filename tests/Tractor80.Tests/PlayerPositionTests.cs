using Tractor80.Core;

namespace Tractor80.Tests;

public sealed class PlayerPositionTests
{
    [Fact]
    public void Positions_AdvanceAntiClockwise()
    {
        Assert.Equal(PlayerPosition.East, PlayerPosition.South.NextAntiClockwise());
        Assert.Equal(PlayerPosition.North, PlayerPosition.East.NextAntiClockwise());
        Assert.Equal(PlayerPosition.West, PlayerPosition.North.NextAntiClockwise());
        Assert.Equal(PlayerPosition.South, PlayerPosition.West.NextAntiClockwise());
    }

    [Fact]
    public void Positions_ReportTeamsPartnersAndNames()
    {
        Assert.Equal(Team.NorthSouth, PlayerPosition.South.Team());
        Assert.Equal(Team.NorthSouth, PlayerPosition.North.Team());
        Assert.Equal(Team.EastWest, PlayerPosition.East.Team());
        Assert.True(PlayerPosition.South.IsPartnerOf(PlayerPosition.North));
        Assert.False(PlayerPosition.South.IsPartnerOf(PlayerPosition.East));

        Assert.Equal("You", PlayerPosition.South.DisplayName());
        Assert.Equal("North", PlayerPosition.North.DisplayName());
        Assert.Equal("East", PlayerPosition.East.DisplayName());
        Assert.Equal("West", PlayerPosition.West.DisplayName());
    }
}
