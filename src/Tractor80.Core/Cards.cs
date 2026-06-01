using System.Collections.Immutable;

namespace Tractor80.Core;

public enum Suit
{
    Clubs = 0,
    Diamonds = 1,
    Hearts = 2,
    Spades = 3
}

public enum Rank
{
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Ace = 14
}

public enum JokerColor
{
    Black = 0,
    Red = 1
}

public readonly record struct Card(int Id, Suit? Suit, Rank? Rank, JokerColor? Joker)
{
    public bool IsJoker => Joker.HasValue;

    public int PointValue => Rank switch
    {
        global::Tractor80.Core.Rank.Five => 5,
        global::Tractor80.Core.Rank.Ten or global::Tractor80.Core.Rank.King => 10,
        _ => 0
    };

    public string FaceKey => IsJoker
        ? $"Joker:{Joker}"
        : $"{Suit}:{Rank}";

    public string ShortName => IsJoker
        ? Joker == JokerColor.Red ? "RJ" : "BJ"
        : $"{RankText(Rank!.Value)}{SuitText(Suit!.Value)}";

    public override string ToString() => ShortName;

    public static string RankText(Rank rank) => rank switch
    {
        global::Tractor80.Core.Rank.Two => "2",
        global::Tractor80.Core.Rank.Three => "3",
        global::Tractor80.Core.Rank.Four => "4",
        global::Tractor80.Core.Rank.Five => "5",
        global::Tractor80.Core.Rank.Six => "6",
        global::Tractor80.Core.Rank.Seven => "7",
        global::Tractor80.Core.Rank.Eight => "8",
        global::Tractor80.Core.Rank.Nine => "9",
        global::Tractor80.Core.Rank.Ten => "10",
        global::Tractor80.Core.Rank.Jack => "J",
        global::Tractor80.Core.Rank.Queen => "Q",
        global::Tractor80.Core.Rank.King => "K",
        global::Tractor80.Core.Rank.Ace => "A",
        _ => "?"
    };

    public static string SuitText(Suit suit) => suit switch
    {
        global::Tractor80.Core.Suit.Clubs => "C",
        global::Tractor80.Core.Suit.Diamonds => "D",
        global::Tractor80.Core.Suit.Hearts => "H",
        global::Tractor80.Core.Suit.Spades => "S",
        _ => "?"
    };
}

public sealed class DeckFactory
{
    public const int StandardDeckCount = 2;
    public const int StandardCardCount = 108;

    public static ImmutableArray<Card> CreateDoubleDeck()
    {
        var cards = ImmutableArray.CreateBuilder<Card>(StandardCardCount);
        var id = 0;

        for (var deck = 0; deck < StandardDeckCount; deck++)
        {
            foreach (var suit in Enum.GetValues<Suit>())
            {
                foreach (var rank in Enum.GetValues<Rank>())
                {
                    cards.Add(new Card(id++, suit, rank, null));
                }
            }

            cards.Add(new Card(id++, null, null, JokerColor.Black));
            cards.Add(new Card(id++, null, null, JokerColor.Red));
        }

        return cards.ToImmutable();
    }

    public static ImmutableArray<Card> Shuffle(int seed)
    {
        var random = new Random(seed);
        var cards = CreateDoubleDeck().ToArray();

        for (var i = cards.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        return cards.ToImmutableArray();
    }
}
