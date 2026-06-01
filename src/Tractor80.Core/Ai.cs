using System.Collections.Immutable;

namespace Tractor80.Core;

public enum AiPersona
{
    TrumpController,
    PointHunter,
    PartnerProtector
}

public sealed class SeniorAiPlayer
{
    private readonly AiPersona _persona;

    public SeniorAiPlayer(AiPersona persona)
    {
        _persona = persona;
    }

    public ImmutableArray<Card> ChoosePlay(
        PlayerPosition player,
        IReadOnlyList<Card> hand,
        Trick trick,
        int opponentPoints,
        IReadOnlyCollection<Card> knownPlayedCards)
    {
        if (trick.Plays.Count == 0)
        {
            return ChooseLead(hand, trick.Trump, opponentPoints, knownPlayedCards);
        }

        var legalPlays = LegalPlayGenerator.GenerateFollows(hand, trick.LeadPattern!, trick.Trump).ToArray();
        if (legalPlays.Length == 0)
        {
            throw new InvalidOperationException("AI has no legal follow play.");
        }

        var currentWinner = ResolvePartialWinner(trick);
        var partnerWinning = currentWinner.IsPartnerOf(player);
        var trickPoints = CardRules.CountPoints(trick.Plays.SelectMany(play => play.Cards));
        var targetPoints = trickPoints + CardRules.CountPoints(legalPlays.SelectMany(play => play));

        var winningPlays = legalPlays
            .Where(play => WouldWin(player, play, trick))
            .OrderBy(play => TacticalCost(play, trick.Trump))
            .ToArray();

        if (!partnerWinning && winningPlays.Length > 0 && (trickPoints >= 10 || opponentPoints >= 60 || _persona == AiPersona.PointHunter))
        {
            return winningPlays[0].ToImmutableArray();
        }

        if (partnerWinning)
        {
            var dump = legalPlays
                .OrderByDescending(play => CardRules.CountPoints(play))
                .ThenBy(play => TacticalCost(play, trick.Trump))
                .First();

            return dump.ToImmutableArray();
        }

        var conservative = legalPlays
            .OrderBy(play => CardRules.CountPoints(play) > 0 && targetPoints >= 10 ? 1 : 0)
            .ThenBy(play => TacticalCost(play, trick.Trump))
            .First();

        return conservative.ToImmutableArray();
    }

    public static ImmutableArray<Card> ChooseDiscard(IReadOnlyList<Card> hand, TrumpConfig trump, int count)
    {
        return hand
            .OrderBy(card => card.PointValue)
            .ThenBy(card => CardRules.IsTrump(card, trump) ? 1 : 0)
            .ThenBy(card => CardRules.Power(card, trump))
            .Take(count)
            .ToImmutableArray();
    }

    public static TrumpConfig ChooseTrump(IReadOnlyList<Card> hand, Rank rank)
    {
        var bestSuit = Enum.GetValues<Suit>()
            .Select(suit => new
            {
                Suit = suit,
                Strength = hand.Sum(card => TrumpSuitStrength(card, rank, suit))
            })
            .OrderByDescending(item => item.Strength)
            .ThenBy(item => item.Suit)
            .First();

        return new TrumpConfig(rank, bestSuit.Suit);
    }

    private ImmutableArray<Card> ChooseLead(
        IReadOnlyList<Card> hand,
        TrumpConfig trump,
        int opponentPoints,
        IReadOnlyCollection<Card> knownPlayedCards)
    {
        var leadOptions = LegalPlayGenerator.GenerateLeads(hand, trump).ToArray();

        var tractors = leadOptions
            .Where(play => play.Length >= 4)
            .OrderByDescending(play => LeadStrength(play, trump))
            .ToArray();

        if (tractors.Length > 0 && (_persona != AiPersona.PartnerProtector || opponentPoints >= 40))
        {
            return tractors[0].ToImmutableArray();
        }

        var pairs = leadOptions
            .Where(play => play.Length == 2)
            .OrderByDescending(play => LeadStrength(play, trump))
            .ToArray();

        if (pairs.Length > 0 && _persona is AiPersona.PointHunter or AiPersona.TrumpController)
        {
            return pairs[0].ToImmutableArray();
        }

        var singles = leadOptions
            .Where(play => play.Length == 1)
            .OrderBy(play => CardRules.IsTrump(play[0], trump) && _persona == AiPersona.PartnerProtector ? 1 : 0)
            .ThenBy(play => CardRules.CountPoints(play))
            .ThenBy(play => CardRules.Power(play[0], trump))
            .ToArray();

        if (_persona == AiPersona.TrumpController && opponentPoints >= 60)
        {
            var trumpLead = singles
                .Where(play => CardRules.IsTrump(play[0], trump))
                .OrderByDescending(play => CardRules.Power(play[0], trump))
                .FirstOrDefault();

            if (trumpLead is not null)
            {
                return trumpLead.ToImmutableArray();
            }
        }

        return singles.Length > 0 ? singles[0].ToImmutableArray() : [hand[0]];
    }

