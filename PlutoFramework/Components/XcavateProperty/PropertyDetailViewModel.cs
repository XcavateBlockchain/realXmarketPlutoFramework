using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Components.Loading;
using PlutoFramework.Components.MessagePopup;
using PlutoFramework.Components.Messages;
using PlutoFramework.Components.TransactionAnalyzer;
using PlutoFramework.Components.WebView;
using PlutoFramework.Constants;
using PlutoFramework.Model;
using PlutoFramework.Model.Currency;
using PlutoFramework.Model.SQLite;
using PlutoFramework.Model.Xcavate;
using PlutoFramework.Model.Xcavate.Profile;
using PlutoFrameworkCore.AssetDidComm;
using PlutoFrameworkCore.Solana;
using PlutoFrameworkCore.Xcavate;
using System.Collections.ObjectModel;
using UniqueryPlus.Metadata;
using UniqueryPlus.Nfts;
using XcavateDevnetIndexer;
using PropertyModel = PlutoFramework.Model.Xcavate.XcavatePropertyModel;

namespace PlutoFramework.Components.XcavateProperty
{
    public enum MainActionStates
    {
        CanNotInvest,
        Buy,
        ListingExpired,
        SpvToBeCreated,
        CreateSpv,
        RefundBought,
        SoldOut,
        Claim,
        ToBeClaimed,
        ClaimExpired,
        RefundUnclaimed,
        RefundClaimed,
        Relist,
        MakeOffer,
        Unknown
    }

    public partial class PropertyDetailViewModel : ObservableObject
    {
        /// <summary>
        /// The messenger route that shows one namespace's buckets, mirroring the
        /// indexed-bucket deep link format in <c>NotificationDeepLinkModel</c>.
        /// </summary>
        private const string NamespaceUrlFormat =
            "https://" + MessengerDashboard.Host + "/messages/namespace/{0}?isHeaderVisible=false&primaryColor=%233B4F74";

        private MainActionStates getMainActionState()
        {
            if (NftWrapper!.ListingHasExpired && ListingDetails?.ListedTokens > 0 && TokensBought > 0)
            {
                return MainActionStates.RefundBought;
            }

            if (NftWrapper.ListingHasExpired && ListingDetails?.ListedTokens > 0)
            {
                return MainActionStates.ListingExpired;
            }

            if (!NftWrapper.ListingHasExpired && ListingDetails?.ListedTokens > 0)
            {
                return MainActionStates.Buy;
            }

            if (ListingDetails?.ListedTokens == 0 && !SpvCreated && (Roles?.Contains(XcavateRole.SpvConfirmation) ?? false))
            {
                return MainActionStates.CreateSpv;
            }

            if (ListingDetails?.ListedTokens == 0 && !SpvCreated && !(Roles?.Contains(XcavateRole.SpvConfirmation) ?? false))
            {
                return MainActionStates.SpvToBeCreated;
            }

            if (NftWrapper.ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0 && TokensOwned > 0)
            {
                return MainActionStates.RefundClaimed;
            }

            if (NftWrapper.ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0 && TokensBought > 0)
            {
                return MainActionStates.RefundUnclaimed;
            }

            if (NftWrapper.ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0)
            {
                return MainActionStates.ClaimExpired;
            }

            if (!NftWrapper.ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0 && TokensBought > 0)
            {
                return MainActionStates.Claim;
            }

            if (!NftWrapper.ClaimHasExpired && ListingDetails?.UnclaimedTokens > 0 && TokensBought == 0)
            {
                return MainActionStates.ToBeClaimed;
            }

            if (ListingDetails?.UnclaimedTokens == 0 && TokensOwned > 0)
            {
                return MainActionStates.Relist;
            }

            // Opened from a secondary-market card and this wallet is not the seller: the
            // primary lifecycle actions above all passed, so negotiating the listed
            // shares is the action the page exists for. The seller keeps whatever their
            // own position says (Relist when they still hold tokens) - offering on your
            // own listing is meaningless.
            if (HasShareListingContext
                && !string.Equals(KeysModel.GetSolanaAddress(), ShareListingContext!.Seller, StringComparison.Ordinal))
            {
                return MainActionStates.MakeOffer;
            }

            if (!NftWrapper.ListingHasExpired && ListingDetails?.ListedTokens == 0 && TokensBought == 0 && TokensOwned == 0)
            {
                return MainActionStates.SoldOut;
            }

            return MainActionStates.Unknown;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(StatusIsVisible))]
        [NotifyPropertyChangedFor(nameof(MainActionButtonState))]
        [NotifyPropertyChangedFor(nameof(MainActionText))]
        [NotifyPropertyChangedFor(nameof(ShowClaimSpvCaseButton))]
        private HashSet<XcavateRole>? roles = null;

        /// <summary>
        /// The role picked in Settings as the main page (the same setting
        /// <see cref="MainPageRoleModel.GetSelectedRoleAsync"/> serves the app shell). One
        /// wallet can carry several on-chain roles; this page's bottom action follows the
        /// chosen one: Investor and LettingAgent keep the investor flow, Developer gets
        /// "Assign Developer Lawyer", Lawyer gets the SPV-case claim. Defaults to Investor
        /// so anything that never sets it keeps today's behaviour.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowInvestorMainAction))]
        [NotifyPropertyChangedFor(nameof(ShowAssignDeveloperLawyerButton))]
        [NotifyPropertyChangedFor(nameof(ShowClaimSpvCaseButton))]
        private MainPageRole mainPageRole = MainPageRole.Investor;

        /// <summary>Re-reads the Settings main-page role - called on every page appearance.</summary>
        public async Task RefreshMainPageRoleAsync()
        {
            var role = await MainPageRoleModel.GetSelectedRoleAsync().ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() => MainPageRole = role);

