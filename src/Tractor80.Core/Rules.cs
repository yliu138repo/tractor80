using System.Collections.Immutable;

namespace Tractor80.Core;

public enum CardGroup
{
    Clubs,
    Diamonds,
    Hearts,
    Spades,
    Trump
}

public enum LeadKind
{
    SingleCard,
    Pair,
    Tractor
}

public readonly record struct TrumpConfig(Rank Rank, Suit? Suit)
{
    public bool HasTrumpSuit => Suit.HasValue;

    public string DisplayName => HasTrumpSuit
        ? $"{Card.RankText(Rank)} / {Card.SuitText(Suit!.Value)}"
        : $"{Card.RankText(Rank)} / No Suit";
}

public sealed record LeadPattern(LeadKind Kind, CardGroup Group, int CardCount, int PairCount, int HighestSequenceValue);

public sealed record ValidationResult(bool IsValid, string Message)
{
    public static readonly ValidationResult Valid = new(true, "Valid play.");

    public static ValidationResult Invalid(string message) => new(false, message);
}

public static class CardRules
{
    private static readonly ImmutableArray<Rank> OrderedRanks =
    [
        Rank.Two,
        Rank.Three,
        Rank.Four,
        Rank.Five,
        Rank.Six,
        Rank.Seven,
        Rank.Eight,
        Rank.Nine,
        Rank.Ten,
        Rank.Jack,
        Rank.Queen,
        Rank.King,
        Rank.Ace
    ];

    public static bool IsTrump(Card card, TrumpConfig trump)
    {
        if (card.IsJoker)
        {
            return true;
        }

        if (!trump.HasTrumpSuit)
        {
            return false;
        }

        return card.Rank == trump.Rank || card.Suit == trump.Suit;
    }

    public static CardGroup EffectiveGroup(Card card, TrumpConfig trump)
    {
        if (IsTrump(card, trump))
        {
            return CardGroup.Trump;
        }

        return card.Suit switch
        {
            Suit.Clubs => CardGroup.Clubs,
            Suit.Diamonds => CardGroup.Diamonds,
            Suit.Hearts => CardGroup.Hearts,
            Suit.Spades => CardGroup.Spades,
            _ => throw new InvalidOperationException("Non-joker card must have a suit.")
        };
    }

    public static int Power(Card card, TrumpConfig trump)
    {
        if (card.IsJoker)
        {
            return card.Joker == JokerColor.Red ? 200 : 190;
        }

        if (trump.HasTrumpSuit && card.Rank == trump.Rank && card.Suit == trump.Suit)
        {
            return 180;
        }

        if (trump.HasTrumpSuit && card.Rank == trump.Rank)
        {
            return 170;
        }

        var rankPower = RankPower(card.Rank!.Value);

        if (trump.HasTrumpSuit && card.Suit == trump.Suit)
        {
            return 100 + rankPower;
        }

        return rankPower;
    }

    public static int SortKey(Card card, TrumpConfig trump)
    {
        var group = EffectiveGroup(card, trump);
        return ((int)group * 1000) + Power(card, trump) * 2 + card.Id % 2;
    }

    public static int RankPower(Rank rank) => rank switch
    {
        Rank.Two => 2,
        Rank.Three => 3,
        Rank.Four => 4,
        Rank.Five => 5,
        Rank.Six => 6,
        Rank.Seven => 7,
        Rank.Eight => 8,
        Rank.Nine => 9,
        Rank.Ten => 10,
        Rank.Jack => 11,
        Rank.Queen => 12,
        Rank.King => 13,
        Rank.Ace => 14,
        _ => 0
    };

    public static bool AreIdentical(Card left, Card right) => left.FaceKey == right.FaceKey;

    public static int SequenceValue(Card card, TrumpConfig trump)
    {
        var naturalCount = NaturalSequenceRanks(trump).Count;

        if (card.IsJoker)
        {
            return card.Joker == JokerColor.Red
                ? naturalCount + (trump.HasTrumpSuit ? 3 : 1)
                : naturalCount + (trump.HasTrumpSuit ? 2 : 0);
        }

        if (trump.HasTrumpSuit && card.Rank == trump.Rank && card.Suit == trump.Suit)
        {
            return naturalCount + 1;
        }

        if (trump.HasTrumpSuit && card.Rank == trump.Rank)
        {
            return naturalCount;
        }

        return NaturalSequenceValue(card.Rank!.Value, trump);
    }

