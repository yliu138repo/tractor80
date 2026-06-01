using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Tractor80.Core;

namespace Tractor80.Windows;

public partial class MainWindow : Window
{
    private const int CompletedTrickHoldMilliseconds = 6200;

    private enum UiLanguage
    {
        Chinese,
        English
    }

    private readonly Dictionary<PlayerPosition, SeniorAiPlayer> _ai = new()
    {
        [PlayerPosition.North] = new SeniorAiPlayer(AiPersona.PartnerProtector),
        [PlayerPosition.East] = new SeniorAiPlayer(AiPersona.PointHunter),
        [PlayerPosition.West] = new SeniorAiPlayer(AiPersona.TrumpController)
    };

    private readonly Dictionary<string, (string Chinese, string English)> _text = new()
    {
        ["AppTitle"] = ("拖拉机 80 / 八十分", "Tractor 80 / Eighty Points"),
        ["NewHand"] = ("新局", "New"),
        ["RushHand"] = ("抢分局", "Rush Hand"),
        ["Hint"] = ("提示", "Hint"),
        ["Rush"] = ("抢分", "Rush"),
        ["Defend"] = ("守分", "Defend"),
        ["Play"] = ("出牌", "Play"),
        ["TableLog"] = ("牌桌记录", "Table Log"),
        ["ScorePanel"] = ("抢分进度", "Scoring Race"),
        ["CurrentTrick"] = ("当前墩分", "Trick Points"),
        ["YourHand"] = ("你的手牌", "Your Hand"),
        ["Selected"] = ("已选 {0} 张", "{0} selected"),
        ["Cards"] = ("{0} 张牌", "{0} cards"),
        ["Trump"] = ("主牌 {0}", "Trump {0}"),
        ["ScoringScore"] = ("抢分方 {0} / 80", "Scorers {0} / 80"),
        ["Turn"] = ("轮到 {0}", "{0}'s turn"),
        ["Lead"] = ("请出牌领墩。", "Lead the trick."),
        ["Waiting"] = ("正在等待牌桌。", "Waiting for the table."),
        ["NoPlay"] = ("请选择要出的牌。", "Select cards to play."),
        ["HintMessage"] = ("建议：{0}", "Hint: {0}"),
        ["RushHint"] = ("抢分建议：{0}", "Rush suggestion: {0}"),
        ["DefendHint"] = ("守分建议：{0}", "Defence suggestion: {0}"),
        ["DealLog"] = ("{0} 开局，主牌是 {1}。", "{0} starts. Trump is {1}."),
        ["RoleLog"] = ("你的身份：{0}。", "Your role: {0}."),
        ["KittyLog"] = ("底牌扣下：{0}。", "Kitty buried: {0}."),
        ["PlayedLog"] = ("{0} 出 {1}。", "{0} played {1}."),
        ["WonLog"] = ("{0} 赢得本墩。", "{0} won the trick."),
        ["RushWonLog"] = ("{0} 抢到 {1} 分。", "{0} captured {1} points."),
        ["ScoringSide"] = ("抢分方：{0}", "Scoring side: {0}"),
        ["RoleScorer"] = ("你是抢分方", "You are scoring"),
        ["RoleDeclarer"] = ("你是坐庄方", "You are defending"),
        ["RushReady"] = ("有 {0} 分可抢，优先考虑拿下本墩。", "{0} points are exposed. Try to take the trick."),
        ["DefendReady"] = ("桌面有 {0} 分，尽量别让抢分方拿走。", "{0} points are exposed. Keep them away from scorers."),
        ["QuietTrick"] = ("本墩暂时没有分牌。", "No point cards in this trick yet."),
        ["TrickReview"] = ("本墩结算中，稍后自然收牌。", "Reviewing the trick; cards will clear shortly."),
        ["ScoreFlashScorer"] = ("抢分成功 +{0}", "Captured +{0}"),
        ["ScoreFlashDefend"] = ("守住 {0} 分", "Protected {0}"),
        ["RoundScorersWin"] = ("抢分方上台：{0} 分，升 {1} 级", "Scorers take over: {0} points, +{1} level(s)"),
        ["RoundDeclarersHold"] = ("坐庄方守住：{0} 分，升 {1} 级", "Declarers hold: {0} points, +{1} level(s)"),
        ["North"] = ("北家", "North"),
        ["East"] = ("东家", "East"),
        ["South"] = ("你", "You"),
        ["West"] = ("西家", "West"),
        ["NorthSouth"] = ("南北", "North/South"),
        ["EastWest"] = ("东西", "East/West"),
        ["NoTrump"] = ("无花主", "No trump")
    };