    private static int TrumpSuitStrength(Card card, Rank rank, Suit suit)
    {
        if (card.IsJoker)
        {
            return card.Joker == JokerColor.Red ? 7 : 5;
        }

        var strength = 0;
        if (card.Rank == rank)
        {
            strength += card.Suit == suit ? 8 : 4;
        }

        if (card.Suit == suit)
        {
            strength += CardRules.RankPower(card.Rank!.Value) >= 11 ? 2 : 1;
        }

        return strength;
    }

    private static int LeadStrength(IReadOnlyList<Card> play, TrumpConfig trump)
    {
        return play.Sum(card => CardRules.Power(card, trump)) + CardRules.CountPoints(play) * 3;
    }

    private static int TacticalCost(IReadOnlyList<Card> play, TrumpConfig trump)
    {
        return play.Sum(card => CardRules.Power(card, trump) + card.PointValue * 6 + (CardRules.IsTrump(card, trump) ? 20 : 0));
    }

    private static bool WouldWin(PlayerPosition player, IReadOnlyList<Card> candidate, Trick trick)
    {
        var probe = new Trick(trick.Leader, trick.Trump);
        var fakeHands = trick.Plays.ToDictionary(play => play.Player, play => play.Cards.AsEnumerable().ToArray());

        foreach (var play in trick.Plays)
        {
            var add = probe.AddPlay(play.Player, fakeHands[play.Player], play.Cards);
            if (!add.IsValid)
            {
                return false;
            }
        }

        var candidateAdd = probe.AddPlay(player, candidate, candidate);
        if (!candidateAdd.IsValid)
        {
            return false;
        }

        if (!probe.IsComplete)
        {
            return ResolvePartialWinner(probe) == player;
        }

        return probe.Winner() == player;
    }

    private static PlayerPosition ResolvePartialWinner(Trick trick)
    {
        if (trick.Plays.Count == 1)
        {
            return trick.Plays[0].Player;
        }

        var fillerPlayers = Enum.GetValues<PlayerPosition>()
            .Where(player => trick.Plays.All(play => play.Player != player))
            .ToArray();

        var probe = new Trick(trick.Leader, trick.Trump);

        foreach (var play in trick.Plays)
        {
            var add = probe.AddPlay(play.Player, play.Cards, play.Cards);
            if (!add.IsValid)
            {
                return trick.Plays[0].Player;
            }
        }

        var filler = LowestFillerCards(trick);
        foreach (var player in fillerPlayers)
        {
            var add = probe.AddPlay(player, filler, filler);
            if (!add.IsValid)
            {
                break;
            }
        }

        return probe.IsComplete ? probe.Winner() : trick.Plays[0].Player;
    }

    private static Card[] LowestFillerCards(Trick trick)
    {
        var group = trick.LeadPattern!.Group == CardGroup.Trump ? Suit.Clubs : GroupToSuit(trick.LeadPattern.Group);
        var cards = new List<Card>();

        for (var i = 0; i < trick.LeadPattern.CardCount; i++)
        {
            cards.Add(new Card(-100 - i, group, Rank.Two, null));
        }

        if (trick.LeadPattern.Kind == LeadKind.Pair && cards.Count == 2)
        {
            cards[1] = cards[0] with { Id = cards[0].Id - 1 };
        }

        return cards.ToArray();
    }

    private static Suit GroupToSuit(CardGroup group)
    {
        return group switch
        {
            CardGroup.Clubs => Suit.Clubs,
            CardGroup.Diamonds => Suit.Diamonds,
            CardGroup.Hearts => Suit.Hearts,
            CardGroup.Spades => Suit.Spades,
            _ => Suit.Clubs
        };
    }
}

public static class LegalPlayGenerator
{
    public static IEnumerable<Card[]> GenerateLeads(IReadOnlyList<Card> hand, TrumpConfig trump)
    {
        foreach (var card in hand)
        {
            yield return [card];
        }

        foreach (var pair in IdenticalPairs(hand))
        {
            yield return pair;
        }

        foreach (var tractor in Tractors(hand, trump))
        {
            yield return tractor;
        }
    }

    public static IEnumerable<Card[]> GenerateFollows(IReadOnlyList<Card> hand, LeadPattern lead, TrumpConfig trump)
    {
        var validator = new PlayValidator(trump);
        var combinations = lead.CardCount <= 4
            ? Combinations(hand, lead.CardCount)
            : HeuristicFollows(hand, lead, trump);

        foreach (var combination in combinations)
        {
            if (validator.ValidateFollow(hand, lead, combination).IsValid)
            {
                yield return combination;
            }
        }
    }

