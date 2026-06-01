using Tractor80.Core;

namespace Tractor80.Tests;

public sealed class LeadAndValidationTests
{
    [Fact]
    public void LeadAnalyzer_AcceptsSinglesPairsAndNaturalTractors()
    {
        var analyzer = new LeadAnalyzer(new TrumpConfig(Rank.Four, Suit.Diamonds));

        Assert.Equal(LeadKind.SingleCard, analyzer.Analyze([C(1, Suit.Clubs, Rank.Ace)])!.Kind);
        Assert.Equal(LeadKind.Pair, analyzer.Analyze([C(1, Suit.Clubs, Rank.Ace), C(2, Suit.Clubs, Rank.Ace)])!.Kind);

        var tractor = analyzer.Analyze([
            C(1, Suit.Clubs, Rank.Eight),
            C(2, Suit.Clubs, Rank.Eight),
            C(3, Suit.Clubs, Rank.Nine),
            C(4, Suit.Clubs, Rank.Nine)
        ]);

        Assert.NotNull(tractor);
        Assert.Equal(LeadKind.Tractor, tractor.Kind);
        Assert.Equal(2, tractor.PairCount);
    }

    [Fact]
    public void LeadAnalyzer_AcceptsSpecialTrumpTractors()
    {
        var analyzer = new LeadAnalyzer(new TrumpConfig(Rank.Four, Suit.Diamonds));

        var aceToSideRank = analyzer.Analyze([
            C(1, Suit.Diamonds, Rank.Ace),
            C(2, Suit.Diamonds, Rank.Ace),
            C(3, Suit.Spades, Rank.Four),
            C(4, Suit.Spades, Rank.Four)
        ]);

        var sideRankToMainRank = analyzer.Analyze([
            C(5, Suit.Hearts, Rank.Four),
            C(6, Suit.Hearts, Rank.Four),
            C(7, Suit.Diamonds, Rank.Four),
            C(8, Suit.Diamonds, Rank.Four)
        ]);

        var jokerTractor = analyzer.Analyze([
            J(9, JokerColor.Black),
            J(10, JokerColor.Black),
            J(11, JokerColor.Red),
            J(12, JokerColor.Red)
        ]);

        Assert.Equal(LeadKind.Tractor, aceToSideRank!.Kind);
        Assert.Equal(LeadKind.Tractor, sideRankToMainRank!.Kind);
        Assert.Equal(LeadKind.Tractor, jokerTractor!.Kind);
    }

    [Fact]
    public void LeadAnalyzer_RejectsMixedOrNonAdjacentCards()
    {
        var analyzer = new LeadAnalyzer(new TrumpConfig(Rank.Four, Suit.Diamonds));

        Assert.Null(analyzer.Analyze([C(1, Suit.Clubs, Rank.Eight), C(2, Suit.Hearts, Rank.Eight)]));
        Assert.Null(analyzer.Analyze([
            C(1, Suit.Spades, Rank.Five),
            C(2, Suit.Spades, Rank.Five),
            C(3, Suit.Spades, Rank.Seven),
            C(4, Suit.Spades, Rank.Seven)
        ]));
    }

    [Fact]
    public void Validator_RequiresSingleFollowWhenAvailable()
    {
        var trump = new TrumpConfig(Rank.Two, Suit.Spades);
        var validator = new PlayValidator(trump);
        var lead = new LeadAnalyzer(trump).Analyze([C(1, Suit.Hearts, Rank.Ace)])!;
        var hand = new[] { C(2, Suit.Hearts, Rank.Three), C(3, Suit.Clubs, Rank.King) };

        Assert.False(validator.ValidateFollow(hand, lead, [hand[1]]).IsValid);
        Assert.True(validator.ValidateFollow(hand, lead, [hand[0]]).IsValid);
    }

    [Fact]
    public void Validator_AllowsOffSuitSingleWhenVoid()
    {
        var trump = new TrumpConfig(Rank.Two, Suit.Spades);
        var validator = new PlayValidator(trump);
        var lead = new LeadAnalyzer(trump).Analyze([C(1, Suit.Hearts, Rank.Ace)])!;
        var hand = new[] { C(2, Suit.Clubs, Rank.Three), C(3, Suit.Diamonds, Rank.King) };

        Assert.True(validator.ValidateFollow(hand, lead, [hand[1]]).IsValid);
    }

    [Fact]
    public void Validator_RequiresPairWhenPairExists()
    {
        var trump = new TrumpConfig(Rank.Two, Suit.Spades);
        var validator = new PlayValidator(trump);
        var lead = new LeadAnalyzer(trump).Analyze([C(1, Suit.Hearts, Rank.Ace), C(2, Suit.Hearts, Rank.Ace)])!;
        var hand = new[]
        {
            C(3, Suit.Hearts, Rank.Three),
            C(4, Suit.Hearts, Rank.Three),
            C(5, Suit.Hearts, Rank.King),
            C(6, Suit.Clubs, Rank.King)
        };

        Assert.False(validator.ValidateFollow(hand, lead, [hand[0], hand[2]]).IsValid);
        Assert.True(validator.ValidateFollow(hand, lead, [hand[0], hand[1]]).IsValid);
    }

    [Fact]
    public void Validator_RequiresAllSuitCardsWhenShort()
    {
        var trump = new TrumpConfig(Rank.Two, Suit.Spades);
        var validator = new PlayValidator(trump);
        var lead = new LeadAnalyzer(trump).Analyze([
            C(1, Suit.Hearts, Rank.Seven),
            C(2, Suit.Hearts, Rank.Seven),
            C(3, Suit.Hearts, Rank.Eight),
            C(4, Suit.Hearts, Rank.Eight)
        ])!;
        var hand = new[]
        {
            C(5, Suit.Hearts, Rank.Three),
            C(6, Suit.Hearts, Rank.Four),
            C(7, Suit.Clubs, Rank.King),
            C(8, Suit.Diamonds, Rank.King)
        };

        Assert.False(validator.ValidateFollow(hand, lead, [hand[0], hand[2], hand[3], C(9, Suit.Clubs, Rank.Ace)]).IsValid);
        Assert.True(validator.ValidateFollow(hand, lead, hand).IsValid);
    }

    private static Card C(int id, Suit suit, Rank rank) => new(id, suit, rank, null);

    private static Card J(int id, JokerColor color) => new(id, null, null, color);
}
