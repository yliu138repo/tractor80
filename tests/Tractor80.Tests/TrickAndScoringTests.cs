using Tractor80.Core;

namespace Tractor80.Tests;

public sealed class TrickAndScoringTests
{
    [Fact]
    public void SingleTrick_TrumpBeatsLedSuitAndFirstEqualWins()
    {
        var trump = new TrumpConfig(Rank.Five, Suit.Diamonds);
        var trick = new Trick(PlayerPosition.South, trump);

        Assert.True(trick.AddPlay(PlayerPosition.South, [C(1, Suit.Hearts, Rank.Seven)], [C(1, Suit.Hearts, Rank.Seven)]).IsValid);
        Assert.True(trick.AddPlay(PlayerPosition.East, [C(2, Suit.Spades, Rank.Jack)], [C(2, Suit.Spades, Rank.Jack)]).IsValid);
        Assert.True(trick.AddPlay(PlayerPosition.North, [C(3, Suit.Diamonds, Rank.Two)], [C(3, Suit.Diamonds, Rank.Two)]).IsValid);
        Assert.True(trick.AddPlay(PlayerPosition.West, [C(4, Suit.Clubs, Rank.Five)], [C(4, Suit.Clubs, Rank.Five)]).IsValid);

        Assert.Equal(PlayerPosition.West, trick.Winner());
    }

    [Fact]
    public void PairTrick_UnpairedHighTrumpsCannotBeatPair()
    {
        var trump = new TrumpConfig(Rank.Three, Suit.Hearts);
        var trick = new Trick(PlayerPosition.South, trump);
        var south = new[] { C(1, Suit.Clubs, Rank.Jack), C(2, Suit.Clubs, Rank.Jack) };
        var east = new[] { J(3, JokerColor.Red), J(4, JokerColor.Black) };
        var north = new[] { C(5, Suit.Clubs, Rank.Ace), C(6, Suit.Clubs, Rank.Two) };
        var west = new[] { C(7, Suit.Diamonds, Rank.Four), C(8, Suit.Diamonds, Rank.Six) };

        Assert.True(trick.AddPlay(PlayerPosition.South, south, south).IsValid);
        Assert.True(trick.AddPlay(PlayerPosition.East, east, east).IsValid);
        Assert.True(trick.AddPlay(PlayerPosition.North, north, north).IsValid);
        Assert.True(trick.AddPlay(PlayerPosition.West, west, west).IsValid);

        Assert.Equal(PlayerPosition.South, trick.Winner());
    }

    [Fact]
    public void PairTrick_VoidTrumpPairCanWin()
    {
        var trump = new TrumpConfig(Rank.Three, Suit.Hearts);
        var trick = new Trick(PlayerPosition.South, trump);
        var south = new[] { C(1, Suit.Clubs, Rank.Jack), C(2, Suit.Clubs, Rank.Jack) };
        var east = new[] { C(3, Suit.Hearts, Rank.Two), C(4, Suit.Hearts, Rank.Two) };
        var north = new[] { C(5, Suit.Clubs, Rank.Ace), C(6, Suit.Clubs, Rank.Two) };
        var west = new[] { C(7, Suit.Diamonds, Rank.Four), C(8, Suit.Diamonds, Rank.Six) };

        Assert.True(trick.AddPlay(PlayerPosition.South, south, south).IsValid);
        Assert.True(trick.AddPlay(PlayerPosition.East, east, east).IsValid);
        Assert.True(trick.AddPlay(PlayerPosition.North, north, north).IsValid);
        Assert.True(trick.AddPlay(PlayerPosition.West, west, west).IsValid);

        Assert.Equal(PlayerPosition.East, trick.Winner());
    }

    [Fact]
    public void TractorTrick_HigherTrumpTractorWins()
    {
        var trump = new TrumpConfig(Rank.Four, Suit.Diamonds);
        var trick = new Trick(PlayerPosition.South, trump);
        var south = new[]
        {
            C(1, Suit.Clubs, Rank.Eight),
            C(2, Suit.Clubs, Rank.Eight),
            C(3, Suit.Clubs, Rank.Nine),
            C(4, Suit.Clubs, Rank.Nine)
        };
        var east = new[]
        {
            C(5, Suit.Diamonds, Rank.Six),
            C(6, Suit.Diamonds, Rank.Six),
            C(7, Suit.Diamonds, Rank.Seven),
            C(8, Suit.Diamonds, Rank.Seven)
        };
        var north = new[] { C(9, Suit.Clubs, Rank.Two), C(10, Suit.Clubs, Rank.Three), C(11, Suit.Hearts, Rank.King), C(12, Suit.Hearts, Rank.Ace) };
        var west = new[] { C(13, Suit.Spades, Rank.Six), C(14, Suit.Spades, Rank.Seven), C(15, Suit.Spades, Rank.Eight), C(16, Suit.Spades, Rank.Nine) };

        Assert.True(trick.AddPlay(PlayerPosition.South, south, south).IsValid);
        Assert.True(trick.AddPlay(PlayerPosition.East, east, east).IsValid);
        Assert.True(trick.AddPlay(PlayerPosition.North, north, north).IsValid);
        Assert.True(trick.AddPlay(PlayerPosition.West, west, west).IsValid);

        Assert.Equal(PlayerPosition.East, trick.Winner());
    }

    [Theory]
    [InlineData(0, false, 0, false, 3, Team.NorthSouth)]
    [InlineData(35, false, 0, false, 2, Team.NorthSouth)]
    [InlineData(75, false, 0, false, 1, Team.NorthSouth)]
    [InlineData(80, false, 0, true, 0, Team.EastWest)]
    [InlineData(120, false, 0, true, 1, Team.EastWest)]
    [InlineData(160, false, 0, true, 2, Team.EastWest)]
    [InlineData(200, false, 0, true, 3, Team.EastWest)]
    [InlineData(240, false, 0, true, 4, Team.EastWest)]
    public void RoundScorer_AppliesPromotionTable(
        int opponentTrickPoints,
        bool wonLast,
        int kittyPoints,
        bool opponentsWon,
        int levelDelta,
        Team nextDeclarers)
    {
        var result = RoundScorer.Score(opponentTrickPoints, kittyPoints, 1, wonLast, Team.NorthSouth);

        Assert.Equal(opponentsWon, result.OpponentsWon);
        Assert.Equal(levelDelta, result.LevelDelta);
        Assert.Equal(nextDeclarers, result.NextDeclarers);
    }

    [Fact]
    public void RoundScorer_MultipliesKittyWhenOpponentsWinLastTrick()
    {
        var result = RoundScorer.Score(60, 15, 4, true, Team.NorthSouth);

        Assert.Equal(8, result.KittyMultiplier);
        Assert.Equal(180, result.OpponentPoints);
        Assert.True(result.OpponentsWon);
    }

    private static Card C(int id, Suit suit, Rank rank) => new(id, suit, rank, null);

    private static Card J(int id, JokerColor color) => new(id, null, null, color);
}

