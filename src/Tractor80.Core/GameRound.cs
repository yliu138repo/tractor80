using System.Collections.Immutable;

namespace Tractor80.Core;

public enum RoundPhase
{
    Playing,
    Complete
}

public sealed record RoundResult(
    int OpponentPoints,
    int KittyPoints,
    int KittyMultiplier,
    bool OpponentsWon,
    int LevelDelta,
    Team NextDeclarers);

public sealed class GameRound
{
    private readonly Dictionary<PlayerPosition, List<Card>> _hands;
    private readonly List<Card> _playedCards = [];

    private GameRound(
        TrumpConfig trump,
        PlayerPosition starter,
        IReadOnlyDictionary<PlayerPosition, List<Card>> hands,
        ImmutableArray<Card> kitty)
    {
        Trump = trump;
        Starter = starter;
        CurrentTrick = new Trick(starter, trump);
        _hands = hands.ToDictionary(item => item.Key, item => item.Value);
        Kitty = kitty;
    }

    public TrumpConfig Trump { get; }

    public PlayerPosition Starter { get; }

    public Team Declarers => Starter.Team();

    public Team Opponents => Declarers == Team.NorthSouth ? Team.EastWest : Team.NorthSouth;

    public IReadOnlyDictionary<PlayerPosition, IReadOnlyList<Card>> Hands =>
        _hands.ToDictionary(item => item.Key, item => (IReadOnlyList<Card>)item.Value.ToArray());

    public ImmutableArray<Card> Kitty { get; private set; }

    public Trick CurrentTrick { get; private set; }

    public RoundPhase Phase { get; private set; } = RoundPhase.Playing;

    public int OpponentTrickPoints { get; private set; }

    public RoundResult? Result { get; private set; }

    public IReadOnlyList<Card> PlayedCards => _playedCards;

    public static GameRound CreateNew(int seed, Rank trumpRank = Rank.Two, PlayerPosition starter = PlayerPosition.South)
    {
        var deck = DeckFactory.Shuffle(seed);
        var hands = Enum.GetValues<PlayerPosition>().ToDictionary(player => player, _ => new List<Card>(25));
        var cursor = 0;

        for (var round = 0; round < 25; round++)
        {
            foreach (var player in DealOrder(starter))
            {
                hands[player].Add(deck[cursor++]);
            }
        }

        var kitty = deck.Skip(cursor).Take(8).ToImmutableArray();
        var trump = SeniorAiPlayer.ChooseTrump(hands[starter], trumpRank);

        hands[starter].AddRange(kitty);
        var discard = SeniorAiPlayer.ChooseDiscard(hands[starter], trump, 8);
        RemoveCards(hands[starter], discard);

        var roundState = new GameRound(trump, starter, hands, discard);
        foreach (var player in Enum.GetValues<PlayerPosition>())
        {
            roundState.SortHand(player);
        }

        return roundState;
    }

    public IReadOnlyList<Card> Hand(PlayerPosition player) => _hands[player];

    public ValidationResult Play(PlayerPosition player, IReadOnlyList<Card> cards)
    {
        if (Phase == RoundPhase.Complete)
        {
            return ValidationResult.Invalid("The hand is complete.");
        }

        var hand = _hands[player];
        var validation = CurrentTrick.AddPlay(player, hand, cards);
        if (!validation.IsValid)
        {
            return validation;
        }

        RemoveCards(hand, cards);
        _playedCards.AddRange(cards);

        if (CurrentTrick.IsComplete)
        {
            CompleteTrick();
        }

        return ValidationResult.Valid;
    }

    public void SortHand(PlayerPosition player)
    {
        _hands[player].Sort((left, right) => CardRules.SortKey(left, Trump).CompareTo(CardRules.SortKey(right, Trump)));
    }

    private void CompleteTrick()
    {
        var winner = CurrentTrick.Winner();
        var trickCards = CurrentTrick.Cards();

        if (winner.Team() == Opponents)
        {
            OpponentTrickPoints += CardRules.CountPoints(trickCards);
        }

        var allHandsEmpty = _hands.Values.All(hand => hand.Count == 0);
        if (allHandsEmpty)
        {
            Phase = RoundPhase.Complete;
            Result = ScoreRound(winner, CurrentTrick.CardCount);
            return;
        }

        CurrentTrick = new Trick(winner, Trump);
    }

    private RoundResult ScoreRound(PlayerPosition lastTrickWinner, int lastTrickCardCount)
    {
        return RoundScorer.Score(
            OpponentTrickPoints,
            CardRules.CountPoints(Kitty),
            lastTrickCardCount,
            lastTrickWinner.Team() == Opponents,
            Declarers);
    }

    private static IEnumerable<PlayerPosition> DealOrder(PlayerPosition starter)
    {
        var player = starter;
        for (var i = 0; i < 4; i++)
        {
            yield return player;
            player = player.NextAntiClockwise();
        }
    }

    private static void RemoveCards(List<Card> hand, IEnumerable<Card> cards)
    {
        foreach (var card in cards)
        {
            var removed = hand.Remove(card);
            if (!removed)
            {
                throw new InvalidOperationException($"Card {card} was not found in hand.");
            }
        }
    }
}

public static class RoundScorer
{
    public static RoundResult Score(
        int opponentTrickPoints,
        int kittyPoints,
        int lastTrickCardCount,
        bool opponentsWonLastTrick,
        Team declarers)
    {
        var opponents = declarers == Team.NorthSouth ? Team.EastWest : Team.NorthSouth;
        var multiplier = opponentsWonLastTrick
            ? Math.Max(2, lastTrickCardCount * 2)
            : 0;

        var total = opponentTrickPoints + kittyPoints * multiplier;
        var opponentsWon = total >= 80;
        var levelDelta = total switch
        {
            < 5 => 3,
            < 40 => 2,
            < 80 => 1,
            < 120 => 0,
            < 160 => 1,
            < 200 => 2,
            < 240 => 3,
            _ => 3 + ((total - 200) / 40)
        };

        return new RoundResult(
            total,
            kittyPoints,
            multiplier,
            opponentsWon,
            levelDelta,
            opponentsWon ? opponents : declarers);
    }
}