    public static IReadOnlyList<Rank> NaturalSequenceRanks(TrumpConfig trump)
    {
        if (!trump.HasTrumpSuit)
        {
            return OrderedRanks;
        }

        return OrderedRanks.Where(rank => rank != trump.Rank).ToArray();
    }

    private static int NaturalSequenceValue(Rank rank, TrumpConfig trump)
    {
        var ranks = NaturalSequenceRanks(trump);
        for (var i = 0; i < ranks.Count; i++)
        {
            if (ranks[i] == rank)
            {
                return i;
            }
        }

        return -1;
    }

    public static int CompareCards(Card left, Card right, TrumpConfig trump)
    {
        return Power(left, trump).CompareTo(Power(right, trump));
    }

    public static int CountPoints(IEnumerable<Card> cards) => cards.Sum(card => card.PointValue);
}

public sealed class LeadAnalyzer
{
    private readonly TrumpConfig _trump;

    public LeadAnalyzer(TrumpConfig trump)
    {
        _trump = trump;
    }

    public LeadPattern? Analyze(IReadOnlyList<Card> cards)
    {
        if (cards.Count == 0)
        {
            return null;
        }

        var group = CardRules.EffectiveGroup(cards[0], _trump);
        if (cards.Any(card => CardRules.EffectiveGroup(card, _trump) != group))
        {
            return null;
        }

        if (cards.Count == 1)
        {
            return new LeadPattern(LeadKind.SingleCard, group, 1, 0, CardRules.SequenceValue(cards[0], _trump));
        }

        if (cards.Count == 2 && CardRules.AreIdentical(cards[0], cards[1]))
        {
            return new LeadPattern(LeadKind.Pair, group, 2, 1, CardRules.SequenceValue(cards[0], _trump));
        }

        if (cards.Count >= 4 && cards.Count % 2 == 0)
        {
            var pairs = cards
                .GroupBy(card => card.FaceKey)
                .Select(grouping => grouping.ToArray())
                .ToArray();

            if (pairs.Any(pair => pair.Length != 2))
            {
                return null;
            }

            var sequenceValues = pairs
                .Select(pair => CardRules.SequenceValue(pair[0], _trump))
                .Order()
                .ToArray();

            if (sequenceValues.Zip(sequenceValues.Skip(1), (current, next) => next - current).All(diff => diff == 1))
            {
                return new LeadPattern(
                    LeadKind.Tractor,
                    group,
                    cards.Count,
                    cards.Count / 2,
                    sequenceValues[^1]);
            }
        }

        return null;
    }
}

public sealed class PlayValidator
{
    private readonly TrumpConfig _trump;
    private readonly LeadAnalyzer _leadAnalyzer;

    public PlayValidator(TrumpConfig trump)
    {
        _trump = trump;
        _leadAnalyzer = new LeadAnalyzer(trump);
    }

    public ValidationResult ValidateLead(IReadOnlyList<Card> hand, IReadOnlyList<Card> selected)
    {
        var ownership = ValidateOwnership(hand, selected);
        if (!ownership.IsValid)
        {
            return ownership;
        }

        return _leadAnalyzer.Analyze(selected) is null
            ? ValidationResult.Invalid("Lead a single, an identical pair, or a consecutive tractor.")
            : ValidationResult.Valid;
    }

    public ValidationResult ValidateFollow(IReadOnlyList<Card> hand, LeadPattern lead, IReadOnlyList<Card> selected)
    {
        var ownership = ValidateOwnership(hand, selected);
        if (!ownership.IsValid)
        {
            return ownership;
        }

        if (selected.Count != lead.CardCount)
        {
            return ValidationResult.Invalid($"Play exactly {lead.CardCount} card(s).");
        }

        return lead.Kind switch
        {
            LeadKind.SingleCard => ValidateSingleFollow(hand, lead, selected),
            LeadKind.Pair => ValidatePairFollow(hand, lead, selected),
            LeadKind.Tractor => ValidateTractorFollow(hand, lead, selected),
            _ => ValidationResult.Invalid("Unsupported lead.")
        };
    }