            // The claim button's own-candidacy state depends on the role being current.
            await RefreshLawyerClaimStateAsync(CancellationToken.None).ConfigureAwait(false);
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MainActionButtonState))]
        [NotifyPropertyChangedFor(nameof(MainActionText))]
        [NotifyPropertyChangedFor(nameof(ShowClaimSpvCaseButton))]
        private bool spvCreated;

        [ObservableProperty]
        private Endpoint? endpoint;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(StatusIsVisible))]
        [NotifyPropertyChangedFor(nameof(MainActionButtonState))]
        [NotifyPropertyChangedFor(nameof(MainActionText))]
        [NotifyPropertyChangedFor(nameof(ShowBuyMoreButtons))]
        [NotifyPropertyChangedFor(nameof(LawyersSectionIsVisible))]
        [NotifyPropertyChangedFor(nameof(DeveloperLawyerText))]
        [NotifyPropertyChangedFor(nameof(DeveloperLawyerStatusIsVisible))]
        [NotifyPropertyChangedFor(nameof(DeveloperLawyerStatusText))]
        [NotifyPropertyChangedFor(nameof(DeveloperLawyerStatusColor))]
        [NotifyPropertyChangedFor(nameof(SpvLawyerText))]
        [NotifyPropertyChangedFor(nameof(SpvLawyerStatusIsVisible))]
        [NotifyPropertyChangedFor(nameof(SpvLawyerStatusText))]
        [NotifyPropertyChangedFor(nameof(SpvLawyerStatusColor))]
        [NotifyPropertyChangedFor(nameof(ShowAssignDeveloperLawyerButton))]
        [NotifyPropertyChangedFor(nameof(ShowClaimSpvCaseButton))]
        private XcavateNftWrapper? nftWrapper;

        [ObservableProperty]
        private XcavateRegion? region;

        /// <summary>
        /// True until the property details have been fetched and the real content can be
        /// shown. The page renders a skeleton (see PropertyDetailSkeletonView) while this is
        /// set, so navigation to the page is instant - no full-screen loading overlay.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLoaded))]
        [NotifyPropertyChangedFor(nameof(ShowInvestorMainAction))]
        [NotifyPropertyChangedFor(nameof(ShowAssignDeveloperLawyerButton))]
        [NotifyPropertyChangedFor(nameof(ShowClaimSpvCaseButton))]
        private bool isLoading = true;

        public bool IsLoaded => !IsLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AreaPricesPercentage))]
        [NotifyPropertyChangedFor(nameof(RentalDemandPercentage))]
        [NotifyPropertyChangedFor(nameof(LocationShortName))]
        [NotifyPropertyChangedFor(nameof(PropertyImages))]
        [NotifyPropertyChangedFor(nameof(PropertyFullImages))]
        [NotifyPropertyChangedFor(nameof(PropertyStatus))]
        [NotifyPropertyChangedFor(nameof(PropertyAddressLine))]
        [NotifyPropertyChangedFor(nameof(ListingPrice))]
        [NotifyPropertyChangedFor(nameof(PricePerTokenText))]
        [NotifyPropertyChangedFor(nameof(Apy))]
        [NotifyPropertyChangedFor(nameof(TokensAvailable))]
        [NotifyPropertyChangedFor(nameof(RentalIncome))]
        [NotifyPropertyChangedFor(nameof(TokensOwnedWorth))]
        [NotifyPropertyChangedFor(nameof(TokensBoughtWorth))]
        [NotifyPropertyChangedFor(nameof(CompanyName))]
        [NotifyPropertyChangedFor(nameof(CompanyImage))]
        [NotifyPropertyChangedFor(nameof(PropertyArea))]
        [NotifyPropertyChangedFor(nameof(OffStreetParking))]
        [NotifyPropertyChangedFor(nameof(OutdoorSpace))]
        [NotifyPropertyChangedFor(nameof(NumberOfBedrooms))]
        [NotifyPropertyChangedFor(nameof(ConstructionDate))]
        [NotifyPropertyChangedFor(nameof(NumberOfBathrooms))]
        [NotifyPropertyChangedFor(nameof(Quality))]
        private PropertyMetadata? metadata;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MainActionButtonState))]
        [NotifyPropertyChangedFor(nameof(MainActionText))]
        [NotifyPropertyChangedFor(nameof(ShowBuyMoreButtons))]
        [NotifyPropertyChangedFor(nameof(TokensAvailable))]
        private XcavateOngoingObjectListingDetails? listingDetails;

        /// <summary>
        /// The secondary-market share listing this page was opened from (the noticeboard's
        /// cheapest active listing for the property); null when the page came from a
        /// primary-market feed. Stored for the secondary-market actions (make an offer,
        /// buy the listed shares) to build on - it carries the share-listing id, seller,
        /// asking price and amount those flows are keyed by.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasShareListingContext))]
        [NotifyPropertyChangedFor(nameof(MainActionButtonState))]
        [NotifyPropertyChangedFor(nameof(MainActionText))]
        private XcavateShareListingContext? shareListingContext;

        /// <summary>True when the page was opened from a secondary-market card.</summary>
        public bool HasShareListingContext => ShareListingContext is not null;

        public double AreaPricesPercentage => PropertyModel.GetAreaPricesPercentage(Metadata?.Financials.PropertyPrice ?? 0);
        public double RentalDemandPercentage => PropertyModel.GetRentalDemand();

        public string LocationShortName => $"{Metadata?.Address.Street}, {Metadata?.Address.TownCity}";

        public string PropertyAddressLine => Metadata is null
            ? "Unknown address"
            : $"{Metadata.Address.FlatOrUnit}, {Metadata.Address.Street}, {Metadata.Address.TownCity}, {Metadata.Address.PostCode}";

        // The detail page's gallery shows the compressed mirror thumbnails; only the
        // full-screen image page (expand) loads the full-resolution originals.
        public IReadOnlyList<string> PropertyImages => Metadata?.DisplayImages ?? [];

        public IReadOnlyList<string> PropertyFullImages => Metadata?.Files ?? [];

        public string PropertyStatus => Metadata?.Status ?? "Unknown";

        public string ListingPrice => ((decimal)(Metadata?.Financials.PropertyPrice ?? 0)).ToCurrencyString();

        public string PricePerTokenText => $"{((decimal)(Metadata?.Financials.PricePerToken ?? 0)).ToCurrencyString()}";

        public string Apy => PropertyModel.GetAPY(Metadata?.Financials);

        public string TokensAvailable => $"{ListingDetails?.ListedTokens.ToString() ?? "-"} / {Metadata?.Financials.NumberOfTokens.ToString() ?? "-"}";

        public string RentalIncome => ((decimal)(Metadata?.Financials.EstimatedRentalIncome ?? 0)).ToCurrencyString();

        public string CompanyName => Metadata?.Company?.Name ?? "Unknown company";

        public string CompanyImage => Metadata?.Company?.Logo ?? "xcavate.png";

        public string PropertyArea => Metadata?.Attributes?.Area ?? "Unknown";

        public string OffStreetParking => Metadata?.Attributes?.OffStreetParking ?? "Unknown";

        public string OutdoorSpace => Metadata?.Attributes?.OutdoorSpace ?? "Unknown";

        public string NumberOfBedrooms => Metadata?.Attributes?.NumberOfBedrooms?.ToString() ?? "Unknown";

        public string ConstructionDate => DateTime.TryParse(Metadata?.Attributes?.ConstructionDate, out var constructionDate)
            ? constructionDate.ToString("yyyy-MM-dd")
            : Metadata?.Attributes?.ConstructionDate ?? "Unknown";

        public string NumberOfBathrooms => Metadata?.Attributes?.NumberOfBathrooms?.ToString() ?? "Unknown";

        public string Quality => Metadata?.Attributes?.Quality ?? "Unknown";


        [RelayCommand]
        public Task OpenMapAsync() => Task.FromResult(0); //Browser.Default.OpenAsync(<location url>, BrowserLaunchMode.SystemPreferred);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TokensBoughtWorth))]
        [NotifyPropertyChangedFor(nameof(BoughtPropertyTokensViewIsVisible))]
        [NotifyPropertyChangedFor(nameof(ShowBuyMoreButtons))]
        private uint tokensBought = 0;
        public string TokensBoughtWorth => ((decimal)(TokensBought * Metadata?.Financials.PricePerToken ?? 0)).ToCurrencyString();

        public bool BoughtPropertyTokensViewIsVisible => TokensBought > 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TokensOwnedWorth))]
        [NotifyPropertyChangedFor(nameof(OwnedPropertyTokensViewIsVisible))]
        [NotifyPropertyChangedFor(nameof(RelistPropertyTokensButtonIsVisible))]
        [NotifyPropertyChangedFor(nameof(SendSharesButtonIsVisible))]
        private uint tokensOwned = 0;
        public string TokensOwnedWorth => ((decimal)(TokensOwned * Metadata?.Financials.PricePerToken ?? 0)).ToCurrencyString();

        public bool OwnedPropertyTokensViewIsVisible => TokensOwned > 0;
        public bool RelistPropertyTokensButtonIsVisible => TokensOwned > 0;
        public bool SendSharesButtonIsVisible => TokensOwned > 0;

        public bool ShowBuyMoreButtons => !NftWrapper!.ListingHasExpired && ListingDetails?.ListedTokens > 0 && TokensBought > 0;

        public string MainActionText => getMainActionState() switch
        {
            MainActionStates.Buy => DirectBuyIsOpen ? "Buy" : "Reserve",
            MainActionStates.ListingExpired => "Expired",
            MainActionStates.RefundBought => "Refund",
            MainActionStates.SoldOut => "Sold Out",
            MainActionStates.CanNotInvest => "You can not invest",
            MainActionStates.SpvToBeCreated => "Waiting for SPV to be created",
            MainActionStates.CreateSpv => "Create SPV",
            MainActionStates.Claim => "Claim",
            MainActionStates.ToBeClaimed => "Waiting for others to claim",
            MainActionStates.ClaimExpired => "Claim Expired",
            MainActionStates.RefundUnclaimed => "Refund",
            MainActionStates.RefundClaimed => "Refund",
            MainActionStates.Relist => "Relist",
            MainActionStates.MakeOffer => "Make offer",
            MainActionStates.Unknown => "Unknown",
            _ => "Unknown",
        };
        public ButtonStateEnum MainActionButtonState => getMainActionState() switch
        {
            MainActionStates.CanNotInvest => ButtonStateEnum.Disabled,
            MainActionStates.Buy => ButtonStateEnum.Enabled,
            MainActionStates.ListingExpired => ButtonStateEnum.Disabled,
            MainActionStates.RefundBought => ButtonStateEnum.Enabled,
            MainActionStates.SoldOut => ButtonStateEnum.Disabled,
            MainActionStates.SpvToBeCreated => ButtonStateEnum.Disabled,
            MainActionStates.CreateSpv => ButtonStateEnum.Enabled,
            MainActionStates.Claim => ButtonStateEnum.Enabled,
            MainActionStates.ToBeClaimed => ButtonStateEnum.Enabled,
            MainActionStates.ClaimExpired => ButtonStateEnum.Disabled,
            MainActionStates.RefundUnclaimed => ButtonStateEnum.Enabled,
            MainActionStates.RefundClaimed => ButtonStateEnum.Enabled,
            MainActionStates.Relist => ButtonStateEnum.Enabled,
            MainActionStates.MakeOffer => ButtonStateEnum.Enabled,
            MainActionStates.Unknown => ButtonStateEnum.Disabled,
            _ => ButtonStateEnum.Disabled,
        };

        public string StatusText => NftWrapper?.Status ?? "Unknown";
        public bool StatusIsVisible => NftWrapper?.StatusIsVisible ?? false;

        /// <summary>
        /// The marketplace listing id the Solana program calls are keyed by. ItemId
        /// carries it for Solana-sourced items - AssetId is the property asset's id, a
        /// different id space that only happens to coincide today.
        /// </summary>
        private long ListingId => NftWrapper?.NftBase is XcavateSolanaListingNft solanaListing
            ? solanaListing.ListingId
            : ListingDetails?.ItemId.Value ?? 0;

        /// <summary>
        /// The property asset id the secondary-market calls (relist_shares and its
        /// siblings) are keyed by. Solana-sourced items carry it untruncated on the NFT
        /// record; the Substrate-era fallback reads the listing details' U32.
        /// </summary>
        private long AssetId => NftWrapper?.NftBase is XcavateSolanaListingNft solanaListing
            ? solanaListing.AssetId
            : ListingDetails?.AssetId.Value ?? 0;

        /// <summary>
        /// True once this listing's claim window has closed: purchases then go through
        /// buy_property_shares (tokens delivered and paid for immediately) instead of
        /// reserve_shares, which the program rejects from that point on. False for
        /// Substrate-sourced items, whose flow is unmigrated.
        /// </summary>
        private bool DirectBuyIsOpen =>
            NftWrapper?.NftBase is XcavateSolanaListingNft solanaListing
            && solanaListing.DirectBuyIsOpen(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        private bool marketplaceTransactionsSubscribed;

        /// <summary>
        /// Listens for marketplace transaction confirmations while the page is on screen.
        /// Called from <c>PropertyDetailPage.OnAppearing</c>; the page's
        /// <c>OnDisappearing</c> calls <see cref="UnsubscribeFromMarketplaceTransactions"/>
        /// so the static event never keeps a popped page's view model alive.
        /// </summary>
        public void SubscribeToMarketplaceTransactions()
        {
            if (marketplaceTransactionsSubscribed)
            {
                return;
            }

            marketplaceTransactionsSubscribed = true;

            XcavateMarketplaceTransactionModel.TransactionConfirmed += OnMarketplaceTransactionConfirmed;
        }

        public void UnsubscribeFromMarketplaceTransactions()
        {
            if (!marketplaceTransactionsSubscribed)
            {
                return;
            }

            marketplaceTransactionsSubscribed = false;

            XcavateMarketplaceTransactionModel.TransactionConfirmed -= OnMarketplaceTransactionConfirmed;

            // A pending claim's navigation dies with the page.
            claimConfirmationPending = false;
        }

        // ── Governance voting (terms ratification, proposals, challenges, elections) ──

        /// <summary>
        /// The listing's live governance surface: the SPV-terms ratification vote during
        /// the legal phase and the letting seat's proposals / challenges / election
        /// afterwards, plus the connected wallet's share ledger (the voting power).
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SpvElectionSectionIsVisible))]
        [NotifyPropertyChangedFor(nameof(SpvElectionIsLive))]
        [NotifyPropertyChangedFor(nameof(SpvElectionClosedIsVisible))]
        [NotifyPropertyChangedFor(nameof(SpvTurnoutPercent))]
        [NotifyPropertyChangedFor(nameof(SpvNotVotedPercent))]
        [NotifyPropertyChangedFor(nameof(SpvTurnoutRatio))]
        [NotifyPropertyChangedFor(nameof(SpvTimeRemainingText))]
        [NotifyPropertyChangedFor(nameof(SpvCanVote))]
        [NotifyPropertyChangedFor(nameof(GovernanceVoteIsVisible))]
        [NotifyPropertyChangedFor(nameof(ProposalIsVisible))]
        [NotifyPropertyChangedFor(nameof(ChallengeIsVisible))]
        [NotifyPropertyChangedFor(nameof(ProposalTitleText))]
        [NotifyPropertyChangedFor(nameof(ProposalTalliesText))]
        [NotifyPropertyChangedFor(nameof(ProposalApprovePercent))]
        [NotifyPropertyChangedFor(nameof(ProposalApproveRatio))]
        [NotifyPropertyChangedFor(nameof(ProposalTimeRemainingText))]
        [NotifyPropertyChangedFor(nameof(ChallengeTitleText))]
        [NotifyPropertyChangedFor(nameof(ChallengeTalliesText))]
        [NotifyPropertyChangedFor(nameof(ChallengeApprovePercent))]
        [NotifyPropertyChangedFor(nameof(ChallengeApproveRatio))]
        [NotifyPropertyChangedFor(nameof(ChallengeTimeRemainingText))]
        [NotifyPropertyChangedFor(nameof(VotingPowerText))]
        [NotifyPropertyChangedFor(nameof(AgentCanVote))]
        [NotifyPropertyChangedFor(nameof(AgentVotePowerText))]
        [NotifyPropertyChangedFor(nameof(CloseEmptyPositionIsVisible))]
        private XcavateMarketplaceIndexerModel.XcavateGovernanceState? governance;

        /// <summary>The unix expiry of whichever vote the section shows, minus now.</summary>
        private static string TimeRemainingText(long expiry)
        {
            var remaining = TimeSpan.FromSeconds(expiry - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (remaining <= TimeSpan.Zero)
            {
                return "closed";
            }

            return remaining.TotalHours >= 24
                ? $"{(int)remaining.TotalDays}d {remaining.Hours}h left"
                : $"{(int)remaining.TotalHours}h {remaining.Minutes}m left";
        }

        private static int PercentOf(long part, long whole) =>
            whole <= 0 ? 0 : (int)Math.Round(part * 100.0 / whole);

        /// <summary>The listing is running an SPV-lawyer election round (the "terms" vote).</summary>
        public bool SpvElectionSectionIsVisible => Governance?.SpvElection is { } election && election.Expiry > 0;
        public bool SpvElectionIsLive => Governance?.SpvElection is { } election
            && election.Expiry > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        /// <summary>A closed (expired) round keeps the banner but drops the bar.</summary>
        public bool SpvElectionClosedIsVisible => SpvElectionSectionIsVisible && !SpvElectionIsLive;
        /// <summary>Share of the sold supply already backing a candidacy, capped at 100.</summary>
        public int SpvTurnoutPercent => PercentOf(
            Math.Min(Governance?.SpvElection?.TotalVotePower ?? 0, Governance?.SpvElection?.SoldShareAmount ?? 0),
            Governance?.SpvElection?.SoldShareAmount ?? 0);
        public int SpvNotVotedPercent => Governance?.SpvElection is { SoldShareAmount: > 0 } ? 100 - SpvTurnoutPercent : 0;
        public double SpvTurnoutRatio => SpvTurnoutPercent / 100.0;
        public string SpvTimeRemainingText => TimeRemainingText(Governance?.SpvElection?.Expiry ?? 0);

        /// <summary>
        /// The wallet may vote when it is an investor the indexer shows free shares for
        /// (owned, not listed, not already locked behind another vote) and a candidacy
        /// exists to back.
        /// </summary>
        public bool SpvCanVote => SpvElectionIsLive
            && Governance?.SpvElection?.LeadingCandidacy is not null
            && (Governance?.Holding?.Votable ?? 0) > 0;

        public bool GovernanceVoteIsVisible => Governance?.ActiveProposal is not null || Governance?.ActiveChallenge is not null;
        public bool ProposalIsVisible => Governance?.ActiveProposal is not null;
        public bool ChallengeIsVisible => Governance?.ActiveChallenge is not null;

        public string ProposalTitleText => $"Agent spending proposal #{Governance?.ActiveProposal?.ProposalId}";
        public string ProposalTalliesText =>
            $"Yes {Governance?.ActiveProposal?.TallyYes ?? 0} · No {Governance?.ActiveProposal?.TallyNo ?? 0} · Abstain {Governance?.ActiveProposal?.TallyAbstain ?? 0}";
        public int ProposalApprovePercent => PercentOf(
            Governance?.ActiveProposal?.TallyYes ?? 0,
            (Governance?.ActiveProposal?.TallyYes ?? 0) + (Governance?.ActiveProposal?.TallyNo ?? 0));
        public double ProposalApproveRatio => ProposalApprovePercent / 100.0;
        public string ProposalTimeRemainingText => TimeRemainingText(Governance?.ActiveProposal?.Expiry ?? 0);

        public string ChallengeTitleText => $"Challenge against the letting agent #{Governance?.ActiveChallenge?.ChallengeId}";
        public string ChallengeTalliesText =>
            $"Yes {Governance?.ActiveChallenge?.TallyYes ?? 0} · No {Governance?.ActiveChallenge?.TallyNo ?? 0} · Abstain {Governance?.ActiveChallenge?.TallyAbstain ?? 0}";
        public int ChallengeApprovePercent => PercentOf(
            Governance?.ActiveChallenge?.TallyYes ?? 0,
            (Governance?.ActiveChallenge?.TallyYes ?? 0) + (Governance?.ActiveChallenge?.TallyNo ?? 0));
        public double ChallengeApproveRatio => ChallengeApprovePercent / 100.0;
        public string ChallengeTimeRemainingText => TimeRemainingText(Governance?.ActiveChallenge?.Expiry ?? 0);

        public string VotingPowerText => Governance?.Holding is { } holding
            ? $"Voting power: {holding.Votable} shares"
            : "No voting power";

        /// <summary>
        /// Re-reads the listing's governance state from the indexer (the terms vote, the
        /// live proposal/challenge, the wallet's ledger). Runs on the same trigger as
        /// <see cref="RefreshListingAsync"/>: after any confirmed transaction and on the
        /// detail page's first load. Tolerant like the listing refresh - a failure keeps
        /// whatever the page already shows.
        /// </summary>
        public async Task RefreshVotingAsync(CancellationToken token)
        {
            if (NftWrapper?.NftBase is not XcavateSolanaListingNft solanaListing)
            {
                return;
            }

            try
            {
                var governanceState = await XcavateMarketplaceIndexerModel.GetGovernanceStateAsync(
                        SolanaNetworkModel.SelectedCluster,
                        solanaListing.ListingId,
                        ListingDetails?.AssetId ?? solanaListing.ListingId,
                        KeysModel.GetSolanaAddress(),
                        token)
                    .ConfigureAwait(false);

                token.ThrowIfCancellationRequested();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Governance = governanceState;
                });
            }
            catch (OperationCanceledException)
            {
                // The page went away mid-query.
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to refresh the governance state: ");
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Approving the SPV lawyer's terms = voting for their candidacy in the listing's
        /// SPV election (the only pre-settlement investor vote the program has; not voting
        /// is the on-chain "reject"). Opens the confirmation sheet; the submit rides the
        /// popup's ContinueRequested back to <see cref="ExecuteApproveSpvTermsAsync"/>.
        /// </summary>
        [RelayCommand]
        public void ApproveSpvTerms()
        {
            var popupViewModel = DependencyService.Get<CastVotePopupViewModel>();
            popupViewModel.VoteKind = "terms";
            popupViewModel.IsApprove = true;
            popupViewModel.ContinueRequested = ExecuteApproveSpvTermsAsync;
            popupViewModel.IsVisible = true;
        }

        private async Task ExecuteApproveSpvTermsAsync()
        {
            var election = Governance?.SpvElection;
            var candidacy = election?.LeadingCandidacy;
            if (election is null || candidacy is null)
            {
                return;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, CancellationToken.None))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            var amount = Governance?.Holding?.Votable ?? 0;
            if (amount < 1)
            {
                return;
            }

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                "Approve SPV terms",
                (voter, cluster, ct) => XcavateMarketplaceCallsModel.VoteOnSpvLawyerAsync(
                    cluster, voter, ListingId, election.Round, candidacy.Lawyer, amount, ct));
        }

        /// <summary>
        /// Yes/No on the letting seat's live spending proposal - same sheet, same flow.
        /// </summary>
        [RelayCommand]
        public void ApproveProposal() => VoteOnProposal(true);

        [RelayCommand]
        public void RejectProposal() => VoteOnProposal(false);

        private void VoteOnProposal(bool approve)
        {
            var popupViewModel = DependencyService.Get<CastVotePopupViewModel>();
            popupViewModel.VoteKind = "proposal";
            popupViewModel.IsApprove = approve;
            popupViewModel.ContinueRequested = () => ExecuteVoteOnProposalAsync(approve);
            popupViewModel.IsVisible = true;
        }

        private async Task ExecuteVoteOnProposalAsync(bool approve)
        {
            var proposal = Governance?.ActiveProposal;
            if (proposal is null)
            {
                return;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, CancellationToken.None))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            var amount = Governance?.Holding?.Votable ?? 0;
            if (amount < 1)
            {
                return;
            }

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                (approve ? "Vote Yes" : "Vote No") + " on proposal",
                (voter, cluster, ct) => XcavatePropertyCallsModel.VoteOnProposalAsync(
                    cluster, voter, ListingDetails!.AssetId, proposal.ProposalId,
                    approve ? XcavateVoteChoice.Yes : XcavateVoteChoice.No, amount, ct));
        }

        /// <summary>
        /// Yes/No on the live challenge against the letting agent - Yes backs the
        /// challenger (a slash + strike; three strikes remove the agent).
        /// </summary>
        [RelayCommand]
        public void ApproveChallenge() => VoteOnChallenge(true);

        [RelayCommand]
        public void RejectChallenge() => VoteOnChallenge(false);

        private void VoteOnChallenge(bool approve)
        {
            var popupViewModel = DependencyService.Get<CastVotePopupViewModel>();
            popupViewModel.VoteKind = "challenge";
            popupViewModel.IsApprove = approve;
            popupViewModel.ContinueRequested = () => ExecuteVoteOnChallengeAsync(approve);
            popupViewModel.IsVisible = true;
        }

        private async Task ExecuteVoteOnChallengeAsync(bool approve)
        {
            var challenge = Governance?.ActiveChallenge;
            if (challenge is null)
            {
                return;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, CancellationToken.None))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            var amount = Governance?.Holding?.Votable ?? 0;
            if (amount < 1)
            {
                return;
            }

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                (approve ? "Vote Yes" : "Vote No") + " on challenge",
                (voter, cluster, ct) => XcavatePropertyCallsModel.VoteOnChallengeAsync(
                    cluster, voter, ListingDetails!.AssetId, challenge.ChallengeId,
                    approve ? XcavateVoteChoice.Yes : XcavateVoteChoice.No, amount, ct));
        }

        // ── Agent election (the letting seat's plurality vote) ──

        /// <summary>
        /// The letting seat's election surface: the sitting agent, the live round with
        /// its candidacies, the connected wallet's own vote and any still-locked vote
        /// from a settled round. Null until the first successful indexer read - the
        /// section stays hidden until then (no empty-card flicker).
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AgentElectionSectionIsVisible))]
        [NotifyPropertyChangedFor(nameof(SittingAgentText))]
        [NotifyPropertyChangedFor(nameof(ElectionBlockIsVisible))]
        [NotifyPropertyChangedFor(nameof(AgentElectionTitleText))]
        [NotifyPropertyChangedFor(nameof(AgentElectionTimeRemainingText))]
        [NotifyPropertyChangedFor(nameof(AgentElectionIsLive))]
        [NotifyPropertyChangedFor(nameof(AgentCanVote))]
        [NotifyPropertyChangedFor(nameof(LockedAgentVoteRound))]
        [NotifyPropertyChangedFor(nameof(AgentUnlockIsVisible))]
        [NotifyPropertyChangedFor(nameof(FinalizeAgentElectionIsVisible))]
        private XcavateMarketplaceIndexerModel.AgentElectionState? agentElection;

        /// <summary>The current round's candidacy rows, rebuilt on every election refresh.</summary>
        public ObservableCollection<AgentCandidacyRowModel> AgentCandidacyRows { get; } = [];

        public bool AgentElectionSectionIsVisible => AgentElection is not null;

        /// <summary>The sitting agent's truncated pubkey, or the vacant-seat copy.</summary>
        public string SittingAgentText => XcavateMarketplaceIndexerModel.IsVacantSeat(AgentElection?.Agent)
            ? "No agent appointed"
            : TruncateAddress(AgentElection!.Agent!);

        /// <summary>The election block shows while the current round has candidacies.</summary>
        public bool ElectionBlockIsVisible => AgentElection is { CandidateCount: > 0 };

        public string AgentElectionTitleText => $"Agent election — round {AgentElection?.Round}";

        public string AgentElectionTimeRemainingText => TimeRemainingText(AgentElection?.Expiry ?? 0);

        public bool AgentElectionIsLive => AgentElection is { } election
            && election.Expiry > DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>
        /// The wallet may back a candidacy while the round runs and the governance read
        /// shows free (owned, unlisted, unlocked) shares - the full votable amount is
        /// what the vote locks, same as the proposal/challenge executors.
        /// </summary>
        public bool AgentCanVote => AgentElectionIsLive && (Governance?.Holding?.Votable ?? 0) > 0;

        public string AgentVotePowerText => Governance?.Holding is { } holding
            ? $"Your voting power: {holding.Votable} tokens"
            : "No voting power";

        /// <summary>
        /// The ended election round the wallet's shares are still locked by, when one
        /// exists: a past round's open vote record (<see cref="XcavateMarketplaceIndexerModel.AgentElectionState.MyLockedRound"/>),
        /// or the current round's own vote once its expiry has passed.
        /// </summary>
        public long? LockedAgentVoteRound
        {
            get
            {
                if (AgentElection is not { } election)
                {
                    return null;
                }

                if (election.MyLockedRound is { } lockedRound)
                {
                    return lockedRound;
                }

                return election.MyVote is not null
                    && election.Expiry > 0
                    && election.Expiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                        ? election.Round
                        : null;
            }
        }

        public bool AgentUnlockIsVisible => LockedAgentVoteRound is not null;

        /// <summary>The ended, unfinalized round is crankable by anyone.</summary>
        public bool FinalizeAgentElectionIsVisible => AgentElection is { CandidateCount: > 0 } election
            && election.Expiry > 0
            && election.Expiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>The app's pubkey truncation idiom (SolanaTokenDetailPageViewModel): first6…last4.</summary>
        private static string TruncateAddress(string address) =>
            address.Length > 10 ? $"{address[..6]}…{address[^4..]}" : address;

        /// <summary>
        /// Re-reads the letting seat's election state from the indexer and rebuilds the
        /// section's candidacy rows. Runs on the same trigger as
        /// <see cref="RefreshVotingAsync"/> and additionally from the page's
        /// <c>OnAppearing</c>. Tolerant like the listing refresh - a failure keeps
        /// whatever the page already shows.
        /// </summary>
        public async Task RefreshAgentElectionAsync(CancellationToken token)
        {
            if (NftWrapper?.NftBase is not XcavateSolanaListingNft solanaListing)
            {
                return;
            }

            try
            {
                var electionState = await XcavateMarketplaceIndexerModel.GetAgentElectionStateAsync(
                        SolanaNetworkModel.SelectedCluster,
                        solanaListing.AssetId,
                        // Empty voter reads the election without the own-vote section.
                        KeysModel.GetSolanaAddress() ?? "",
                        token)
                    .ConfigureAwait(false);

                token.ThrowIfCancellationRequested();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AgentElection = electionState;

                    AgentCandidacyRows.Clear();
                    foreach (var candidacy in electionState.Candidacies)
                    {
                        var agent = candidacy.Agent;

                        AgentCandidacyRows.Add(new AgentCandidacyRowModel(
                            agent,
                            TruncateAddress(agent),
                            $"{candidacy.VotePower} votes",
                            electionState.MyVote?.Candidate == agent,
                            AgentCanVote ? ButtonStateEnum.Enabled : ButtonStateEnum.Disabled,
                            new RelayCommand(() => VoteForAgent(agent))));
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // The page went away mid-query.
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to refresh the agent election state: ");
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Backing a candidacy (or moving the vote to another one) opens the
        /// confirmation sheet with the candidate's identity and the voter's power; the
        /// submit rides the popup's ContinueRequested back to
        /// <see cref="ExecuteVoteOnAgentAsync"/>. A revote's previous-candidacy
        /// resolution happens inside the call wrapper, not here.
        /// </summary>
        private void VoteForAgent(string agent)
        {
            var popupViewModel = DependencyService.Get<CastVotePopupViewModel>();
            popupViewModel.VoteKind = "agent";
            popupViewModel.CandidateIdentity = TruncateAddress(agent);
            popupViewModel.VotePower = Governance?.Holding?.Votable ?? 0;
            popupViewModel.ContinueRequested = () => ExecuteVoteOnAgentAsync(agent);
            popupViewModel.IsVisible = true;
        }

        private async Task ExecuteVoteOnAgentAsync(string agent)
        {
            var election = AgentElection;
            if (election is null)
            {
                return;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, CancellationToken.None))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            var amount = Governance?.Holding?.Votable ?? 0;
            if (amount < 1)
            {
                return;
            }

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                "Vote for letting agent",
                (voter, cluster, ct) => XcavatePropertyCallsModel.VoteOnAgentAsync(
                    cluster, voter, AssetId, (ulong)election.Round, agent, amount, ct));
        }

        /// <summary>
        /// One-tap release of the shares a vote in an ENDED election round still locks;
        /// they return to the wallet's transferable balance. Single signer - the voter.
        /// </summary>
        [RelayCommand]
        public async Task UnlockAgentVotesAsync()
        {
            if (LockedAgentVoteRound is not { } lockedRound)
            {
                return;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, CancellationToken.None))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                "Unlock election votes",
                (voter, cluster, ct) => XcavatePropertyCallsModel.UnlockAgentVotesAsync(
                    cluster, voter, AssetId, (ulong)lockedRound, ct));
        }

        /// <summary>
        /// The permissionless crank seating the ended round's plurality winner. Anyone
        /// can send it, so the investor-role gate the vote executors apply does NOT
        /// apply here - only the wallet requirements.
        /// </summary>
        [RelayCommand]
        public async Task FinalizeAgentElectionAsync()
        {
            if (!FinalizeAgentElectionIsVisible)
            {
                return;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                "Finalize agent election",
                (cranker, cluster, ct) => XcavatePropertyCallsModel.FinalizeAgentElectionAsync(
                    cluster, cranker, AssetId, ct));
        }

        // ── Lawyers (the legal-phase document reviewers) ──

        /// <summary>The listing colors' literal forms, mirroring the DefaultStyles.xaml
        /// resource keys of the same names (the view model cannot reach StaticResources).</summary>
        private static readonly Color PrimaryColor = Color.FromArgb("#1d70a2");
        private static readonly Color PositiveColor = Color.FromArgb("#FF008000");
        private static readonly Color DangerousRedColor = Color.FromArgb("#eb4747");
        private static readonly Color Gray500Color = Color.FromArgb("#6E6E6E");

        /// <summary>The Solana listing the lawyers section reads, null for Substrate-sourced items.</summary>
        private XcavateSolanaListingNft? SolanaListing => NftWrapper?.NftBase as XcavateSolanaListingNft;

        /// <summary>
        /// The lawyers carry the legal phase - there is nothing to show before the sale
        /// sells out (the program only engages them from SOLD_OUT on) and no reason to
        /// hide them after: the verdicts stay readable through the settled and
        /// wound-down states.
        /// </summary>
        public bool LawyersSectionIsVisible => SolanaListing is { } listing && listing.InLegalPhase;

        /// <summary>The developer lawyer's truncated pubkey, or the vacant-seat copy.</summary>
        public string DeveloperLawyerText =>
            XcavateMarketplaceIndexerModel.IsVacantSeat(SolanaListing?.DeveloperLawyer)
                ? "No lawyer engaged"
                : TruncateAddress(SolanaListing!.DeveloperLawyer);

        /// <summary>The engaged developer lawyer's document verdict as a status pill.</summary>
        public bool DeveloperLawyerStatusIsVisible =>
            !XcavateMarketplaceIndexerModel.IsVacantSeat(SolanaListing?.DeveloperLawyer);

        public string DeveloperLawyerStatusText =>
            LawyerStatusText(SolanaListing?.DeveloperLawyerDocStatus ?? DocumentStatus.Pending);

        public Color DeveloperLawyerStatusColor =>
            LawyerStatusColor(SolanaListing?.DeveloperLawyerDocStatus ?? DocumentStatus.Pending);

        /// <summary>The SPV lawyer's truncated pubkey, or the vacant-seat copy.</summary>
        public string SpvLawyerText =>
            XcavateMarketplaceIndexerModel.IsVacantSeat(SolanaListing?.SpvLawyer)
                ? "No lawyer engaged"
                : TruncateAddress(SolanaListing!.SpvLawyer);

        /// <summary>The engaged SPV lawyer's document verdict as a status pill.</summary>
        public bool SpvLawyerStatusIsVisible =>
            !XcavateMarketplaceIndexerModel.IsVacantSeat(SolanaListing?.SpvLawyer);

        public string SpvLawyerStatusText =>
            LawyerStatusText(SolanaListing?.SpvLawyerDocStatus ?? DocumentStatus.Pending);

        public Color SpvLawyerStatusColor =>
            LawyerStatusColor(SolanaListing?.SpvLawyerDocStatus ?? DocumentStatus.Pending);

        private static string LawyerStatusText(DocumentStatus status) => status switch
        {
            DocumentStatus.Approved => "Documents approved",
            DocumentStatus.Rejected => "Documents rejected",
            _ => "Reviewing documents",
        };

        private static Color LawyerStatusColor(DocumentStatus status) => status switch
        {
            DocumentStatus.Approved => PositiveColor,
            DocumentStatus.Rejected => DangerousRedColor,
            _ => PrimaryColor,
        };

        // ── Role-aware bottom action (the Settings main-page role) ──

        /// <summary>
        /// The investor lifecycle bar: shown for the Investor main-page role and for
        /// LettingAgent, which has no detail-page actions of its own and keeps the
        /// investor flow (the same fallback <see cref="MainPageRoleOptions.ResolveRoute"/>
        /// applies to the shell).
        /// </summary>
        public bool ShowInvestorMainAction =>
            IsLoaded && MainPageRole is MainPageRole.Investor or MainPageRole.LettingAgent;

        /// <summary>
        /// The developer's engagement action: assign_developer_lawyer opens once the sale
        /// is SOLD_OUT while the developer-lawyer seat is still vacant. The program
        /// enforces the remaining preconditions (the caller owning the listing, the
        /// lawyer's region and compliance) on chain.
        /// </summary>
        public bool ShowAssignDeveloperLawyerButton =>
            IsLoaded && MainPageRole == MainPageRole.Developer
            && SolanaListing is { ListingStatus: "SoldOut" } listing
            && XcavateMarketplaceIndexerModel.IsVacantSeat(listing.DeveloperLawyer);

        /// <summary>
        /// The lawyer's claim action: claim_spv_case opens for a compliant Lawyer-role
        /// wallet on a SOLD_OUT listing whose SPV exists while the SPV-lawyer seat is
        /// vacant and the wallet is not already the developer-side lawyer. The program
        /// additionally enforces the region match and the legal deadline on chain.
        /// </summary>
        public bool ShowClaimSpvCaseButton =>
            IsLoaded && MainPageRole == MainPageRole.Lawyer
            && SolanaListing is { ListingStatus: "SoldOut" } listing
            && SpvCreated
            && XcavateMarketplaceIndexerModel.IsVacantSeat(listing.SpvLawyer)
            && !string.Equals(KeysModel.GetSolanaAddress(), listing.DeveloperLawyer, StringComparison.Ordinal)
            && (Roles?.Contains(XcavateRole.Lawyer) ?? false);

        /// <summary>
        /// True once the wallet's own candidacy stands in the round a new claim would
        /// enter - the claim button then reads "Claim submitted" and stays disabled.
        /// Refreshed by <see cref="RefreshLawyerClaimStateAsync"/>.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LawyerClaimText))]
        [NotifyPropertyChangedFor(nameof(LawyerClaimButtonState))]
        private bool ownCandidacyStanding = false;

        public string LawyerClaimText => OwnCandidacyStanding ? "Claim submitted" : "Claim";

        public ButtonStateEnum LawyerClaimButtonState =>
            OwnCandidacyStanding ? ButtonStateEnum.Disabled : ButtonStateEnum.Enabled;

        /// <summary>
        /// The election round a new candidacy stands in: while no round is running (the
        /// election's expiry is 0 - or no election has ever opened) the first claim opens
        /// the next round; while a round runs the candidacy joins the current one. Read
        /// fresh at build time - a stale round fails the candidacy PDA's seed check.
        /// </summary>
        private ulong ClaimRound => Governance?.SpvElection is { } election && election.Expiry > 0
            ? election.Round
            : (Governance?.SpvElection?.Round ?? 0) + 1;

        /// <summary>
        /// Probes the chain for the wallet's own candidacy in the round a new claim would
        /// enter, so a lawyer who already claimed sees "Claim submitted" instead of a
        /// second (doomed) submission. Tolerant like <see cref="RefreshVotingAsync"/> - a
        /// failed read keeps whatever the page already shows.
        /// </summary>
        public async Task RefreshLawyerClaimStateAsync(CancellationToken token)
        {
            if (MainPageRole != MainPageRole.Lawyer
                || NftWrapper?.NftBase is not XcavateSolanaListingNft solanaListing
                || !ShowClaimSpvCaseButton)
            {
                return;
            }

            try
            {
                var cluster = SolanaNetworkModel.SelectedCluster;
                var wallet = KeysModel.GetSolanaAddress();

                if (string.IsNullOrEmpty(wallet) || !XcavateDeploymentModel.IsDeployed(cluster))
                {
                    return;
                }

                var programs = XcavateProgramAddresses.Require(cluster);

                var candidacy = XcavateMarketplaceProgram.DeriveLawyerCandidacy(
                    programs, (ulong)solanaListing.ListingId, ClaimRound, new Solnet.Wallet.PublicKey(wallet));

                // A missing account is a successful null answer; an RPC failure throws.
                var account = await SolanaRpcModel.GetAccountInfoAsync(cluster, candidacy.Key, token)
                    .ConfigureAwait(false);

                token.ThrowIfCancellationRequested();

                await MainThread.InvokeOnMainThreadAsync(() => OwnCandidacyStanding = account is not null);
            }
            catch (OperationCanceledException)
            {
                // The page went away mid-query.
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to refresh the lawyer claim state: ");
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Opens the developer-lawyer assignment sheet; the submit rides the popup's
        /// ContinueRequested back to <see cref="ExecuteAssignDeveloperLawyerAsync"/>.
        /// </summary>
        [RelayCommand]
        public void AssignDeveloperLawyer()
        {
            if (!ShowAssignDeveloperLawyerButton)
            {
                return;
            }

            var popupViewModel = DependencyService.Get<AssignDeveloperLawyerPopupViewModel>();

            popupViewModel.SetToDefault();
            popupViewModel.ContinueRequested = ExecuteAssignDeveloperLawyerAsync;
            popupViewModel.IsVisible = true;
        }

        private async Task ExecuteAssignDeveloperLawyerAsync()
        {
            var popupViewModel = DependencyService.Get<AssignDeveloperLawyerPopupViewModel>();
            var lawyerAddress = popupViewModel.LawyerAddress;

            if (!SolanaAddressValidator.IsValidAddress(lawyerAddress))
            {
                return;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            try
            {
                if (!await RequirementsModel.CheckRequirementsAsync())
                {
                    return;
                }

                if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateDeveloper, CancellationToken.None))
                {
                    return;
                }
            }
            finally
            {
                // Cleared on every exit path - a throwing requirements check (network or
                // indexer failure) must not wedge the page behind the overlay.
                fullPageLoadingViewModel.IsVisible = false;
            }

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                "Assign developer lawyer",
                (developer, cluster, ct) => XcavateMarketplaceCallsModel.AssignDeveloperLawyerAsync(
                    cluster, developer, ListingId, lawyerAddress, ct));
        }

        /// <summary>
        /// Opens the SPV-case claim sheet with the seats' live status and the wallet's
        /// published terms document preloaded; the submit rides the popup's
        /// ContinueRequested back to <see cref="ExecuteClaimSpvCaseAsync"/>.
        /// </summary>
        [RelayCommand]
        public async Task ClaimSpvCaseAsync()
        {
            if (!ShowClaimSpvCaseButton || OwnCandidacyStanding)
            {
                return;
            }

            var popupViewModel = DependencyService.Get<ClaimSpvCasePopupViewModel>();

            popupViewModel.SetToDefault();
            popupViewModel.DeveloperLawyerStatusText =
                XcavateMarketplaceIndexerModel.IsVacantSeat(SolanaListing?.DeveloperLawyer)
                    ? "Available"
                    : "Assigned";
            popupViewModel.ContinueRequested = ExecuteClaimSpvCaseAsync;
            popupViewModel.IsVisible = true;

            var wallet = KeysModel.GetSolanaAddress();

            if (!string.IsNullOrEmpty(wallet))
            {
                await popupViewModel.LoadTermsAsync(wallet, CancellationToken.None);
            }
        }

        /// <summary>
        /// True between the claim's submission and its confirmation: the confirmation
        /// (and only it) navigates to the claim-submitted page. Set from
        /// <see cref="XcavateMarketplaceTransactionModel.SubmitAsync"/>'s result, so a
        /// declined unlock prompt or a failed build never wedges it - a later unrelated
        /// confirmation must not report a claim that was never signed.
        /// </summary>
        private bool claimConfirmationPending;

        private async Task ExecuteClaimSpvCaseAsync()
        {
            var popupViewModel = DependencyService.Get<ClaimSpvCasePopupViewModel>();

            // Validated and converted by the popup's Continue; null means the sheet
            // closed without a usable quote and nothing may be submitted.
            if (popupViewModel.CostsQuote is not { } costsQuote)
            {
                return;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            try
            {
                if (!await RequirementsModel.CheckRequirementsAsync())
                {
                    return;
                }

                if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.Lawyer, CancellationToken.None))
                {
                    return;
                }
            }
            finally
            {
                // Cleared on every exit path - a throwing requirements check (network or
                // indexer failure) must not wedge the page behind the overlay.
                fullPageLoadingViewModel.IsVisible = false;
            }

            var round = ClaimRound;

            claimConfirmationPending = await XcavateMarketplaceTransactionModel.SubmitAsync(
                "Claim SPV case",
                (lawyer, cluster, ct) => XcavateMarketplaceCallsModel.ClaimSpvCaseAsync(
                    cluster, lawyer, ListingId, round, costsQuote, ct));
        }

        /// <summary>
        /// After a confirmed claim transaction, the lawyer lands on the claim-submitted
        /// page. Runs inside the page's regular refresh handler: any other confirmed
        /// marketplace transaction just refreshes, as before.
        /// </summary>
        private async Task OnClaimConfirmedNavigateAsync()
        {
            claimConfirmationPending = false;

            await NavigationModel.PushAsync(new ClaimSubmittedPage());
        }

        // ── Pending finalizations (the permissionless cranks) ──

        /// <summary>
        /// Which finalization cranks this listing/property is ready for right now, from
        /// ONE indexer read per refresh (<see cref="RefreshPendingFinalizationsAsync"/>);
        /// null until the first successful read, so every banner stays hidden until
        /// loaded. The agent-election arrow is deliberately NOT surfaced here - the
        /// letting-agent section hosts that finalize button off its own election read.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FinalizeProposalIsVisible))]
        [NotifyPropertyChangedFor(nameof(FinalizeChallengeIsVisible))]
        [NotifyPropertyChangedFor(nameof(FinalizeSpvElectionIsVisible))]
        [NotifyPropertyChangedFor(nameof(ResolveSilentVerdictIsVisible))]
        [NotifyPropertyChangedFor(nameof(ExecuteDealIsVisible))]
        private XcavateMarketplaceIndexerModel.PendingFinalizations? pendingFinalizations;

        /// <summary>True while any crank submission is being built, signed or submitted.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CrankButtonState))]
        private bool crankRunning;

        public bool FinalizeProposalIsVisible => PendingFinalizations?.Proposal is not null;
        public bool FinalizeChallengeIsVisible => PendingFinalizations?.Challenge is not null;
        public bool FinalizeSpvElectionIsVisible => PendingFinalizations?.SpvElection is not null;
        public bool ResolveSilentVerdictIsVisible => PendingFinalizations?.SilentVerdictOverdue == true;
        public bool ExecuteDealIsVisible => PendingFinalizations?.ExecuteDeal is not null;

        /// <summary>Enabled while no crank is in flight - one submission at a time.</summary>
        public ButtonStateEnum CrankButtonState =>
            CrankRunning ? ButtonStateEnum.Disabled : ButtonStateEnum.Enabled;

        /// <summary>
        /// Re-reads which finalization cranks are due from the indexer (one
        /// GetPendingFinalizationsAsync per refresh). Runs on the same trigger as
        /// <see cref="RefreshVotingAsync"/> and additionally from the page's
        /// <c>OnAppearing</c>. Tolerant like the listing refresh - a failure keeps
        /// whatever the page already shows.
        /// </summary>
        public async Task RefreshPendingFinalizationsAsync(CancellationToken token)
        {
            if (NftWrapper?.NftBase is not XcavateSolanaListingNft solanaListing)
            {
                return;
            }

            try
            {
                var pending = await XcavateMarketplaceIndexerModel.GetPendingFinalizationsAsync(
                        SolanaNetworkModel.SelectedCluster,
                        solanaListing.AssetId,
                        solanaListing.ListingId,
                        KeysModel.GetSolanaAddress(),
                        token)
                    .ConfigureAwait(false);

                token.ThrowIfCancellationRequested();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    PendingFinalizations = pending;
                });
            }
            catch (OperationCanceledException)
            {
                // The page went away mid-query.
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to refresh the pending finalizations: ");
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// The shared crank executor: the wallet-requirements gate behind the loading
        /// overlay (cleared on every exit path), then the submission through the
        /// marketplace pipeline's status popup. The cranks are permissionless, so the
        /// investor-role check the vote executors apply does NOT apply here - the same
        /// shape as <see cref="FinalizeAgentElectionAsync"/>. <see cref="CrankRunning"/>
        /// keeps every banner's button disabled until the submission is in.
        /// </summary>
        private async Task RunCrankAsync(
            string description,
            Func<string, SolanaCluster, CancellationToken, Task<List<Solnet.Rpc.Models.TransactionInstruction>>> buildInstructionsAsync)
        {
            if (CrankRunning)
            {
                return;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            try
            {
                if (!await RequirementsModel.CheckRequirementsAsync())
                {
                    return;
                }
            }
            finally
            {
                // Cleared on every exit path - a throwing requirements check (network or
                // indexer failure) must not wedge the page behind the overlay.
                fullPageLoadingViewModel.IsVisible = false;
            }

            CrankRunning = true;

            try
            {
                await XcavateMarketplaceTransactionModel.SubmitAsync(description, buildInstructionsAsync);
            }
            finally
            {
                CrankRunning = false;
            }
        }

        /// <summary>The property name the crank descriptions report with.</summary>
        private string CrankPropertyName => Metadata?.PropertyName ?? "this property";

        /// <summary>finalize_proposal: settles the letting seat's ended spending proposal.</summary>
        [RelayCommand]
        public Task FinalizeProposalAsync() => RunCrankAsync(
            $"Finalize proposal on {CrankPropertyName}",
            (cranker, cluster, ct) => XcavatePropertyCallsModel.FinalizeProposalAsync(
                cluster, cranker, AssetId, ct));

        /// <summary>finalize_challenge: settles the ended challenge's slash/versus payouts.</summary>
        [RelayCommand]
        public Task FinalizeChallengeAsync() => RunCrankAsync(
            $"Finalize challenge on {CrankPropertyName}",
            (cranker, cluster, ct) => XcavatePropertyCallsModel.FinalizeChallengeAsync(
                cluster, cranker, AssetId, ct));

        /// <summary>finalize_spv_election: seats the ended SPV-lawyer election's winner.</summary>
        [RelayCommand]
        public Task FinalizeSpvElectionAsync() => RunCrankAsync(
            $"Finalize SPV election on {CrankPropertyName}",
            (cranker, cluster, ct) => XcavateMarketplaceCallsModel.FinalizeSpvElectionAsync(
                cluster, cranker, ListingId, ct));

        /// <summary>resolve_silent_verdict: rules on the developer's lawyer who let the review window run out.</summary>
        [RelayCommand]
        public Task ResolveSilentVerdictAsync() => RunCrankAsync(
            $"Resolve silent verdict on {CrankPropertyName}",
            (cranker, cluster, ct) => XcavateMarketplaceCallsModel.ResolveSilentVerdictAsync(
                cluster, cranker, ListingId, ct));

        /// <summary>execute_deal: settles the sale for every investor once both lawyers approved.</summary>
        [RelayCommand]
        public Task ExecuteDealAsync() => RunCrankAsync(
            $"Complete settlement for {CrankPropertyName}",
            (cranker, cluster, ct) => XcavateMarketplaceCallsModel.ExecuteDealAsync(
                cluster, cranker, ListingId, ct));

        /// <summary>
        /// True only when the indexer still shows this wallet's ShareHolding row with
        /// nothing in it - the row goes inactive when the account closes (the schema's
        /// <c>active</c>/<c>closedAtSlot</c> pair), so an active zero-amount row means
        /// the emptied account genuinely still exists on chain: exactly what
        /// close_share_holding reclaims. A wallet that never held (null row) sees nothing.
        /// </summary>
        public bool CloseEmptyPositionIsVisible =>
            Governance?.Holding is { Amount: 0, Listed: 0, MaxLock: 0 };

        /// <summary>
        /// One tap closing the emptied position: any final banked rent claims first
        /// (claim_income, with close_income_checkpoint riding inside when the checkpoint
        /// drains), then close_share_holding reclaims the holding account itself - one
        /// transaction, at most two signatures (the rent collector joins only the claim
        /// path, exactly like <see cref="ClaimRentAsync"/>).
        /// </summary>
        [RelayCommand]
        public Task CloseEmptyPositionAsync() => RunCrankAsync(
            $"Close empty position on {CrankPropertyName}",
            (owner, cluster, ct) => XcavatePropertyCallsModel.CloseEmptyPositionAsync(
                cluster, owner, AssetId, ct));

        // ── Rent income (claim_income) ──

        /// <summary>Mint decimals by (cluster, mint), resolved once per page visit.</summary>
        private readonly Dictionary<(SolanaCluster, string), int> incomeMintDecimals = [];

        /// <summary>The wallet's share count at the moment <see cref="IncomeState"/> was read.</summary>
        private ulong incomeShares;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IncomeSectionIsVisible))]
        private XcavateMarketplaceIndexerModel.PropertyIncomeState? incomeState;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ClaimRentButtonState))]
        private bool incomeClaimRunning;

        /// <summary>Per-mint claimable amounts, display units with the payment token's symbol.</summary>
        public ObservableCollection<string> ClaimableRentLines { get; } = [];

        /// <summary>Per-mint lifetime totals distributed per token (the streams' accumulators).</summary>
        public ObservableCollection<string> LifetimeRentLines { get; } = [];

        /// <summary>
        /// The income section exists for holders only: a Solana listing with income
        /// streams AND a wallet that holds shares or still has rent banked to pull.
        /// </summary>
        public bool IncomeSectionIsVisible =>
            IncomeState is { Streams.Count: > 0 } && (incomeShares > 0 || ClaimableRentLines.Count > 0);

        public bool ClaimableRentIsVisible => ClaimableRentLines.Count > 0;

        /// <summary>Enabled iff there is rent to claim and no claim is in flight.</summary>
        public ButtonStateEnum ClaimRentButtonState =>
            !IncomeClaimRunning && ClaimableRentLines.Count > 0
                ? ButtonStateEnum.Enabled
                : ButtonStateEnum.Disabled;

        /// <summary>
        /// Re-reads this property's income ledger (streams plus the wallet's checkpoint)
        /// and rebuilds the income section's figures. Runs on the same trigger as
        /// <see cref="RefreshVotingAsync"/> and additionally from the page's
        /// <c>OnAppearing</c>. Tolerant like the listing refresh - a failure keeps
        /// whatever the page already shows.
        /// </summary>
        public async Task RefreshIncomeAsync(CancellationToken token)
        {
            if (NftWrapper?.NftBase is not XcavateSolanaListingNft solanaListing)
            {
                return;
            }

            try
            {
                var cluster = SolanaNetworkModel.SelectedCluster;
                var owner = KeysModel.GetSolanaAddress();

                if (string.IsNullOrEmpty(owner) || !XcavateDeploymentModel.IsDeployed(cluster))
                {
                    return;
                }

                var income = await XcavateMarketplaceIndexerModel.GetIncomeStateAsync(
                        cluster, solanaListing.AssetId, owner, SolanaRpcModel.GetClient(cluster), token)
                    .ConfigureAwait(false);

                // The share count the claimable formula settles against: the holding from
                // the governance read this page already makes, falling back to the
                // listing figure. claim_income re-reads it at build time either way.
                var shares = Governance?.Holding?.Amount ?? TokensOwned;

                var claimableLines = new List<string>();
                var lifetimeLines = new List<string>();

                foreach (var stream in income.Streams)
                {
                    var decimals = await ResolveIncomeMintDecimalsAsync(cluster, stream.Mint, token)
                        .ConfigureAwait(false);
                    var symbol = IncomeMintSymbol(cluster, stream.Mint);

                    var claimable = income.GetClaimable(stream.Mint, shares);
                    if (claimable.Sign > 0)
                    {
                        claimableLines.Add($"{XcavateReserveBalanceModel.Format(XcavateMarketplaceIndexerModel.PropertyIncomeState.ToDisplayAmount(claimable, decimals))} {symbol}");
                    }

                    lifetimeLines.Add($"{XcavateReserveBalanceModel.Format(XcavateMarketplaceIndexerModel.PropertyIncomeState.ToDisplayAmount(stream.PerShare, decimals))} {symbol} per token");
                }

                token.ThrowIfCancellationRequested();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IncomeState = income;
                    incomeShares = shares;

                    ClaimableRentLines.Clear();
                    foreach (var line in claimableLines)
                    {
                        ClaimableRentLines.Add(line);
                    }

                    LifetimeRentLines.Clear();
                    foreach (var line in lifetimeLines)
                    {
                        LifetimeRentLines.Add(line);
                    }

                    OnPropertyChanged(nameof(IncomeSectionIsVisible));
                    OnPropertyChanged(nameof(ClaimableRentIsVisible));
                    OnPropertyChanged(nameof(ClaimRentButtonState));
                });
            }
            catch (OperationCanceledException)
            {
                // The page went away mid-query.
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to refresh the income state: ");
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// The mint's decimals for the claimable display: the whitelist entry when the
        /// app knows the mint, else the mint account on chain - the same resolution
        /// <see cref="XcavateMarketplaceCallsModel"/> applies to payment mints.
        /// </summary>
        private async Task<int> ResolveIncomeMintDecimalsAsync(SolanaCluster cluster, string mint, CancellationToken token)
        {
            if (incomeMintDecimals.TryGetValue((cluster, mint), out var decimals))
            {
                return decimals;
            }

            var (_, resolved) = await XcavateMarketplaceCallsModel.ResolveMintAsync(
                cluster, new Solnet.Wallet.PublicKey(mint), token).ConfigureAwait(false);

            incomeMintDecimals[(cluster, mint)] = resolved;

            return resolved;
        }

        /// <summary>
        /// The whitelist symbol the buy flow prints payment amounts with; an income mint
        /// the app does not know displays as plain "tokens" rather than a base58 address.
        /// </summary>
        private static string IncomeMintSymbol(SolanaCluster cluster, string mint) =>
            SolanaTokenWhitelist.ForCluster(cluster)
                .FirstOrDefault(entry => entry.Mint == mint)?.Symbol ?? "tokens";

        /// <summary>
        /// Claims every mint the wallet can pull from this property's income ledger (one
        /// claim_income per mint; close_income_checkpoint rides inside when the
        /// checkpoint drains - never a separate action). Same requirement gates as the
        /// page's other investor actions; the pipeline's confirmation event refreshes
        /// the section.
        /// </summary>
        [RelayCommand]
        public async Task ClaimRentAsync()
        {
            if (IncomeClaimRunning
                || ClaimableRentLines.Count == 0
                || NftWrapper?.NftBase is not XcavateSolanaListingNft)
            {
                return;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            try
            {
                if (!await RequirementsModel.CheckRequirementsAsync())
                {
                    return;
                }

                if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, CancellationToken.None))
                {
                    return;
                }
            }
            finally
            {
                // Cleared on every exit path - a throwing requirements check (network or
                // indexer failure) must not wedge the page behind the overlay.
                fullPageLoadingViewModel.IsVisible = false;
            }

            IncomeClaimRunning = true;

            try
            {
                await ClaimRentModel.ClaimAsync(
                    [new ClaimRentModel.ClaimTarget(AssetId, Metadata?.PropertyName ?? "this property")]);
            }
            finally
            {
                IncomeClaimRunning = false;
            }
        }

        /// <summary>
        /// A confirmed marketplace transaction (a reserve, buy or claim) changed this
        /// listing's on-chain state, so the figures on screen - tokens still available,
        /// this wallet's reserved tokens, the action button - are the pre-transaction
        /// ones and must be re-read. The offers namespace is now created automatically by
        /// the backend when the indexer sees a claim or relist (backend-driven offers namespace
        /// creation, see Indexer ADR-42), so the mobile app no longer prompts for a second
        /// signature here.
        /// </summary>
        private void OnMarketplaceTransactionConfirmed(object? sender, MarketplaceTransactionConfirmedEventArgs e) =>
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await RefreshListingAsync(CancellationToken.None);

                // The claim's confirmation lands the lawyer on the claim-submitted page,
                // after the refresh so the page behind it already shows the new state.
                if (claimConfirmationPending)
                {
                    claimConfirmationPending = false;

                    await OnClaimConfirmedNavigateAsync();
                }
            });

        /// <summary>
        /// Re-reads this listing from the Xcavate indexer and re-applies it to the page.
        /// Mirrors what <c>NavigateToPropertyDetailPageAsync</c> does on first load - the
        /// wrapper is rebuilt with the same code path, so expiry and countdown states are
        /// recomputed from the fresh data instead of patched onto the stale wrapper. A
        /// failed re-read keeps whatever the page already shows: stale beats an empty page.
        /// </summary>
        public async Task RefreshListingAsync(CancellationToken token)
        {
            if (NftWrapper?.NftBase is not XcavateSolanaListingNft solanaListing)
            {
                // Substrate-sourced listings have no Solana indexer to re-read.
                return;
            }

            try
            {
                var solanaAddress = KeysModel.GetSolanaAddress();

                var freshListing = await XcavateMarketplaceIndexerModel.GetListingFullInfoAsync(
                        SolanaNetworkModel.SelectedCluster, solanaListing.ListingId, solanaAddress, token)
                    .ConfigureAwait(false);

                if (freshListing is null)
                {
                    return;
                }

                var freshWrapper = await XcavatePropertyModel.ToXcavateNftWrapperAsync(freshListing, token)
                    .ConfigureAwait(false);

                token.ThrowIfCancellationRequested();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    NftWrapper = freshWrapper;
                    Metadata = ((INftXcavateMetadata)freshListing).XcavateMetadata;
                    ListingDetails = ((INftXcavateOngoingObjectListing)freshListing).OngoingObjectListingDetails;
                    TokensBought = freshWrapper.TokensBought;
                    TokensOwned = freshWrapper.TokensOwned;
                    SpvCreated = freshWrapper.SpvCreated;
                });

                // The vote sections ride the same refresh: the listing figures and the
                // governance tallies always come from the same moment.
                await RefreshVotingAsync(token).ConfigureAwait(false);

                // The agent election comes after the governance read so the candidacy
                // rows' vote buttons are gated on the fresh votable figure.
                await RefreshAgentElectionAsync(token).ConfigureAwait(false);

                // The finalization-crank banners ride the same refresh: a confirmed
                // crank (or a vote closing) is exactly what makes an arrow appear or
                // disappear.
                await RefreshPendingFinalizationsAsync(token).ConfigureAwait(false);

                // So does the income section: a confirmed claim (or a rent distribution)
                // changes exactly these figures.
                await RefreshIncomeAsync(token).ConfigureAwait(false);

                // And the lawyer's own-candidacy state: a confirmed claim flips the
                // claim button to its submitted state.
                await RefreshLawyerClaimStateAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The page went away mid-query.
            }
            catch (Exception ex)
            {
                // Keep the current data - the same tolerance the first load applies when
                // the indexer cannot be reached.
                Console.WriteLine("Failed to refresh the Solana listing: ");
                Console.WriteLine(ex);
            }
        }

        [RelayCommand]
        public Task MainActionAsync()
        {
            switch (getMainActionState())
            {
                case MainActionStates.Buy:
                    return BuyAsync();

                case MainActionStates.CreateSpv:
                    return CreateSpvAsync();

                case MainActionStates.Claim:
                    return ClaimAsync();

                case MainActionStates.RefundBought:
                    return RefundBoughtAsync();

                case MainActionStates.RefundUnclaimed:
                    return RefundUnclaimedAsync();

                case MainActionStates.RefundClaimed:
                    return RefundClaimedAsync();

                case MainActionStates.Relist:
                    return RelistAsync();

                case MainActionStates.MakeOffer:
                    return MakeOfferAsync();

            }

            return Task.FromResult(0);
        }

        public async Task BuyAsync()
        {
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, CancellationToken.None))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            var viewModel = DependencyService.Get<BuyPropertyTokensViewModel>();

            viewModel.ListingDetails = ListingDetails;
            viewModel.Metadata = Metadata;
            viewModel.IsVisible = true;
            viewModel.EndpointKey = PlutoFrameworkCore.NftModel.GetEndpointKey(NftWrapper!.NftBase.Type);
            viewModel.DirectBuyIsOpen = DirectBuyIsOpen;
        }

        public async Task CreateSpvAsync()
        {
            var token = CancellationToken.None;
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.SpvConfirmation, token))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                "Create SPV",
                (confirmer, cluster, ct) => XcavateMarketplaceCallsModel.CreateSpvAsync(cluster, confirmer, ListingId, ct));
        }

        public async Task ClaimAsync()
        {
            var token = CancellationToken.None;
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, token))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                "Claim property tokens",
                (investor, cluster, ct) => XcavateMarketplaceCallsModel.ClaimSharesAsync(cluster, investor, ListingId, ct));
        }

        public async Task RefundBoughtAsync()
        {
            var token = CancellationToken.None;
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, token))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                "Refund property tokens",
                (investor, cluster, ct) => XcavateMarketplaceCallsModel.WithdrawExpiredAsync(cluster, investor, ListingId, ct));
        }

        public async Task RefundUnclaimedAsync()
        {
            var token = CancellationToken.None;
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, token))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                "Refund property tokens",
                BuildClaimPhaseRefundAsync);
        }

        public async Task RefundClaimedAsync()
        {
            var token = CancellationToken.None;
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, token))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            await XcavateMarketplaceTransactionModel.SubmitAsync(
                "Refund property tokens",
                BuildClaimPhaseRefundAsync);
        }

        /// <summary>
        /// The claim-phase refund instruction keys off the listing's exact on-chain
        /// status, not on what the caller holds: withdraw_cancelled takes only a
        /// cancelled listing, while a refunding one - the legal deadline blew out and
        /// exits already started - still goes through withdraw_legal_process_expired,
        /// the same instruction that moved it into refunding with the first withdrawal.
        /// </summary>
        private Task<List<Solnet.Rpc.Models.TransactionInstruction>> BuildClaimPhaseRefundAsync(
            string investor, SolanaCluster cluster, CancellationToken ct)
        {
            var isCancelled = (NftWrapper?.NftBase as XcavateSolanaListingNft)?.IsCancelled == true;

            return isCancelled
                ? XcavateMarketplaceCallsModel.WithdrawCancelledAsync(cluster, investor, ListingId, ct)
                : XcavateMarketplaceCallsModel.WithdrawLegalProcessExpiredAsync(cluster, investor, ListingId, ct);
        }

        [RelayCommand]
        public async Task RelistAsync()
        {
            // The marketplace program takes relist_shares only on a Finalized primary
            // listing and refuses every other status with PropertyNotFinalized (6073) -
            // say that in words rather than spending a wallet prompt on a transaction
            // that must fail. Solana-sourced listings carry the status; anything older
            // falls through to the chain's own rejection.
            if (SolanaListing is { IsFinalized: false } unfinalized)
            {
                var messagePopup = DependencyService.Get<MessagePopupViewModel>();

                messagePopup.Title = "Sale not settled yet";
                messagePopup.Text = $"These tokens can be listed for resale once the property's sale has completed its legal process. This property's status is \"{unfinalized.ListingStatus}\" - refresh the page once it shows \"Finalized\".";
                messagePopup.IsVisible = true;

                return;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;

                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            var viewModel = DependencyService.Get<RelistPropertyTokensViewModel>();

            viewModel.ListingDetails = ListingDetails;
            viewModel.Metadata = Metadata;
            viewModel.IsVisible = true;
            viewModel.EndpointKey = PlutoFrameworkCore.NftModel.GetEndpointKey(NftWrapper!.NftBase.Type);
            viewModel.TokensOwned = TokensOwned;
            viewModel.AssetId = (ulong)AssetId;
        }

        /// <summary>
        /// The "Send shares" entry: opens the peer-to-peer transfer sheet keyed by a fresh
        /// governance-state read (the same call <see cref="RefreshVotingAsync"/> makes), so
        /// the sheet's max sendable amount reflects the live locks and listed-for-sale
        /// amount, not just the page's TokensOwned. Requirements are checked up front like
        /// <see cref="RelistAsync"/>; a failed or empty holding read falls back to the
        /// governance snapshot the page already shows, then to not opening at all.
        /// </summary>
        [RelayCommand]
        public async Task SendSharesAsync()
        {
            if (TokensOwned == 0 || NftWrapper?.NftBase is not XcavateSolanaListingNft solanaListing)
            {
                return;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;

                return;
            }

            XcavateMarketplaceIndexerModel.XcavateGovernanceState.HoldingState? holding = null;

            try
            {
                var token = CancellationToken.None;

                holding = (await XcavateMarketplaceIndexerModel.GetGovernanceStateAsync(
                        SolanaNetworkModel.SelectedCluster,
                        solanaListing.ListingId,
                        ListingDetails?.AssetId ?? solanaListing.ListingId,
                        KeysModel.GetSolanaAddress(),
                        token)
                    .ConfigureAwait(false))
                    .Holding;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to refresh the holding before opening the send sheet: ");
                Console.WriteLine(ex);
            }

            // The chain enforces the true sendable amount regardless; the page's last
            // indexer read is still better than a button that appears to do nothing.
            holding ??= Governance?.Holding;

            fullPageLoadingViewModel.IsVisible = false;

            if (holding is not { } currentHolding)
            {
                return;
            }

            DependencyService.Get<SendSharesPopupViewModel>()
                .SetContext(currentHolding.Amount, currentHolding.MaxLock, currentHolding.Listed, (ulong)AssetId);
        }

        /// <summary>
        /// The secondary-market counterpart of <see cref="BuyAsync"/>: the wallet is not
        /// the seller of the listing this page was opened from, so instead of a purchase
        /// form it gets the offer form - the price the buyer would pay per token
        /// (prefilled with the ask) and the amount, which the popup turns into the offer
        /// bucket the negotiation lives in.
        /// </summary>
        [RelayCommand]
        public async Task MakeOfferAsync()
        {
            if (ShareListingContext is null)
            {
                return;
            }

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;

                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, CancellationToken.None))
            {
                fullPageLoadingViewModel.IsVisible = false;

                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            var viewModel = DependencyService.Get<MakeOfferPopupViewModel>();

            viewModel.Metadata = Metadata;
            viewModel.ShareListingContext = ShareListingContext;
            viewModel.PricePerToken = XcavateQuoteFormatter
                .QuoteToPounds(ShareListingContext.SharePriceQuote)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            viewModel.IsVisible = true;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FavouriteImage))]
        private bool favourite = false;

        public ImageSource FavouriteImage => new FontImageSource
        {
            Color = (Color)Application.Current!.Resources["Primary"],
            FontFamily = "FontAwesome",
            Size = 25,
            Glyph = Favourite ? "\uf004" : "\uf08a",
            FontAutoScalingEnabled = false
        };



        [RelayCommand]
        public async Task MakeFavouriteAsync()
        {
            Favourite = !Favourite;

            await XcavatePropertyDatabase.SavePropertyAsync(new NftWrapper
            {
                Endpoint = Endpoint!,
                NftBase = NftWrapper!.NftBase,
                Favourite = Favourite
            });

            UpdateFavouritePropertiesModel.UpdateFavourite((INftXcavateBase)NftWrapper.NftBase, Favourite);
        }

        [RelayCommand]
        public Task ShareAsync() => Share.RequestAsync(new ShareTextRequest
        {
            Uri = $"https://app.realxmarket.io/marketplace/{ListingDetails?.AssetId}",
            Title = $"Share {Metadata?.PropertyName}",
        });

        [RelayCommand]
        public Task NavigateToFeesAsync() => Shell.Current.Navigation.PushAsync(new ExtensionWebViewPage("https://app.realxmarket.io/property-info-fees"));

        [RelayCommand]
        public async Task CancelReservationAsync()
        {
            var cancelPopupViewModel = DependencyService.Get<CancelReservationPopupViewModel>();
            cancelPopupViewModel.ContinueRequested = ExecuteCancelReservationAsync;
            cancelPopupViewModel.IsVisible = true;
        }

        private async Task ExecuteCancelReservationAsync()
        {
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, CancellationToken.None))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            // unreserve_shares is the Solana successor to the pallet's
            // cancel_property_purchase: it releases the investor's reservation.
            await XcavateMarketplaceTransactionModel.SubmitAsync(
                "Cancel reservation",
                (investor, cluster, ct) => XcavateMarketplaceCallsModel.CancelReservationAsync(cluster, investor, ListingId, ct));
        }

        [RelayCommand]
        public async Task BuyMoreAsync()
        {
            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            if (!await RequirementsModel.CheckRequirementsAsync())
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            if (!await RequirementsModel.CheckXcavateRoleAsync(XcavateRole.RealEstateInvestor, CancellationToken.None))
            {
                fullPageLoadingViewModel.IsVisible = false;
                return;
            }

            fullPageLoadingViewModel.IsVisible = false;

            var viewModel = DependencyService.Get<BuyPropertyTokensViewModel>();

            viewModel.ListingDetails = ListingDetails;
            viewModel.Metadata = Metadata;
            viewModel.IsVisible = true;
            viewModel.EndpointKey = PlutoFrameworkCore.NftModel.GetEndpointKey(NftWrapper!.NftBase.Type);
            viewModel.DirectBuyIsOpen = DirectBuyIsOpen;
        }

        /// <summary>
        /// Opens the messenger on the namespace the profile API created for this property
        /// (keyed by the marketplace listing id). A property whose namespace is not indexed
        /// yet - or any lookup failure - falls back to the generic bucket dashboard.
        /// </summary>
        [RelayCommand]
        public async Task MessageAsync()
        {
            long? namespaceId = null;

            try
            {
                namespaceId = await PropertyNamespaceClient.GetNamespaceIdByPropertyIdAsync(ListingId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to resolve the property's messaging namespace: " + ex);
            }

            // The messenger gate (MessengerAccessModel) raises NoAccountPopup or the
            // create/import X25519 popup instead of opening the page when the account
            // is not ready.
            await MessengerAccessModel.TryOpenMessagesAsync(namespaceId is null
                ? null
                : string.Format(NamespaceUrlFormat, namespaceId));
        }
    }
}