    private static IEnumerable<Card[]> HeuristicFollows(IReadOnlyList<Card> hand, LeadPattern lead, TrumpConfig trump)
    {
        var groupCards = hand
            .Where(card => CardRules.EffectiveGroup(card, trump) == lead.Group)
            .OrderBy(card => CardRules.Power(card, trump))
            .ThenBy(card => card.PointValue)
            .ToArray();

        var offCards = hand
            .Where(card => CardRules.EffectiveGroup(card, trump) != lead.Group)
            .OrderBy(card => CardRules.Power(card, trump))
            .ThenBy(card => card.PointValue)
            .ToArray();

        if (groupCards.Length < lead.CardCount)
        {
            foreach (var fill in CandidateFills(offCards, lead.CardCount - groupCards.Length, trump))
            {
                yield return groupCards.Concat(fill).ToArray();
            }

            yield break;
        }

        foreach (var tractor in Tractors(groupCards, trump).Where(play => play.Length == lead.CardCount))
        {
            yield return tractor;
        }

        var requiredPairs = Math.Min(lead.PairCount, PlayValidator.CountIdenticalPairs(groupCards));
        var pairCards = IdenticalPairs(groupCards)
            .OrderBy(pair => CardRules.Power(pair[0], trump))
            .Take(requiredPairs)
            .SelectMany(pair => pair)
            .ToArray();

        var fillers = groupCards
            .Where(card => pairCards.All(pairCard => pairCard.Id != card.Id))
            .Take(lead.CardCount - pairCards.Length)
            .ToArray();

        yield return pairCards.Concat(fillers).ToArray();

        var highCards = groupCards
            .OrderByDescending(card => CardRules.Power(card, trump))
            .Take(lead.CardCount)
            .ToArray();

        yield return highCards;
    }

    private static IEnumerable<Card[]> CandidateFills(IReadOnlyList<Card> cards, int count, TrumpConfig trump)
    {
        if (count <= 0)
        {
            yield return [];
            yield break;
        }

        yield return cards.Take(count).ToArray();
        yield return cards.OrderByDescending(card => card.PointValue).ThenBy(card => CardRules.Power(card, trump)).Take(count).ToArray();
        yield return cards.OrderByDescending(card => CardRules.Power(card, trump)).Take(count).ToArray();
    }

    public static IEnumerable<Card[]> IdenticalPairs(IReadOnlyList<Card> cards)
    {
        return cards
            .GroupBy(card => card.FaceKey)
            .Where(group => group.Count() >= 2)
            .Select(group => group.Take(2).ToArray());
    }

    public static IEnumerable<Card[]> Tractors(IReadOnlyList<Card> hand, TrumpConfig trump)
    {
        var analyzer = new LeadAnalyzer(trump);
        var pairs = IdenticalPairs(hand)
            .GroupBy(pair => CardRules.EffectiveGroup(pair[0], trump))
            .ToArray();

        foreach (var group in pairs)
        {
            var ordered = group
                .OrderBy(pair => CardRules.SequenceValue(pair[0], trump))
                .ToArray();

            for (var i = 0; i < ordered.Length - 1; i++)
            {
                var run = new List<Card[]>
                {
                    ordered[i]
                };

                for (var j = i + 1; j < ordered.Length; j++)
                {
                    var previous = CardRules.SequenceValue(run[^1][0], trump);
                    var current = CardRules.SequenceValue(ordered[j][0], trump);

                    if (current == previous + 1)
                    {
                        run.Add(ordered[j]);
                        if (run.Count >= 2)
                        {
                            var cards = run.SelectMany(pair => pair).ToArray();
                            if (analyzer.Analyze(cards)?.Kind == LeadKind.Tractor)
                            {
                                yield return cards;
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
    }

    private static IEnumerable<Card[]> Combinations(IReadOnlyList<Card> cards, int choose)
    {
        if (choose == 1)
        {
            foreach (var card in cards)
            {
                yield return [card];
            }

            yield break;
        }

        var buffer = new Card[choose];
        foreach (var combination in CombinationsCore(cards, choose, 0, 0, buffer))
        {
            yield return combination;
        }
    }

    private static IEnumerable<Card[]> CombinationsCore(
        IReadOnlyList<Card> cards,
        int choose,
        int start,
        int depth,
        Card[] buffer)
    {
        if (depth == choose)
        {
            yield return buffer.ToArray();
            yield break;
        }

        for (var i = start; i <= cards.Count - (choose - depth); i++)
        {
            buffer[depth] = cards[i];
            foreach (var combination in CombinationsCore(cards, choose, i + 1, depth + 1, buffer))
            {
                yield return combination;
            }
        }
    }
}