    public LeadPattern? AnalyzeLead(IReadOnlyList<Card> cards) => _leadAnalyzer.Analyze(cards);

    private ValidationResult ValidateSingleFollow(IReadOnlyList<Card> hand, LeadPattern lead, IReadOnlyList<Card> selected)
    {
        var required = hand.Where(card => CardRules.EffectiveGroup(card, _trump) == lead.Group).ToArray();
        if (required.Length > 0 && CardRules.EffectiveGroup(selected[0], _trump) != lead.Group)
        {
            return ValidationResult.Invalid($"Follow {lead.Group} when you can.");
        }

        return ValidationResult.Valid;
    }

    private ValidationResult ValidatePairFollow(IReadOnlyList<Card> hand, LeadPattern lead, IReadOnlyList<Card> selected)
    {
        var groupCards = CardsInGroup(hand, lead.Group);
        var selectedGroupCards = CardsInGroup(selected, lead.Group);
        var handHasPair = HasIdenticalPair(groupCards);
        var selectedIsPairInGroup = selectedGroupCards.Count == 2 && CardRules.AreIdentical(selectedGroupCards[0], selectedGroupCards[1]);

        if (handHasPair && !selectedIsPairInGroup)
        {
            return ValidationResult.Invalid($"Follow with an identical {lead.Group} pair.");
        }

        var requiredCount = Math.Min(2, groupCards.Count);
        if (selectedGroupCards.Count < requiredCount)
        {
            return ValidationResult.Invalid($"Play {requiredCount} {lead.Group} card(s).");
        }

        return ValidationResult.Valid;
    }

    private ValidationResult ValidateTractorFollow(IReadOnlyList<Card> hand, LeadPattern lead, IReadOnlyList<Card> selected)
    {
        var groupCards = CardsInGroup(hand, lead.Group);
        var selectedGroupCards = CardsInGroup(selected, lead.Group);
        var requiredGroupCount = Math.Min(lead.CardCount, groupCards.Count);

        if (selectedGroupCards.Count < requiredGroupCount)
        {
            return ValidationResult.Invalid($"Play all available {lead.Group} cards before discarding off-suit.");
        }

        var requiredPairCount = Math.Min(lead.PairCount, CountIdenticalPairs(groupCards));
        var selectedPairCount = CountIdenticalPairs(selectedGroupCards);

        if (selectedPairCount < requiredPairCount)
        {
            return ValidationResult.Invalid($"Preserve {requiredPairCount} pair(s) while following the tractor.");
        }

        return ValidationResult.Valid;
    }

    private static ValidationResult ValidateOwnership(IReadOnlyList<Card> hand, IReadOnlyList<Card> selected)
    {
        if (selected.Count == 0)
        {
            return ValidationResult.Invalid("Select at least one card.");
        }

        var handIds = hand.Select(card => card.Id).ToHashSet();
        return selected.All(card => handIds.Contains(card.Id))
            ? ValidationResult.Valid
            : ValidationResult.Invalid("Selected cards are not in the player's hand.");
    }

    private List<Card> CardsInGroup(IEnumerable<Card> cards, CardGroup group)
    {
        return cards.Where(card => CardRules.EffectiveGroup(card, _trump) == group).ToList();
    }

    private static bool HasIdenticalPair(IEnumerable<Card> cards)
    {
        return CountIdenticalPairs(cards) > 0;
    }

    public static int CountIdenticalPairs(IEnumerable<Card> cards)
    {
        return cards
            .GroupBy(card => card.FaceKey)
            .Sum(group => group.Count() / 2);
    }
}

public sealed record PlayedCards(PlayerPosition Player, ImmutableArray<Card> Cards);

public sealed class Trick
{
    private readonly List<PlayedCards> _plays = [];

    public Trick(PlayerPosition leader, TrumpConfig trump)
    {
        Leader = leader;
        Trump = trump;
    }

    public PlayerPosition Leader { get; }

    public TrumpConfig Trump { get; }

    public IReadOnlyList<PlayedCards> Plays => _plays;

    public LeadPattern? LeadPattern { get; private set; }

    public bool IsComplete => _plays.Count == 4;

    public int CardCount => LeadPattern?.CardCount ?? 0;

