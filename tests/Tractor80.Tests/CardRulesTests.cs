using Tractor80.Core;

namespace Tractor80.Tests;

public sealed class CardRulesTests
{
    [Fact]
    public void DoubleDeck_HasExpectedCardsAndPointTotal()
    {
        var deck = DeckFactory.CreateDoubleDeck();

        Assert.Equal(108, deck.Length);
        Assert.Equal(200, CardRules.CountPoints(deck));
        Assert.Equal(2, deck.Count(card => card.FaceKey == "Hearts:Five"));
        Assert.Equal(2, deck.Count(card => card.Joker == JokerColor.Red));
        Assert.Equal(deck.Length, deck.Select(card => card.Id).Distinct().Count());
    }

    [Fact]
    public void Shuffle_IsDeterministicForSeed()
    {
        var first = DeckFactory.Shuffle(42).Select(card => card.Id).ToArray();
        var second = DeckFactory.Shuffle(42).Select(card => card.Id).ToArray();
        var third = DeckFactory.Shuffle(43).Select(card => card.Id).ToArray();

        Assert.Equal(first, second);
        Assert.NotEqual(first, third);
    }

    [Fact]
    public void TrumpOrdering_FollowsPagatRankModel()
    {
        var trump = new TrumpConfig(Rank.Seven, Suit.Clubs);
        var redJoker = J(1, JokerColor.Red);
        var blackJoker = J(2, JokerColor.Black);
        var mainSeven = C(3, Suit.Clubs, Rank.Seven);
        var sideSeven = C(4, Suit.Hearts, Rank.Seven);
        var trumpAce = C(5, Suit.Clubs, Rank.Ace);
        var offAce = C(6, Suit.Spades, Rank.Ace);

        Assert.True(CardRules.IsTrump(redJoker, trump));
        Assert.True(CardRules.IsTrump(sideSeven, trump));
        Assert.True(CardRules.Power(redJoker, trump) > CardRules.Power(blackJoker, trump));
        Assert.True(CardRules.Power(blackJoker, trump) > CardRules.Power(mainSeven, trump));
        Assert.True(CardRules.Power(mainSeven, trump) > CardRules.Power(sideSeven, trump));
        Assert.True(CardRules.Power(sideSeven, trump) > CardRules.Power(trumpAce, trump));
        Assert.True(CardRules.Power(trumpAce, trump) > CardRules.Power(offAce, trump));
    }

    [Fact]
    public void NoTrump_OnlyJokersAreTrumpAndRankIsNatural()
    {
        var noTrump = new TrumpConfig(Rank.Two, null);
        var two = C(1, Suit.Spades, Rank.Two);
        var joker = J(2, JokerColor.Black);

        Assert.False(CardRules.IsTrump(two, noTrump));
        Assert.True(CardRules.IsTrump(joker, noTrump));
        Assert.Equal(CardGroup.Spades, CardRules.EffectiveGroup(two, noTrump));
        Assert.Equal(CardGroup.Trump, CardRules.EffectiveGroup(joker, noTrump));
    }

    [Fact]
    public void CardDisplayHelpers_CoverRanksSuitsAndJokers()
    {
        var ranks = Enum.GetValues<Rank>().Select(Card.RankText).ToArray();
        var suits = Enum.GetValues<Suit>().Select(Card.SuitText).ToArray();
        var blackJoker = J(1, JokerColor.Black);
        var redJoker = J(2, JokerColor.Red);
        var ace = C(3, Suit.Spades, Rank.Ace);

        Assert.Equal(["2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A"], ranks);
        Assert.Equal(["C", "D", "H", "S"], suits);
        Assert.Equal("BJ", blackJoker.ShortName);
        Assert.Equal("RJ", redJoker.ShortName);
        Assert.Equal("AS", ace.ShortName);
        Assert.Equal("AS", ace.ToString());
    }

    [Fact]
    public void TrumpConfig_DisplayNameHandlesSuitAndNoSuit()
    {
        Assert.Equal("8 / H", new TrumpConfig(Rank.Eight, Suit.Hearts).DisplayName);
        Assert.Equal("K / No Suit", new TrumpConfig(Rank.King, null).DisplayName);
    }

    [Fact]
    public void NaturalSequenceRanks_SkipTrumpRank()
    {
        var trump = new TrumpConfig(Rank.Four, Suit.Diamonds);
        var ranks = CardRules.NaturalSequenceRanks(trump);

        Assert.DoesNotContain(Rank.Four, ranks);
        Assert.Equal(Rank.Three, ranks[1]);
        Assert.Equal(Rank.Five, ranks[2]);
    }

    private static Card C(int id, Suit suit, Rank rank) => new(id, suit, rank, null);

    private static Card J(int id, JokerColor color) => new(id, null, null, color);
}