    private readonly HashSet<int> _selectedCardIds = [];
    private readonly Random _random = new();
    private UiLanguage _language = UiLanguage.Chinese;
    private GameRound _round = null!;
    private IReadOnlyList<PlayedCards>? _completedTrickSnapshot;
    private PlayerPosition? _completedTrickWinner;
    private bool _aiRunning;
    private bool _startingHand;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await StartNewHandAsync(PlayerPosition.South, animateDeal: true);
    }

    private async Task StartNewHandAsync(PlayerPosition starter, bool animateDeal)
    {
        _startingHand = true;
        _round = GameRound.CreateNew(_random.Next(), starter: starter);
        _completedTrickSnapshot = null;
        _completedTrickWinner = null;
        _selectedCardIds.Clear();
        LogList.Items.Clear();
        UpdateStaticText();
        Render();

        Log(Format("DealLog", PlayerName(starter), TrumpDisplay(_round.Trump)));
        Log(Format("RoleLog", HumanRoleText()));
        Log(Format("KittyLog", CardsText(_round.Kitty)));

        if (animateDeal)
        {
            await AnimateDealAsync();
        }

        _startingHand = false;
        Render();
        await RunAiUntilHumanTurnAsync();
    }

    private async Task RunAiUntilHumanTurnAsync()
    {
        if (_aiRunning || _startingHand)
        {
            return;
        }

        _aiRunning = true;
        try
        {
            while (_round.Phase == RoundPhase.Playing && _round.CurrentTrick.ExpectedPlayer != PlayerPosition.South)
            {
                await Task.Delay(260);

                var player = _round.CurrentTrick.ExpectedPlayer;
                var play = _ai[player].ChoosePlay(player, _round.Hand(player), _round.CurrentTrick, _round.OpponentTrickPoints, _round.PlayedCards);
                await ApplyPlayAsync(player, play);
            }
        }
        finally
        {
            _aiRunning = false;
        }
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_round.Phase == RoundPhase.Complete)
        {
            await StartNewHandAsync(PlayerPosition.South, animateDeal: true);
            return;
        }

        if (!CanHumanPlay())
        {
            MessageText.Text = Text("Waiting");
            return;
        }

        var selected = _round.Hand(PlayerPosition.South)
            .Where(card => _selectedCardIds.Contains(card.Id))
            .ToArray();

        if (selected.Length == 0)
        {
            MessageText.Text = Text("NoPlay");
            return;
        }

        var played = await ApplyPlayAsync(PlayerPosition.South, selected);
        if (!played)
        {
            return;
        }

        _selectedCardIds.Clear();
        Render();
        await RunAiUntilHumanTurnAsync();
    }

    private void HintButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanHumanPlay())
        {
            return;
        }

        var persona = IsHumanScoringSide() ? AiPersona.PointHunter : AiPersona.PartnerProtector;
        SelectSuggestedPlay(persona, Text("HintMessage"));
    }

    private void RushButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanHumanPlay())
        {
            return;
        }

        var persona = IsHumanScoringSide() ? AiPersona.PointHunter : AiPersona.TrumpController;
        SelectSuggestedPlay(persona, IsHumanScoringSide() ? Text("RushHint") : Text("DefendHint"));
    }

    private async void NewHandButton_Click(object sender, RoutedEventArgs e)
    {
        await StartNewHandAsync(PlayerPosition.South, animateDeal: true);
    }

    private async void RushHandButton_Click(object sender, RoutedEventArgs e)
    {
        await StartNewHandAsync(PlayerPosition.East, animateDeal: true);
    }

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        _language = _language == UiLanguage.Chinese ? UiLanguage.English : UiLanguage.Chinese;
        UpdateStaticText();
        Render();
    }

    private async Task<bool> ApplyPlayAsync(PlayerPosition player, IReadOnlyList<Card> cards)
    {
        var trickBefore = _round.CurrentTrick;
        var result = _round.Play(player, cards);
        if (!result.IsValid)
        {
            MessageText.Text = result.Message;
            return false;
        }

        var trickCompleted = trickBefore.IsComplete;
        var winner = trickCompleted ? trickBefore.Winner() : (PlayerPosition?)null;
        var trickPoints = trickCompleted ? CardRules.CountPoints(trickBefore.Cards()) : 0;
        if (trickCompleted)
        {
            _completedTrickSnapshot = trickBefore.Plays.ToArray();
            _completedTrickWinner = winner;
        }

        Log(Format("PlayedLog", PlayerName(player), CardsText(cards)));
        Render();

        if (trickCompleted)
        {
            Log(Format("WonLog", PlayerName(winner!.Value)));

            if (trickPoints > 0)
            {
                Log(Format("RushWonLog", PlayerName(winner.Value), trickPoints));
                await ShowScoreFlashAsync(winner.Value, trickPoints);
            }

            await Task.Delay(CompletedTrickHoldMilliseconds);
            await DismissCompletedTrickAsync();
            Render();
        }

        return true;
    }

    private void SelectSuggestedPlay(AiPersona persona, string messageFormat)
    {
        var advisor = new SeniorAiPlayer(persona);
        var play = advisor.ChoosePlay(PlayerPosition.South, _round.Hand(PlayerPosition.South), _round.CurrentTrick, _round.OpponentTrickPoints, _round.PlayedCards);

        _selectedCardIds.Clear();
        foreach (var card in play)
        {
            _selectedCardIds.Add(card.Id);
        }

        MessageText.Text = string.Format(CultureInfo.CurrentCulture, messageFormat, CardsText(play));
        RenderHand();
        Pulse(RushButton);
    }

    private void Render()
    {
        if (_round is null)
        {
            return;
        }

        UpdateStaticText();

        TrumpText.Text = Format("Trump", TrumpDisplay(_round.Trump));
        ScoreText.Text = Format("ScoringScore", _round.OpponentTrickPoints);
        RoleText.Text = HumanRoleText();
        TurnText.Text = _round.Phase == RoundPhase.Complete
            ? ResultText()
            : Format("Turn", PlayerName(_round.CurrentTrick.ExpectedPlayer));

        NorthCountText.Text = Format("Cards", _round.Hand(PlayerPosition.North).Count);
        EastCountText.Text = Format("Cards", _round.Hand(PlayerPosition.East).Count);
        WestCountText.Text = Format("Cards", _round.Hand(PlayerPosition.West).Count);
        SouthCountText.Text = Format("Cards", _round.Hand(PlayerPosition.South).Count);

        var visiblePlays = _completedTrickSnapshot ?? _round.CurrentTrick.Plays;
        var trickPoints = CardRules.CountPoints(visiblePlays.SelectMany(play => play.Cards));
        CurrentPotText.Text = trickPoints.ToString(CultureInfo.CurrentCulture);
        ScoreProgress.Value = Math.Min(80, _round.OpponentTrickPoints);
        ScoreTeamText.Text = Format("ScoringSide", TeamName(_round.Opponents));
        RushStateText.Text = RushStateTextFor(trickPoints);

        if (_completedTrickSnapshot is not null)
        {
            MessageText.Text = Text("TrickReview");
            PlayButton.Content = Text("Play");
        }
        else if (_round.Phase == RoundPhase.Complete)
        {
            MessageText.Text = ResultText();
            PlayButton.Content = Text("NewHand");
        }
        else
        {
            MessageText.Text = _round.CurrentTrick.Plays.Count == 0 ? Text("Lead") : "";
            PlayButton.Content = Text("Play");
        }

        RenderTrick();
        RenderHand();
    }

    private void UpdateStaticText()
    {
        Title = Text("AppTitle");
        TitleText.Text = Text("AppTitle");
        LanguageButton.Content = _language == UiLanguage.Chinese ? "English" : "中文";
        NewHandButton.Content = Text("NewHand");
        RushHandButton.Content = Text("RushHand");
        HintButton.Content = Text("Hint");
        RushButton.Content = IsHumanScoringSide() ? Text("Rush") : Text("Defend");
        LogTitleText.Text = Text("TableLog");
        ScorePanelTitleText.Text = Text("ScorePanel");
        PotTitleText.Text = Text("CurrentTrick");
        HandTitleText.Text = Text("YourHand");
        NorthNameText.Text = Text("North");
        EastNameText.Text = Text("East");
        SouthNameText.Text = Text("South");
        WestNameText.Text = Text("West");
    }

    private void RenderTrick()
    {
        CurrentTrickPanel.Children.Clear();
        var plays = _completedTrickSnapshot ?? _round.CurrentTrick.Plays;

        foreach (var play in plays)
        {
            var isWinner = _completedTrickWinner == play.Player;
            var panel = new Border
            {
                Margin = new Thickness(7),
                Padding = new Thickness(8),
                Background = new SolidColorBrush(Color.FromArgb(224, 20, 55, 45)),
                BorderBrush = isWinner
                    ? new SolidColorBrush(Color.FromRgb(238, 204, 111))
                    : new SolidColorBrush(Color.FromRgb(57, 123, 101)),
                BorderThickness = isWinner ? new Thickness(2) : new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Effect = TryFindResource("PanelShadow") as System.Windows.Media.Effects.Effect,
                Opacity = 0,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.94, 0.94),
                HorizontalAlignment = TrickHorizontalAlignment(play.Player),
                VerticalAlignment = TrickVerticalAlignment(play.Player),
                MaxWidth = play.Player is PlayerPosition.East or PlayerPosition.West ? 246 : 420
            };

            var cards = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };

            foreach (var card in play.Cards)
            {
                cards.Children.Add(CreateCardFace(card, selected: false, compact: true));
            }

            panel.Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = PlayerName(play.Player),
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    cards
                }
            };

            CurrentTrickPanel.Children.Add(panel);
            Grid.SetRow(panel, TrickGridRow(play.Player));
            Grid.SetColumn(panel, TrickGridColumn(play.Player));
            FadeIn(panel);
        }
    }

    private async Task DismissCompletedTrickAsync()
    {
        if (_completedTrickSnapshot is null)
        {
            return;
        }

        foreach (UIElement child in CurrentTrickPanel.Children)
        {
            child.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(520))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn }
            });
        }

        await Task.Delay(540);
        _completedTrickSnapshot = null;
        _completedTrickWinner = null;
    }

    private static int TrickGridRow(PlayerPosition player)
    {
        return player switch
        {
            PlayerPosition.North => 0,
            PlayerPosition.West or PlayerPosition.East => 1,
            PlayerPosition.South => 2,
            _ => 1
        };
    }

    private static int TrickGridColumn(PlayerPosition player)
    {
        return player switch
        {
            PlayerPosition.West => 0,
            PlayerPosition.North or PlayerPosition.South => 1,
            PlayerPosition.East => 2,
            _ => 1
        };
    }

    private static HorizontalAlignment TrickHorizontalAlignment(PlayerPosition player)
    {
        return player switch
        {
            PlayerPosition.West => HorizontalAlignment.Left,
            PlayerPosition.East => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center
        };
    }

    private static VerticalAlignment TrickVerticalAlignment(PlayerPosition player)
    {
        return player switch
        {
            PlayerPosition.North => VerticalAlignment.Top,
            PlayerPosition.South => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center
        };
    }

    private void RenderHand()
    {
        HumanHandPanel.Children.Clear();
        foreach (var card in _round.Hand(PlayerPosition.South))
        {
            var selected = _selectedCardIds.Contains(card.Id);
            var button = new Button
            {
                Tag = card,
                Width = 68,
                Height = 98,
                Margin = new Thickness(5),
                Style = (Style)FindResource("FlatCardButtonStyle"),
                Content = CreateCardFace(card, selected, compact: false),
                RenderTransform = new TranslateTransform(0, selected ? -12 : 0)
            };

            button.Click += CardButton_Click;
            HumanHandPanel.Children.Add(button);
        }

        SelectionText.Text = Format("Selected", _selectedCardIds.Count);
    }

    private void CardButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanHumanPlay() || sender is not Button { Tag: Card card })
        {
            return;
        }

        if (!_selectedCardIds.Add(card.Id))
        {
            _selectedCardIds.Remove(card.Id);
        }

        RenderHand();
    }

    private Border CreateCardFace(Card card, bool selected, bool compact)
    {
        var width = compact ? 56 : 64;
        var height = compact ? 78 : 92;
        var isRed = card.Joker == JokerColor.Red || card.Suit is Suit.Hearts or Suit.Diamonds;
        var foreground = isRed
            ? new SolidColorBrush(Color.FromRgb(177, 42, 54))
            : new SolidColorBrush(Color.FromRgb(20, 30, 27));
        var background = selected
            ? new LinearGradientBrush(Color.FromRgb(255, 231, 157), Color.FromRgb(230, 181, 79), 90)
            : new LinearGradientBrush(Color.FromRgb(255, 255, 249), Color.FromRgb(225, 232, 218), 90);

        var cardGrid = new Grid();
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var rank = card.IsJoker
            ? card.Joker == JokerColor.Red ? "R" : "B"
            : Card.RankText(card.Rank!.Value);
        var suit = card.IsJoker ? "JOKER" : SuitSymbol(card.Suit!.Value);

        cardGrid.Children.Add(new TextBlock
        {
            Text = rank,
            Foreground = foreground,
            FontSize = compact ? rank.Length > 1 ? 15 : 18 : rank.Length > 1 ? 17 : 21,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(5, 2, 5, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        });

        var center = new TextBlock
        {
            Text = suit,
            Foreground = foreground,
            FontSize = card.IsJoker ? compact ? 10 : 11 : compact ? 26 : 28,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(center, 1);
        cardGrid.Children.Add(center);

        if (card.PointValue > 0)
        {
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(220, 184, 85)),
                CornerRadius = new CornerRadius(6),
                Padding = compact ? new Thickness(4, 1, 4, 1) : new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 5, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = new TextBlock
                {
                    Text = $"+{card.PointValue}",
                    Foreground = new SolidColorBrush(Color.FromRgb(20, 30, 27)),
                    FontSize = compact ? 10 : 11,
                    FontWeight = FontWeights.Bold
                }
            };
            Grid.SetRow(badge, 2);
            cardGrid.Children.Add(badge);
        }

        return new Border
        {
            Width = width,
            Height = height,
            Background = background,
            BorderBrush = selected
                ? new SolidColorBrush(Color.FromRgb(255, 236, 158))
                : new SolidColorBrush(Color.FromRgb(208, 216, 203)),
            BorderThickness = selected ? new Thickness(2) : new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Effect = TryFindResource("CardShadow") as System.Windows.Media.Effects.Effect,
            Child = cardGrid
        };
    }

    private async Task AnimateDealAsync()
    {
        await Task.Delay(120);
        RootGrid.UpdateLayout();
        AnimationCanvas.Children.Clear();

        var center = new Point(Math.Max(0, AnimationCanvas.ActualWidth / 2 - 26), Math.Max(0, AnimationCanvas.ActualHeight / 2 - 36));
        var targets = new[]
        {
            SeatTarget(SouthSeat),
            SeatTarget(EastSeat),
            SeatTarget(NorthSeat),
            SeatTarget(WestSeat)
        };

        for (var i = 0; i < 40; i++)
        {
            var card = CreateCardBack();
            Canvas.SetLeft(card, center.X);
            Canvas.SetTop(card, center.Y);
            AnimationCanvas.Children.Add(card);

            var target = targets[i % targets.Length];
            var duration = TimeSpan.FromMilliseconds(360);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            card.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(center.X, target.X, duration) { EasingFunction = easing });
            card.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(center.Y, target.Y, duration) { EasingFunction = easing });
            card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.18, duration) { BeginTime = TimeSpan.FromMilliseconds(180) });

            await Task.Delay(28);
        }

        await Task.Delay(520);
        AnimationCanvas.Children.Clear();
        Pulse(SouthSeat);
    }

    private Border CreateCardBack()
    {
        var grid = new Grid();
        grid.Children.Add(new Border
        {
            Margin = new Thickness(7),
            BorderBrush = new SolidColorBrush(Color.FromRgb(230, 198, 104)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5)
        });
        grid.Children.Add(new TextBlock
        {
            Text = "80",
            Foreground = new SolidColorBrush(Color.FromRgb(246, 227, 154)),
            FontWeight = FontWeights.Bold,
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        return new Border
        {
            Width = 52,
            Height = 72,
            Background = new LinearGradientBrush(Color.FromRgb(21, 71, 59), Color.FromRgb(10, 28, 24), 45),
            BorderBrush = new SolidColorBrush(Color.FromRgb(55, 134, 107)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Effect = TryFindResource("CardShadow") as System.Windows.Media.Effects.Effect,
            Child = grid
        };
    }

    private Point SeatTarget(FrameworkElement seat)
    {
        var point = seat.TranslatePoint(new Point(seat.ActualWidth / 2 - 26, seat.ActualHeight / 2 - 36), RootGrid);
        return new Point(point.X, point.Y);
    }

    private async Task ShowScoreFlashAsync(PlayerPosition winner, int trickPoints)
    {
        var scoringSideWon = winner.Team() == _round.Opponents;
        ScoreFlashText.Text = string.Format(
            CultureInfo.CurrentCulture,
            Text(scoringSideWon ? "ScoreFlashScorer" : "ScoreFlashDefend"),
            trickPoints);
        ScoreFlash.BorderBrush = scoringSideWon
            ? (Brush)FindResource("GoldBrush")
            : new SolidColorBrush(Color.FromRgb(86, 185, 148));
        ScoreFlash.Visibility = Visibility.Visible;
        ScoreFlash.Opacity = 0;
        ScoreFlash.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(0.82, 0.82);
        ScoreFlash.RenderTransform = scale;

        ScoreFlash.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.82, 1.04, TimeSpan.FromMilliseconds(220)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 } });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.82, 1.04, TimeSpan.FromMilliseconds(220)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 } });

        await Task.Delay(880);
        ScoreFlash.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(260)));
        await Task.Delay(270);
        ScoreFlash.Visibility = Visibility.Collapsed;
    }

    private static void FadeIn(FrameworkElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        if (element.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(180)));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(180)));
        }
    }

    private static void Pulse(UIElement element)
    {
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(1, 1);
        element.RenderTransform = scale;
        var animation = new DoubleAnimation(1, 1.035, TimeSpan.FromMilliseconds(160))
        {
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private bool CanHumanPlay()
    {
        return !_startingHand
            && !_aiRunning
            && _completedTrickSnapshot is null
            && _round.Phase == RoundPhase.Playing
            && _round.CurrentTrick.ExpectedPlayer == PlayerPosition.South;
    }

    private bool IsHumanScoringSide()
    {
        return _round is not null && PlayerPosition.South.Team() == _round.Opponents;
    }

    private string RushStateTextFor(int trickPoints)
    {
        if (trickPoints <= 0)
        {
            return Text("QuietTrick");
        }

        return IsHumanScoringSide()
            ? Format("RushReady", trickPoints)
            : Format("DefendReady", trickPoints);
    }

    private string ResultText()
    {
        var result = _round.Result;
        if (result is null)
        {
            return "";
        }

        return result.OpponentsWon
            ? Format("RoundScorersWin", result.OpponentPoints, result.LevelDelta)
            : Format("RoundDeclarersHold", result.OpponentPoints, result.LevelDelta);
    }

    private string HumanRoleText()
    {
        return Text(IsHumanScoringSide() ? "RoleScorer" : "RoleDeclarer");
    }

    private void Log(string message)
    {
        LogList.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
    }

    private string Text(string key)
    {
        var pair = _text[key];
        return _language == UiLanguage.Chinese ? pair.Chinese : pair.English;
    }

    private string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Text(key), args);
    }

    private string PlayerName(PlayerPosition player)
    {
        return player switch
        {
            PlayerPosition.North => Text("North"),
            PlayerPosition.East => Text("East"),
            PlayerPosition.South => Text("South"),
            PlayerPosition.West => Text("West"),
            _ => player.ToString()
        };
    }

    private string TeamName(Team team)
    {
        return team == Team.NorthSouth ? Text("NorthSouth") : Text("EastWest");
    }

    private string CardsText(IEnumerable<Card> cards)
    {
        return string.Join(" ", cards.Select(CardDisplay));
    }

    private string CardDisplay(Card card)
    {
        if (card.IsJoker)
        {
            return card.Joker == JokerColor.Red
                ? _language == UiLanguage.Chinese ? "大王" : "RJ"
                : _language == UiLanguage.Chinese ? "小王" : "BJ";
        }

        return $"{Card.RankText(card.Rank!.Value)}{SuitSymbol(card.Suit!.Value)}";
    }

    private string TrumpDisplay(TrumpConfig trump)
    {
        return trump.Suit.HasValue
            ? $"{Card.RankText(trump.Rank)} {SuitSymbol(trump.Suit.Value)}"
            : $"{Card.RankText(trump.Rank)} {Text("NoTrump")}";
    }

    private static string SuitSymbol(Suit suit)
    {
        return suit switch
        {
            Suit.Clubs => "\u2663",
            Suit.Diamonds => "\u2666",
            Suit.Hearts => "\u2665",
            Suit.Spades => "\u2660",
            _ => "?"
        };
    }
}