    public ValidationResult AddPlay(PlayerPosition player, IReadOnlyList<Card> hand, IReadOnlyList<Card> cards)
    {
        if (IsComplete)
        {
            return ValidationResult.Invalid("The trick is already complete.");
        }

        if (player != ExpectedPlayer)
        {
            return ValidationResult.Invalid($"It is {ExpectedPlayer}'s turn.");
        }

        var validator = new PlayValidator(Trump);
        ValidationResult validation;

        if (_plays.Count == 0)
        {
            validation = validator.ValidateLead(hand, cards);
            if (validation.IsValid)
            {
                LeadPattern = validator.AnalyzeLead(cards);
            }
        }
        else
        {
            validation = validator.ValidateFollow(hand, LeadPattern!, cards);
        }

        if (!validation.IsValid)
        {
            return validation;
        }

        _plays.Add(new PlayedCards(player, cards.ToImmutableArray()));
        return ValidationResult.Valid;
    }

    public PlayerPosition ExpectedPlayer => _plays.Count == 0
        ? Leader
        : _plays[^1].Player.NextAntiClockwise();

    public PlayerPosition Winner()
    {
        if (!IsComplete || LeadPattern is null)
        {
            throw new InvalidOperationException("Cannot resolve an incomplete trick.");
        }

        return LeadPattern.Kind switch
        {
            LeadKind.SingleCard => WinnerForSingles(),
            LeadKind.Pair => WinnerForPairs(),
            LeadKind.Tractor => WinnerForTractors(),
            _ => Leader
        };
    }

    public ImmutableArray<Card> Cards() => _plays.SelectMany(play => play.Cards).ToImmutableArray();

    private PlayerPosition WinnerForSingles()
    {
        var best = _plays[0];
        var bestCard = best.Cards[0];

        foreach (var play in _plays.Skip(1))
        {
            var card = play.Cards[0];
            var cardGroup = CardRules.EffectiveGroup(card, Trump);
            var bestGroup = CardRules.EffectiveGroup(bestCard, Trump);

            if (cardGroup == CardGroup.Trump && bestGroup != CardGroup.Trump)
            {
                best = play;
                bestCard = card;
                continue;
            }

            if (cardGroup == bestGroup && CardRules.CompareCards(card, bestCard, Trump) > 0)
            {
                best = play;
                bestCard = card;
            }
        }

        return best.Player;
    }

    private PlayerPosition WinnerForPairs()
    {
        var candidates = _plays
            .Select(play => new
            {
                Play = play,
                Pair = TryGetIdenticalPair(play.Cards),
                Group = TryGetIdenticalPair(play.Cards) is { } pair
                    ? CardRules.EffectiveGroup(pair[0], Trump)
                    : (CardGroup?)null
            })
            .Where(candidate => candidate.Pair is not null)
            .Where(candidate => candidate.Group == LeadPattern!.Group || candidate.Group == CardGroup.Trump)
            .ToArray();

        return candidates
            .OrderByDescending(candidate => candidate.Group == CardGroup.Trump ? 1 : 0)
            .ThenByDescending(candidate => CardRules.Power(candidate.Pair![0], Trump))
            .Select(candidate => candidate.Play.Player)
            .First();
    }

    private PlayerPosition WinnerForTractors()
    {
        var analyzer = new LeadAnalyzer(Trump);
        var candidates = _plays
            .Select(play => new
            {
                Play = play,
                Pattern = analyzer.Analyze(play.Cards)
            })
            .Where(candidate => candidate.Pattern is not null)
            .Where(candidate => candidate.Pattern!.Kind == LeadKind.Tractor)
            .Where(candidate => candidate.Pattern!.PairCount == LeadPattern!.PairCount)
            .Where(candidate => candidate.Pattern!.Group == LeadPattern!.Group || candidate.Pattern!.Group == CardGroup.Trump)
            .ToArray();

        return candidates
            .OrderByDescending(candidate => candidate.Pattern!.Group == CardGroup.Trump ? 1 : 0)
            .ThenByDescending(candidate => candidate.Pattern!.HighestSequenceValue)
            .Select(candidate => candidate.Play.Player)
            .First();
    }

    private static Card[]? TryGetIdenticalPair(IReadOnlyList<Card> cards)
    {
        return cards.Count == 2 && CardRules.AreIdentical(cards[0], cards[1])
            ? [cards[0], cards[1]]
            : null;
    }
}
