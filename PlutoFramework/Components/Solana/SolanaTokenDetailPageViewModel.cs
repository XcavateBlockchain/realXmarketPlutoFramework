using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microcharts;
using PlutoFramework.Components.AddressView;
using PlutoFramework.Model;
using PlutoFramework.Model.Constants;
using PlutoFramework.Model.Currency;
using PlutoFrameworkCore.Solana;
using SkiaSharp;

namespace PlutoFramework.Components.Solana
{
    /// <summary>
    /// One token's detail page. Holdings and on-chain facts come from the row the user
    /// tapped; only a chartable token reaches the network at all.
    /// </summary>
    public partial class SolanaTokenDetailPageViewModel : ObservableObject
    {
        /// <summary>
        /// Matches <c>AssetDetailViewModel</c>, and matches what Jupiter's <c>candles</c>
        /// parameter is asked for, so the four time labels line up with the plotted points.
        /// </summary>
        private const int CHART_STEPS = 24;

        private const string PositiveColor = "#2E9E5B";
        private const string NegativeColor = "#CC3333";

        /// <summary>
        /// Cancels and replaces itself at the top of every <see cref="LoadAsync"/> call.
        /// Mirrors <see cref="SolanaBalancesPageViewModel"/>: without it, tapping 1d then 3w
        /// quickly lets the slower first response land last and draw the wrong range under
        /// the newly selected button.
        /// </summary>
        private CancellationTokenSource? loadCts;

        private readonly SolanaTokenBalance balance;

        public SolanaTokenDetailPageViewModel(SolanaTokenBalance balance)
        {
            this.balance = balance;

            Symbol = balance.Symbol;
            Mint = balance.Mint;
            IconSource = Assets.GetAssetIcon(balance.Symbol);
            DecimalsText = balance.Decimals.ToString();
            AmountText = $"{SolanaAmount.ToDisplayString(balance.Amount, balance.Decimals)} {balance.Symbol}";

            // An unknown value shows nothing at all, matching SolanaAssetView. "$0.00" would
            // read as "your money is gone" rather than "we could not reach the price feed".
            UsdValueText = balance.UsdValue is double usd ? usd.ToUsdCurrencyString() : string.Empty;

            SolanaNetworkModel.ClusterChanged += OnClusterChanged;
        }

        public string Symbol { get; }

        public string Mint { get; }

        public string IconSource { get; }

        public string DecimalsText { get; }

        public string AmountText { get; }

        public string UsdValueText { get; }

        /// <summary>
        /// A mint is 32-44 base58 characters and will not fit beside its label. The full
        /// value is still reachable - tapping the row copies it.
        /// </summary>
        public string MintShort => Mint.Length <= 13
            ? Mint
            : $"{Mint[..6]}…{Mint[^4..]}";

        public string NetworkName => SolanaNetworkModel.SelectedCluster.GetName();

        /// <summary>
        /// Stablecoins never show a chart, so the whole price block - chart, interval
        /// buttons, live price and change - is absent rather than empty.
        /// </summary>
        public bool ChartIsVisible => balance.ShowPriceChart;

        /// <summary>
        /// Drives the "unavailable" message only. Starts false so the first load shows blank
        /// space rather than flashing a failure the moment the page opens.
        /// </summary>
        [ObservableProperty]
        private bool historyIsUnavailable = false;

        /// <summary>
        /// Drives the chart itself. Keyed off the points rather than
        /// <see cref="HistoryIsUnavailable"/> so the ChartView is never mounted with an empty
        /// series - Microcharts sizes its items by dividing by the entry count. It also
        /// leaves the interval buttons on screen when the chart is hidden, so switching range
        /// is how the user retries a failed fetch.
        /// </summary>
        public bool HasHistory => Points.Count >= 2;

