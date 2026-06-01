using Tractor80.Core;

namespace Tractor80.Tests;

public sealed class AiAndRoundTests
{
    [Fact]
    public void Ai_ChoosesTrumpFromStrongestSuit()
    {
        var hand = new[]
        {
            C(1, Suit.Spades, Rank.Two),
            C(2, Suit.Spades, Rank.Ace),
            C(3, Suit.Spades, Rank.King),
            C(4, Suit.Hearts, Rank.Two),
            C(5, Suit.Clubs, Rank.Seven)
        };

        var trump = SeniorAiPlayer.ChooseTrump(hand, Rank.Two);

        Assert.Equal(Suit.Spades, trump.Suit);
    }

    [Fact]
    public void Ai_DiscardAvoidsPointsAndTrumpsFirst()
    {
        var trump = new TrumpConfig(Rank.Two, Suit.Spades);
        var hand = new[]
        {
            C(1, Suit.Clubs, Rank.Three),
            C(2, Suit.Diamonds, Rank.Four),
            C(3, Suit.Spades, Rank.Four),
            C(4, Suit.Hearts, Rank.King),
            C(5, Suit.Clubs, Rank.Five)
        };

        var discard = SeniorAiPlayer.ChooseDiscard(hand, trump, 2);

        Assert.Equal([hand[0], hand[1]], discard);
    }

    [Fact]
    public void Ai_FollowPlayIsRuleLegal()
    {
        var trump = new TrumpConfig(Rank.Two, Suit.Spades);
        var trick = new Trick(PlayerPosition.South, trump);
        var lead = new[] { C(1, Suit.Hearts, Rank.Ace), C(2, Suit.Hearts, Rank.Ace) };
        Assert.True(trick.AddPlay(PlayerPosition.South, lead, lead).IsValid);

        var hand = new[]
        {
            C(3, Suit.Hearts, Rank.Three),
            C(4, Suit.Hearts, Rank.Three),
            C(5, Suit.Clubs, Rank.King),
            C(6, Suit.Spades, Rank.Ace)
        };
        var ai = new SeniorAiPlayer(AiPersona.PointHunter);

        var play = ai.ChoosePlay(PlayerPosition.East, hand, trick, 50, []);
        var validator = new PlayValidator(trump);

        Assert.True(validator.ValidateFollow(hand, trick.LeadPattern!, play).IsValid);
        Assert.Equal(2, play.Length);
        Assert.All(play, card => Assert.Equal(Suit.Hearts, card.Suit));
    }

    [Fact]
    public void LegalPlayGenerator_ProducesTractors()
    {
        var trump = new TrumpConfig(Rank.Four, Suit.Diamonds);
        var hand = new[]
        {
            C(1, Suit.Clubs, Rank.Six),
            C(2, Suit.Clubs, Rank.Six),
            C(3, Suit.Clubs, Rank.Seven),
            C(4, Suit.Clubs, Rank.Seven),
            C(5, Suit.Clubs, Rank.Nine)
        };

        var tractors = LegalPlayGenerator.Tractors(hand, trump).ToArray();

        Assert.Single(tractors);
        Assert.Equal(4, tractors[0].Length);
    }

    [Fact]
    public void LegalPlayGenerator_UsesHeuristicFollowsForLongLeadsWhenShort()
    {
        var trump = new TrumpConfig(Rank.Two, Suit.Spades);
        var lead = new LeadAnalyzer(trump).Analyze([
            C(1, Suit.Hearts, Rank.Six),
            C(2, Suit.Hearts, Rank.Six),
            C(3, Suit.Hearts, Rank.Seven),
            C(4, Suit.Hearts, Rank.Seven),
            C(5, Suit.Hearts, Rank.Eight),
            C(6, Suit.Hearts, Rank.Eight)
        ])!;
        var hand = new[]
        {
            C(7, Suit.Hearts, Rank.Three),
            C(8, Suit.Hearts, Rank.Four),
            C(9, Suit.Clubs, Rank.Three),
            C(10, Suit.Clubs, Rank.King),
            C(11, Suit.Diamonds, Rank.Five),
            C(12, Suit.Spades, Rank.Ace)
        };

        var follows = LegalPlayGenerator.GenerateFollows(hand, lead, trump).ToArray();

        Assert.NotEmpty(follows);
        Assert.All(follows, play =>
        {
            Assert.Equal(6, play.Length);
            Assert.Contains(hand[0], play);
            Assert.Contains(hand[1], play);
        });
    }

