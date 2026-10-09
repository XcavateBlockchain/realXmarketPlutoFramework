using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Components.Loading;
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
using UniqueryPlus.Metadata;
using UniqueryPlus.Nfts;
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
        private HashSet<XcavateRole>? roles = null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MainActionButtonState))]
        [NotifyPropertyChangedFor(nameof(MainActionText))]
        private bool spvCreated;

        [ObservableProperty]
        private Endpoint? endpoint;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(StatusIsVisible))]
        [NotifyPropertyChangedFor(nameof(MainActionButtonState))]
        [NotifyPropertyChangedFor(nameof(MainActionText))]
        [NotifyPropertyChangedFor(nameof(ShowBuyMoreButtons))]
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
        private uint tokensOwned = 0;
        public string TokensOwnedWorth => ((decimal)(TokensOwned * Metadata?.Financials.PricePerToken ?? 0)).ToCurrencyString();

        public bool OwnedPropertyTokensViewIsVisible => TokensOwned > 0;
        public bool RelistPropertyTokensButtonIsVisible => TokensOwned > 0;

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

        /// <summary>
        /// A confirmed marketplace transaction (a reserve, buy or claim) changed this
        /// listing's on-chain state, so the figures on screen - tokens still available,
        /// this wallet's reserved tokens, the action button - are the pre-transaction
        /// ones and must be re-read.
        /// </summary>
        private void OnMarketplaceTransactionConfirmed(object? sender, EventArgs e) =>
            MainThread.BeginInvokeOnMainThread(() => _ = RefreshListingAsync(CancellationToken.None));

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