        [ObservableProperty]
        private string priceText = "-";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ChangeIsVisible))]
        private string changeText = string.Empty;

        [ObservableProperty]
        private Color changeColor = Colors.Gray;

        public bool ChangeIsVisible => !string.IsNullOrEmpty(ChangeText);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HourlyIsSelected))]
        [NotifyPropertyChangedFor(nameof(DailyIsSelected))]
        [NotifyPropertyChangedFor(nameof(WeeklyIsSelected))]
        private Interval chartInterval = Interval.Hourly;

        public bool HourlyIsSelected => ChartInterval == Interval.Hourly;

        public bool DailyIsSelected => ChartInterval == Interval.Daily;

        public bool WeeklyIsSelected => ChartInterval == Interval.Weekly;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Chart))]
        [NotifyPropertyChangedFor(nameof(HasHistory))]
        private IReadOnlyList<SolanaPricePoint> points = [];

        [ObservableProperty]
        private string time1Text = string.Empty;

        [ObservableProperty]
        private string time2Text = string.Empty;

        [ObservableProperty]
        private string time3Text = string.Empty;

        [ObservableProperty]
        private string time4Text = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MinMaxIsVisible))]
        private string minText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MinMaxIsVisible))]
        private string maxText = string.Empty;

        /// <summary>Hidden when they coincide, so a flat range does not print the same number twice.</summary>
        public bool MinMaxIsVisible => MinText != MaxText;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MinLayoutBounds))]
        [NotifyPropertyChangedFor(nameof(MinMargin))]
        private double minXPosition = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MaxLayoutBounds))]
        [NotifyPropertyChangedFor(nameof(MaxMargin))]
        private double maxXPosition = 0;

        public Rect MinLayoutBounds =>
            new(MinXPosition, 1, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize);

        public Rect MaxLayoutBounds =>
            new(MaxXPosition, 0, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize);

        /// <summary>Nudges a label off the edge when its extreme falls at either end.</summary>
        public Thickness MinMargin => EdgeMargin(MinXPosition);

        public Thickness MaxMargin => EdgeMargin(MaxXPosition);

        private static Thickness EdgeMargin(double xPosition) => xPosition switch
        {
            0 => new Thickness(15, 0, 0, 0),
            1 => new Thickness(0, 0, 15, 0),
            _ => new Thickness(0),
        };

        public LineChart Chart
        {
            get
            {
                var color = SKColor.Parse(((Color)Application.Current!.Resources["Primary"]).ToHex());

                var (floor, ceiling) = Bounds();

                return new LineChart
                {
                    Margin = 0,
                    LabelTextSize = 32,
                    LabelOrientation = Orientation.Vertical,
                    LineSize = 20,
                    MinValue = (float)floor,
                    MaxValue = (float)ceiling,
                    Entries = Points
                        .Select(point => new ChartEntry((float)point.UsdPrice)
                        {
                            Color = color,
                            ValueLabelColor = color,
                        })
                        .ToList(),
                    ValueLabelOrientation = Orientation.Vertical,
                    ValueLabelOption = ValueLabelOption.TopOfElement,
                    ValueLabelTextSize = 32,
                    PointMode = PointMode.None,
                    Typeface = SKTypeface.FromFamilyName("XcavateFont"),
                };
            }
        }

        /// <summary>
        /// Ten percent of headroom either side, so the line is not pinned to the frame. A
        /// perfectly flat series has no spread to take ten percent of, so its own level is
        /// used instead.
        /// </summary>
        private (double Floor, double Ceiling) Bounds()
        {
            if (Points.Count == 0)
            {
                return (0, 1);
            }

            var min = Points.Min(point => point.UsdPrice);
            var max = Points.Max(point => point.UsdPrice);

            var spread = (max - min) * 0.1;
            var padding = spread == 0 ? max * 0.1 : spread;

            return (min - padding, max + padding);
        }

        /// <summary>
        /// Called by the page when it disappears. Without it the static event keeps every
        /// view model this page ever created alive. Also cancels any in-flight load, so a
        /// request started before navigating away cannot resolve into a discarded page.
        /// </summary>
        public void Unsubscribe()
        {
            SolanaNetworkModel.ClusterChanged -= OnClusterChanged;

            loadCts?.Cancel();
        }

        /// <summary>
        /// A mint identifies a different token on each cluster - USDC's devnet mint does not
        /// exist on mainnet - so this page's subject may simply not be there anymore.
        /// Returning to the balances list is the honest response; redrawing would leave the
        /// page's own Network row asserting a cluster the mint does not belong to.
        /// </summary>
        private void OnClusterChanged(object? sender, SolanaCluster cluster) =>
            MainThread.BeginInvokeOnMainThread(async () => await NavigationModel.PopAsync());

        /// <summary>
        /// Mirrors <see cref="SolanaBalancesPageViewModel.ReplaceLoadingToken"/>. Not
        /// lock-protected for the same reason: every caller runs on the UI thread's single
        /// synchronization context, so only sequential interleaving of awaits has to be
        /// resolved, which cancellation alone already does.
        /// </summary>
        private CancellationToken ReplaceLoadingToken(CancellationToken externalToken)
        {
            var previousCts = loadCts;
            var newCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

            loadCts = newCts;

            previousCts?.Cancel();
            previousCts?.Dispose();

            return newCts.Token;
        }

        [RelayCommand]
        public void ChangeChartInterval(Interval interval)
        {
            if (ChartInterval == interval)
            {
                return;
            }

            ChartInterval = interval;

            _ = LoadAsync(CancellationToken.None);
        }

        /// <summary>
        /// Opens the QR popup straight from the Solana key. Deliberately not
        /// <c>ReceiveAndTransferModel.Receive()</c>, which returns early when there is no
        /// Substrate key and would show a "no account" popup to a Solana-only user looking
        /// at their own balance.
        /// </summary>
        [RelayCommand]
        public void Receive()
        {
            var address = KeysModel.GetSolanaAddress();

            if (string.IsNullOrEmpty(address))
            {
                return;
            }

            var qrViewModel = DependencyService.Get<AddressQrCodeViewModel>();

            qrViewModel.Address = address;
            qrViewModel.QrAddress = $"solana:{address}";
            qrViewModel.IsVisible = true;
        }

        [RelayCommand]
        public Task CopyMintAsync() => CopyAddress.CopyToClipboardAsync(Mint);

        public async Task LoadAsync(CancellationToken token)
        {
            if (!ChartIsVisible)
            {
                // A stablecoin's page is entirely built from the row it was constructed with.
                // Nothing to fetch, so nothing to fail.
                return;
            }

            var loadToken = ReplaceLoadingToken(token);

            try
            {
                // Independent best-effort calls: neither should wait on the other.
                var historyTask = SolanaPriceHistoryModel.GetPriceHistoryAsync(
                    Mint, ChartInterval, CHART_STEPS, loadToken);

                var quoteTask = SolanaPriceModel.GetSpotQuoteAsync(Mint, loadToken);

                await Task.WhenAll(historyTask, quoteTask);

                // Guards a load that finished normally after a newer one superseded it.
                // ReplaceLoadingToken cancels the previous source synchronously, so a stale
                // token already reports cancellation regardless of how the awaits completed.
                loadToken.ThrowIfCancellationRequested();

                ApplyHistory(await historyTask);
                ApplyQuote(await quoteTask);
            }
            catch (OperationCanceledException)
            {
                // The page went away mid-query, or a newer interval superseded this one.
            }
        }

        /// <summary>
        /// Fewer than two points cannot make a line. The chart is replaced by a message
        /// rather than a flat placeholder series: a straight line at an arbitrary level
        /// reads as a real, stable price, which is the exact misreading this page avoids.
        /// </summary>
        private void ApplyHistory(IReadOnlyList<SolanaPricePoint> history)
        {
            if (history.Count < 2)
            {
                HistoryIsUnavailable = true;
                Points = [];

                return;
            }

            HistoryIsUnavailable = false;
            Points = history;

            var minIndex = IndexOfExtreme(history, lowest: true);
            var maxIndex = IndexOfExtreme(history, lowest: false);

            MinText = history[minIndex].UsdPrice.ToUsdCurrencyString(currencyFormat: "{0:0.00}");
            MaxText = history[maxIndex].UsdPrice.ToUsdCurrencyString(currencyFormat: "{0:0.00}");

            MinXPosition = (double)minIndex / (history.Count - 1);
            MaxXPosition = (double)maxIndex / (history.Count - 1);

            ApplyTimeLabels(history);
        }

        /// <summary>
        /// The position of the highest or lowest point, as an index rather than the point
        /// itself: two candles can close at the same price, and the label has to sit above
        /// one particular column.
        /// </summary>
        private static int IndexOfExtreme(IReadOnlyList<SolanaPricePoint> history, bool lowest)
        {
            var index = 0;

            for (var candidate = 1; candidate < history.Count; candidate++)
            {
                var isBetter = lowest
                    ? history[candidate].UsdPrice < history[index].UsdPrice
                    : history[candidate].UsdPrice > history[index].UsdPrice;

                if (isBetter)
                {
                    index = candidate;
                }
            }

            return index;
        }

        /// <summary>
        /// Four labels under a series of <see cref="CHART_STEPS"/> points, each sitting at
        /// the centre of its quarter. Read from the points' own timestamps rather than
        /// counted back from now, so a short or gapped series still labels itself correctly.
        /// </summary>
        private void ApplyTimeLabels(IReadOnlyList<SolanaPricePoint> history)
        {
            Time1Text = TimeLabel(history, 0.125);
            Time2Text = TimeLabel(history, 0.375);
            Time3Text = TimeLabel(history, 0.625);
            Time4Text = TimeLabel(history, 0.875);
        }

        private string TimeLabel(IReadOnlyList<SolanaPricePoint> history, double position)
        {
            var index = Math.Clamp((int)(history.Count * position), 0, history.Count - 1);

            var time = history[index].Time.ToLocalTime();

            return ChartInterval switch
            {
                Interval.Hourly => time.ToString("HH:mm"),
                _ => time.ToString("MMM d"),
            };
        }

        private void ApplyQuote(SolanaSpotQuote? quote)
        {
            if (quote is null)
            {
                PriceText = "-";
                ChangeText = string.Empty;

                return;
            }

            PriceText = quote.UsdPrice.ToUsdCurrencyString();

            if (quote.Change24h is not double change)
            {
                // Absent, not zero. Printing "+0.00%" over missing data asserts the price
                // held steady when we simply do not know.
                ChangeText = string.Empty;

                return;
            }

            ChangeText = $"{change:+0.00;-0.00}%";
            ChangeColor = Color.FromArgb(change < 0 ? NegativeColor : PositiveColor);
        }
    }
}