    [Fact]
    public void LegalPlayGenerator_UsesHeuristicFollowsForLongLeadsWhenFlush()
    {
        var trump = new TrumpConfig(Rank.Two, Suit.Spades);
        var lead = new LeadAnalyzer(trump).Analyze([
            C(1, Suit.Hearts, Rank.Six),
            C(2, Suit.Hearts, Rank.Six),
            C(3, Suit.Hearts, Rank.Seven),
            C(4, Suit.Hearts, Rank.Seven),
            C(5, Suit.Hearts, Rank.Eight),
            C(6, Suit.Hearts, Rank.Eight)
        ])!;
        var hand = new[]
        {
            C(7, Suit.Hearts, Rank.Three),
            C(8, Suit.Hearts, Rank.Three),
            C(9, Suit.Hearts, Rank.Four),
            C(10, Suit.Hearts, Rank.Four),
            C(11, Suit.Hearts, Rank.Five),
            C(12, Suit.Hearts, Rank.Five),
            C(13, Suit.Hearts, Rank.Ace)
        };

        var follows = LegalPlayGenerator.GenerateFollows(hand, lead, trump).ToArray();

        Assert.NotEmpty(follows);
        Assert.Contains(follows, play => play.Length == 6 && play.All(card => card.Suit == Suit.Hearts));
    }

    [Fact]
    public void GameRound_DealsExpectedCountsAndUniqueCards()
    {
        var round = GameRound.CreateNew(1234);

        Assert.Equal(8, round.Kitty.Length);
        Assert.All(Enum.GetValues<PlayerPosition>(), player => Assert.Equal(25, round.Hand(player).Count));
        Assert.Equal(108, round.Hands.Values.SelectMany(cards => cards).Concat(round.Kitty).Select(card => card.Id).Distinct().Count());
        Assert.Equal(PlayerPosition.South, round.CurrentTrick.ExpectedPlayer);
    }

    [Fact]
    public void GameRound_CanStartWithHumanOnScoringSide()
    {
        var round = GameRound.CreateNew(4321, starter: PlayerPosition.East);

        Assert.Equal(Team.EastWest, round.Declarers);
        Assert.Equal(Team.NorthSouth, round.Opponents);
        Assert.Equal(round.Opponents, PlayerPosition.South.Team());
        Assert.Equal(PlayerPosition.East, round.CurrentTrick.ExpectedPlayer);
    }

    [Fact]
    public void GameRound_CanBeCompletedByAiWithoutIllegalMoves()
    {
        var round = GameRound.CreateNew(20260528);
        var ai = new Dictionary<PlayerPosition, SeniorAiPlayer>
        {
            [PlayerPosition.South] = new(AiPersona.PartnerProtector),
            [PlayerPosition.North] = new(AiPersona.PartnerProtector),
            [PlayerPosition.East] = new(AiPersona.PointHunter),
            [PlayerPosition.West] = new(AiPersona.TrumpController)
        };
        var guard = 0;

        while (round.Phase == RoundPhase.Playing && guard++ < 150)
        {
            var player = round.CurrentTrick.ExpectedPlayer;
            var play = ai[player].ChoosePlay(player, round.Hand(player), round.CurrentTrick, round.OpponentTrickPoints, round.PlayedCards);
            var result = round.Play(player, play);
            Assert.True(result.IsValid, result.Message);
        }

        Assert.Equal(RoundPhase.Complete, round.Phase);
        Assert.NotNull(round.Result);
        Assert.InRange(round.Result!.OpponentPoints, 0, 400);
    }

    private static Card C(int id, Suit suit, Rank rank) => new(id, suit, rank, null);
}
