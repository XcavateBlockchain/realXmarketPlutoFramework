using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Alerts;
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
        LegalProcessExpired,
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
            if (NftWrapper is null)
            {
                return MainActionStates.Unknown;
            }

            if (LegalProcessHasExpired)
            {
                if (TokensOwned > 0)
                {
                    return MainActionStates.RefundClaimed;
                }

                if (TokensBought > 0)
                {
                    return MainActionStates.RefundUnclaimed;
                }

                return MainActionStates.LegalProcessExpired;
            }

            if (NftWrapper.ListingHasExpired && ListingDetails?.ListedTokens > 0 && TokensBought > 0)
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
        [NotifyPropertyChangedFor(nameof(ShowDeveloperListButton))]
        [NotifyPropertyChangedFor(nameof(ShowCompleteListingButton))]
        [NotifyPropertyChangedFor(nameof(ShowAdminDecidedText))]
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

        // ── Admin review of a pending property ──

        /// <summary>
        /// The off-chain developer property record this page reviews when it was opened from
        /// the pending-properties list. Set means admin-review mode: the property has nothing
        /// on chain yet, so the page reads the record alone - the indexer refreshes stay off,
        /// the on-chain sections hide, and the bottom bar is the admin's Reject / Approve
        /// pair instead of any marketplace action.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPendingReview))]
        [NotifyPropertyChangedFor(nameof(ShowOnChainSections))]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(StatusIsVisible))]
        [NotifyPropertyChangedFor(nameof(ShowInvestorMainAction))]
        [NotifyPropertyChangedFor(nameof(ShowBuyMoreButtons))]
        [NotifyPropertyChangedFor(nameof(ShowAdminDecisionButtons))]
        [NotifyPropertyChangedFor(nameof(ShowAdminDecidedText))]
        [NotifyPropertyChangedFor(nameof(AdminDecidedText))]
        [NotifyPropertyChangedFor(nameof(AdminDecidedColor))]
        [NotifyPropertyChangedFor(nameof(ShowDeveloperListButton))]
        [NotifyPropertyChangedFor(nameof(ShowCompleteListingButton))]
        private DeveloperProperty? pendingProperty;

        public bool IsPendingReview => PendingProperty is not null;

        /// <summary>The sections and chrome that only make sense for an on-chain listing.</summary>
        public bool ShowOnChainSections => !IsPendingReview;

        /// <summary>
        /// The admin-review entry point: the record is already in hand, so the page skips the
        /// skeleton - there is nothing to fetch.
        /// </summary>
        public static PropertyDetailViewModel ForPendingProperty(DeveloperProperty record) => new()
        {
            PendingProperty = record,
            Metadata = MapPendingProperty(record),
            IsLoading = false,
        };

        /// <summary>
        /// The record in the page's metadata shape: every field the page binds that the
        /// record actually carries; the indexer-only ones (gallery mirrors, map, the form's
        /// detail attributes) stay empty. The record's stored share price is a junk mid-form
        /// value (see docs/developer-page-dynamo-findings.md), so the per-token price is
        /// derived from the declared price and share count instead.
        /// </summary>
        private static PropertyMetadata MapPendingProperty(DeveloperProperty record) => new()
        {
            PropertyName = record.NameText,
            PropertyType = record.TypeText,
            PropertyDescription = string.Empty,
            Files = record.HasImage ? [record.ImageUrl!] : [],
            Address = new PropertyAddress
            {
                PostCode = record.PostCode,
                Street = record.Street ?? record.Location,
                TownCity = record.TownCity ?? record.Region,
            },
            Company = record.CompanyName is { } company
                ? new PropertyCompany { Name = company }
                : null,
            Financials = new PropertyFinancials
            {
                PropertyPrice = record.PropertyPrice ?? 0,
                NumberOfTokens = (int)(record.NumberOfShares ?? 0),
                EstimatedRentalIncome = record.EstimatedRentalIncome ?? 0,
                PricePerToken = record.PropertyPrice is { } price && record.NumberOfShares is > 0
                    ? price / record.NumberOfShares.Value
                    : 0,
            },
        };

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

        public bool ShowBuyMoreButtons => !IsPendingReview && !NftWrapper!.ListingHasExpired && ListingDetails?.ListedTokens > 0 && TokensBought > 0;

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
            MainActionStates.LegalProcessExpired => "Legal process expired",
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
            MainActionStates.LegalProcessExpired => ButtonStateEnum.Disabled,
            MainActionStates.Relist => ButtonStateEnum.Enabled,
            MainActionStates.MakeOffer => ButtonStateEnum.Enabled,
            MainActionStates.Unknown => ButtonStateEnum.Disabled,
            _ => ButtonStateEnum.Disabled,
        };

        public string StatusText => PendingProperty?.StatusText ?? NftWrapper?.Status ?? "Unknown";
        public bool StatusIsVisible => IsPendingReview || (NftWrapper?.StatusIsVisible ?? false);

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

            // So does a pending listing's record update.
            listConfirmationPending = false;
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

        private bool LegalProcessHasExpired =>
            SolanaListing?.LegalProcessHasExpired(DateTimeOffset.UtcNow.ToUnixTimeSeconds()) == true;

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
            !IsPendingReview && IsLoaded && MainPageRole is MainPageRole.Investor or MainPageRole.LettingAgent;

        /// <summary>
        /// The developer's engagement action: assign_developer_lawyer opens once the sale
        /// is SOLD_OUT, before the legal deadline, while the developer-lawyer seat is still
        /// vacant. The program enforces the remaining preconditions (the caller owning the
        /// listing, the lawyer's region and compliance) on chain.
        /// </summary>
        public bool ShowAssignDeveloperLawyerButton =>
            IsLoaded && MainPageRole == MainPageRole.Developer && !LegalProcessHasExpired
            && SolanaListing is { ListingStatus: "SoldOut" } listing
            && XcavateMarketplaceIndexerModel.IsVacantSeat(listing.DeveloperLawyer);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowClaimSpvCaseButton))]
        private bool lawyerRoleCompliant;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowClaimSpvCaseButton))]
        private ushort? lawyerRegionId;

        private SpvLawyerClaimEligibility.State? ClaimEligibilityState =>
            SolanaListing is { } listing
                ? new SpvLawyerClaimEligibility.State(
                    MainPageRole == MainPageRole.Lawyer,
                    LawyerRoleCompliant,
                    LawyerRegionId,
                    KeysModel.GetSolanaAddress() ?? string.Empty,
                    listing.ListingStatus,
                    SpvCreated,
                    listing.SpvLawyer,
                    listing.DeveloperLawyer,
                    listing.PropertyRegionId,
                    listing.LegalDeadlineTimestamp,
                    listing.SpvElectionExpiryTimestamp,
                    listing.SpvElectionCandidateCount)
                : null;

        public bool ShowClaimSpvCaseButton =>
            IsLoaded
            && ClaimEligibilityState is { } state
            && SpvLawyerClaimEligibility.CanDisplay(state, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        private bool CanSubmitClaim =>
            ClaimEligibilityState is { } state
            && SpvLawyerClaimEligibility.CanSubmit(
                state, OwnCandidacyStanding, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LawyerClaimText))]
        [NotifyPropertyChangedFor(nameof(LawyerClaimButtonState))]
        private bool ownCandidacyStanding = false;

        public string LawyerClaimText => OwnCandidacyStanding ? "Claim submitted" : "Claim";

        public ButtonStateEnum LawyerClaimButtonState =>
            OwnCandidacyStanding ? ButtonStateEnum.Disabled : ButtonStateEnum.Enabled;

        // ── The admin's decision on a pending property ──

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AdminDecisionButtonsState))]
        private bool adminDecisionSaving;

        /// <summary>The buttons belong to the awaiting-decision state alone.</summary>
        public bool ShowAdminDecisionButtons =>
            IsPendingReview && PendingProperty!.Status == DeveloperPropertyStatus.Submitted;

        /// <summary>
        /// The developer's own action on a decided record: list_property opens for the
        /// Developer main-page role while the record stands Verified - once the on-chain
        /// listing confirms, the record flips to Listed and the decided text takes over.
        /// </summary>
        public bool ShowDeveloperListButton =>
            IsPendingReview && MainPageRole == MainPageRole.Developer
            && PendingProperty is { Status: DeveloperPropertyStatus.Verified };

        public bool ShowAdminDecidedText =>
            IsPendingReview && !ShowDeveloperListButton && PendingProperty!.Status
                is DeveloperPropertyStatus.Verified or DeveloperPropertyStatus.Rejected
                or DeveloperPropertyStatus.Listed;

        public string AdminDecidedText =>
            PendingProperty?.Status switch
            {
                DeveloperPropertyStatus.Verified => "Approved",
                DeveloperPropertyStatus.Listed => "Listed",
                _ => "Rejected",
            };

        public Color AdminDecidedColor =>
            PendingProperty?.Status is DeveloperPropertyStatus.Verified or DeveloperPropertyStatus.Listed
                ? PrimaryColor
                : DangerousRedColor;

        public ButtonStateEnum AdminDecisionButtonsState =>
            AdminDecisionSaving ? ButtonStateEnum.Disabled : ButtonStateEnum.Enabled;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DeveloperListButtonState))]
        private bool developerListSaving;

        public ButtonStateEnum DeveloperListButtonState =>
            DeveloperListSaving ? ButtonStateEnum.Disabled : ButtonStateEnum.Enabled;

        /// <summary>
        /// Set by <see cref="RefreshListingAssetStateAsync"/>: the record is Listed but its
        /// on-chain listing still stands PENDING_ASSETS - an app version before
        /// list_property and init_property_assets were bundled left it half-open, and the
        /// marketplace feed never shows it. The Complete Listing button finishes it.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowCompleteListingButton))]
        private bool listingNeedsAssetSetup;

        /// <summary>
        /// The developer's recovery action on a half-opened listing; only the developer
        /// role sees it, and only while the on-chain state calls for it.
        /// </summary>
        public bool ShowCompleteListingButton =>
            IsPendingReview && MainPageRole == MainPageRole.Developer
            && PendingProperty is { Status: DeveloperPropertyStatus.Listed }
            && ListingNeedsAssetSetup;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CompleteListingButtonState))]
        private bool completeListingSaving;

        public ButtonStateEnum CompleteListingButtonState =>
            CompleteListingSaving ? ButtonStateEnum.Disabled : ButtonStateEnum.Enabled;

        [RelayCommand]
        private Task ApprovePendingPropertyAsync() => DecidePendingPropertyAsync(DeveloperPropertyStatus.Verified);

        [RelayCommand]
        private Task RejectPendingPropertyAsync() => DecidePendingPropertyAsync(DeveloperPropertyStatus.Rejected);

        /// <summary>
        /// The decision, written to the developer property store (Submitted → Verified or
        /// Rejected). The store answers with the record as it stands after the write - or as
        /// it stands, when another admin's decision beat this one - so the page settles on
        /// the truth either way. Null means the decision did not go through: the buttons
        /// stay, and a retry is a tap away.
        /// </summary>
        private async Task DecidePendingPropertyAsync(DeveloperPropertyStatus status)
        {
            if (AdminDecisionSaving || PendingProperty is null)
            {
                return;
            }

            AdminDecisionSaving = true;

            try
            {
                var updated = await DeveloperPropertyClient.UpdatePropertyStatusAsync(
                    PendingProperty.PropertyId, status, CancellationToken.None);

                if (updated is null)
                {
                    await Toast.Make("The property could not be updated. Please try again.").Show();
                    return;
                }

                PendingProperty = updated;
            }
            finally
            {
                AdminDecisionSaving = false;
            }
        }

        /// <summary>
        /// True between the listing's submission and its confirmation: the confirmation
        /// (and only it) marks the record Listed in the developer property store. Set from
        /// <see cref="XcavateMarketplaceTransactionModel.SubmitAsync"/>'s result together
        /// with the listing id the build reserved, so a declined unlock prompt or a failed
        /// build never wedges it - a later unrelated confirmation must not mark a record
        /// whose transaction was never signed.
        /// </summary>
        private bool listConfirmationPending;
        private ulong pendingListingId;

        /// <summary>
        /// The developer's "List on Marketplace" action: validates the record, loads the
        /// registered on-chain locations and opens the region picker; the submission itself
        /// rides the picker's callback to <see cref="ExecuteListPropertyAsync"/>. The
        /// record's own update waits for the on-chain confirmation
        /// (<see cref="OnMarketplaceTransactionConfirmed"/>), so the page never claims a
        /// listing the chain rejected.
        /// </summary>
        [RelayCommand]
        private async Task ListPropertyAsync()
        {
            if (!ShowDeveloperListButton || DeveloperListSaving || PendingProperty is not { } property)
            {
                return;
            }

            if (property.PropertyPrice is not > 0 || property.NumberOfShares is not > 0)
            {
                await Toast.Make("The property's price and share count must be set before it can be listed.").Show();
                return;
            }

            DeveloperListSaving = true;

            var fullPageLoadingViewModel = DependencyService.Get<FullPageLoadingViewModel>();

            fullPageLoadingViewModel.IsVisible = true;

            IReadOnlyList<(ushort RegionId, string Postcode)>? locations = null;

            try
            {
                locations = await XcavateMarketplaceCallsModel.GetRegisteredLocationsAsync(
                    SolanaNetworkModel.SelectedCluster, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load the registered regions: ");
                Console.WriteLine(ex);
            }
            finally
            {
                // Cleared on every exit path - a throwing read (network or indexer
                // failure) must not wedge the page behind the overlay.
                fullPageLoadingViewModel.IsVisible = false;
            }

            if (locations is null)
            {
                DeveloperListSaving = false;
                await Toast.Make("The registered regions could not be loaded. Please try again.").Show();
                return;
            }

            if (locations.Count == 0)
            {
                DeveloperListSaving = false;
                await Toast.Make("No regions are registered on chain yet.").Show();
                return;
            }

            var popupViewModel = DependencyService.Get<SelectListingRegionPopupViewModel>();

            // The saving flag spans the picker's lifetime: a selection hands it to
            // ExecuteListPropertyAsync's finally, a dismissal without one releases it
            // here - the button must not stay disabled over a closed popup, nor wake
            // up mid-submission (the popup closes AFTER its callback fires).
            var selectionMade = false;

            popupViewModel.PropertyChanged += OnRegionPopupPropertyChanged;

            popupViewModel.Open(locations, option =>
            {
                selectionMade = true;
                _ = ExecuteListPropertyAsync(option);
            });

            void OnRegionPopupPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(SelectListingRegionPopupViewModel.IsVisible) && !popupViewModel.IsVisible)
                {
                    popupViewModel.PropertyChanged -= OnRegionPopupPropertyChanged;

                    if (!selectionMade)
                    {
                        DeveloperListSaving = false;
                    }
                }
            }
        }

        /// <summary>
        /// The submission behind the region picker's callback: the requirements and
        /// developer-role checks run against the wallet, then list_property and
        /// init_property_assets go out in one atomic transaction for the picked
        /// (region, postcode) pair - the property's name and published JSON document URL
        /// become the share mint's on-chain metadata.
        /// </summary>
        private async Task ExecuteListPropertyAsync(SelectListingRegionOption option)
        {
            if (PendingProperty is not { } property)
            {
                DeveloperListSaving = false;
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

            try
            {
                var metadataUri = await DeveloperPropertyClient.UploadMetadataAsync(
                    property.PropertyId, CancellationToken.None);

                if (metadataUri is null)
                {
                    await Toast.Make("The property details could not be published. Please try again.").Show();
                    return;
                }

                ulong? listedId = null;

                var submitted = await XcavateMarketplaceTransactionModel.SubmitAsync(
                    "List property",
                    async (developer, cluster, ct) =>
                    {
                        var (instructions, listingId) = await XcavateMarketplaceCallsModel.ListPropertyAsync(
                            cluster,
                            developer,
                            option.RegionId,
                            option.Postcode,
                            property.PropertyPrice!.Value,
                            (long)property.NumberOfShares!.Value,
                            property.NameText,
                            metadataUri,
                            ct);

                        listedId = listingId;
                        return instructions;
                    });

                if (submitted && listedId is { } id)
                {
                    listConfirmationPending = true;
                    pendingListingId = id;
                }
            }
            finally
            {
                DeveloperListSaving = false;
            }
        }

        /// <summary>
        /// After a confirmed list_property transaction, the record flips to Listed in the
        /// developer property store. A failed update leaves the on-chain listing without
        /// its record link - the page says so rather than pretending the listing vanished.
        /// </summary>
        private async Task OnListConfirmedMarkListedAsync()
        {
            var updated = await DeveloperPropertyClient.MarkPropertyListedAsync(
                PendingProperty?.PropertyId ?? string.Empty, pendingListingId, CancellationToken.None);

            if (updated is null)
            {
                await Toast.Make("The property was listed on chain, but its record could not be updated.").Show();
                return;
            }

            PendingProperty = updated;
        }

        /// <summary>
        /// Re-reads the record's on-chain listing state: a Listed record whose listing
        /// still stands PENDING_ASSETS was opened by an app version that sent
        /// list_property without init_property_assets, and only the Complete Listing
        /// button can finish it. Tolerant like the listing refresh - a failed read keeps
        /// whatever the page already shows. No-op outside pending-review mode.
        /// </summary>
        public async Task RefreshListingAssetStateAsync(CancellationToken token)
        {
            if (!IsPendingReview || PendingProperty is not { Status: DeveloperPropertyStatus.Listed } property)
            {
                await MainThread.InvokeOnMainThreadAsync(() => ListingNeedsAssetSetup = false);
                return;
            }

            var cluster = SolanaNetworkModel.SelectedCluster;

            var needsSetup = false;

            if (XcavateDeploymentModel.IsDeployed(cluster)
                && long.TryParse(property.ListingId, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var listingId))
            {
                try
                {
                    var listing = await XcavateMarketplaceIndexerModel.GetListingFullInfoAsync(
                        cluster, listingId, null, token).ConfigureAwait(false);

                    // A null read (indexer lag) stays hidden rather than offering a
                    // completion the chain may not need.
                    needsSetup = listing?.ListingStatus == "PendingAssets";
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to read the listing's on-chain state: ");
                    Console.WriteLine(ex);
                }
            }

            await MainThread.InvokeOnMainThreadAsync(() => ListingNeedsAssetSetup = needsSetup);
        }

        /// <summary>
        /// The Complete Listing button's submission: init_property_assets alone, for the
        /// listing list_property already opened - the second half of the pair
        /// <see cref="ExecuteListPropertyAsync"/> now bundles into one transaction. No
        /// record update follows: the record is already Listed, and the confirmation's
        /// refresh hides the button once the listing leaves PENDING_ASSETS.
        /// </summary>
        [RelayCommand]
        private async Task CompleteListingAsync()
        {
            if (!ShowCompleteListingButton || CompleteListingSaving || PendingProperty is not { } property
                || !long.TryParse(property.ListingId, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var listingId))
            {
                return;
            }

            CompleteListingSaving = true;

            try
            {
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

                var metadataUri = await DeveloperPropertyClient.UploadMetadataAsync(
                    property.PropertyId, CancellationToken.None);

                if (metadataUri is null)
                {
                    await Toast.Make("The property details could not be published. Please try again.").Show();
                    return;
                }

                await XcavateMarketplaceTransactionModel.SubmitAsync(
                    "Complete listing",
                    (developer, cluster, ct) => XcavateMarketplaceCallsModel.InitPropertyAssetsAsync(
                        cluster, developer, listingId, property.NameText, metadataUri, ct));
            }
            finally
            {
                CompleteListingSaving = false;
            }
        }

        private long lawyerClaimRefreshVersion;

        private ulong ClaimRound => SpvLawyerClaimEligibility.GetClaimRound(
            SolanaListing?.SpvElectionRound ?? 0,
            SolanaListing?.SpvElectionExpiryTimestamp ?? 0);

        public async Task RefreshLawyerClaimStateAsync(CancellationToken token)
        {
            var refreshVersion = Interlocked.Increment(ref lawyerClaimRefreshVersion);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (refreshVersion != Interlocked.Read(ref lawyerClaimRefreshVersion))
                {
                    return;
                }

                LawyerRoleCompliant = false;
                LawyerRegionId = null;
                OwnCandidacyStanding = false;
            });

            if (MainPageRole != MainPageRole.Lawyer
                || NftWrapper?.NftBase is not XcavateSolanaListingNft solanaListing)
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

                var roleTask = WhitelistModel.HasRoleAsync(wallet, XcavateRole.Lawyer, cluster, token);
                var regionTask = XcavateMarketplaceIndexerModel.GetLawyerRegionAsync(cluster, wallet, token);
                var candidacyTask = SolanaRpcModel.GetAccountInfoAsync(cluster, candidacy.Key, token);

                await Task.WhenAll(roleTask, regionTask, candidacyTask).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (refreshVersion != Interlocked.Read(ref lawyerClaimRefreshVersion))
                    {
                        return;
                    }

                    LawyerRoleCompliant = roleTask.Result;
                    LawyerRegionId = regionTask.Result;
                    OwnCandidacyStanding = candidacyTask.Result is not null;
                });
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
                if (LegalProcessHasExpired)
                {
                    ShowLegalProcessExpired();
                }

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

            if (LegalProcessHasExpired)
            {
                popupViewModel.IsVisible = false;
                ShowLegalProcessExpired();
                return;
            }

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
            popupViewModel.CollectedFeeQuote = SolanaListing?.CollectedFeeQuote ?? 0;
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

                await RefreshListingAsync(CancellationToken.None);

                if (!CanSubmitClaim || costsQuote > (SolanaListing?.CollectedFeeQuote ?? 0))
                {
                    await Toast.Make("This SPV lawyer claim is no longer available.").Show();
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
        public bool ResolveSilentVerdictIsVisible =>
            !LegalProcessHasExpired && PendingFinalizations?.SilentVerdictOverdue == true;
        public bool ExecuteDealIsVisible =>
            !LegalProcessHasExpired && PendingFinalizations?.ExecuteDeal is not null;

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
        public Task ResolveSilentVerdictAsync()
        {
            if (LegalProcessHasExpired)
            {
                ShowLegalProcessExpired();
                return Task.CompletedTask;
            }

            return RunCrankAsync(
                $"Resolve silent verdict on {CrankPropertyName}",
                (cranker, cluster, ct) => XcavateMarketplaceCallsModel.ResolveSilentVerdictAsync(
                    cluster, cranker, ListingId, ct));
        }

        /// <summary>execute_deal: settles the sale for every investor once both lawyers approved.</summary>
        [RelayCommand]
        public Task ExecuteDealAsync()
        {
            if (LegalProcessHasExpired)
            {
                ShowLegalProcessExpired();
                return Task.CompletedTask;
            }

            return RunCrankAsync(
                $"Complete settlement for {CrankPropertyName}",
                (cranker, cluster, ct) => XcavateMarketplaceCallsModel.ExecuteDealAsync(
                    cluster, cranker, ListingId, ct));
        }

        private void ShowLegalProcessExpired()
        {
            var messagePopup = DependencyService.Get<MessagePopupViewModel>();
            messagePopup.Title = "Legal process expired";
            messagePopup.Text = "The legal review period for this property has ended. Settlement and lawyer actions are no longer available. Investors with reserved or owned tokens can use Refund instead.";
            messagePopup.IsVisible = true;

            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusIsVisible));
            OnPropertyChanged(nameof(MainActionText));
            OnPropertyChanged(nameof(MainActionButtonState));
            OnPropertyChanged(nameof(ShowAssignDeveloperLawyerButton));
            OnPropertyChanged(nameof(ShowClaimSpvCaseButton));
            OnPropertyChanged(nameof(ResolveSilentVerdictIsVisible));
            OnPropertyChanged(nameof(ExecuteDealIsVisible));
        }

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

                // In pending-review mode the refresh above reads nothing; this is where a
                // confirmed init_property_assets retires the Complete Listing button.
                await RefreshListingAssetStateAsync(CancellationToken.None);

                // The claim's confirmation lands the lawyer on the claim-submitted page,
                // after the refresh so the page behind it already shows the new state.
                if (claimConfirmationPending)
                {
                    claimConfirmationPending = false;

                    await OnClaimConfirmedNavigateAsync();
                }

                // The listing's confirmation flips the record to Listed. In pending-review
                // mode the refresh above reads nothing (there is no on-chain listing wrapper
                // yet), so this runs on its own.
                if (listConfirmationPending)
                {
                    listConfirmationPending = false;

                    await OnListConfirmedMarkListedAsync();
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
                    await MainThread.InvokeOnMainThreadAsync(() => LawyerRoleCompliant = false);
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
                await MainThread.InvokeOnMainThreadAsync(() => LawyerRoleCompliant = false);
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
                BuildRefundAsync);
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
                BuildRefundAsync);
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
                BuildRefundAsync);
        }

        /// <summary>
        /// Selects the withdrawal by the listing's exact on-chain status. Each wrapper
        /// also routes a reservation-only position to release_reservation.
        /// </summary>
        private Task<List<Solnet.Rpc.Models.TransactionInstruction>> BuildRefundAsync(
            string investor, SolanaCluster cluster, CancellationToken ct)
        {
            var listing = SolanaListing
                ?? throw new InvalidOperationException("A refund requires the Solana listing state.");

            return listing.RefundInstruction switch
            {
                XcavateRefundInstruction.Expired =>
                    XcavateMarketplaceCallsModel.WithdrawExpiredAsync(cluster, investor, ListingId, ct),
                XcavateRefundInstruction.Cancelled =>
                    XcavateMarketplaceCallsModel.WithdrawCancelledAsync(cluster, investor, ListingId, ct),
                XcavateRefundInstruction.LegalProcessExpired =>
                    XcavateMarketplaceCallsModel.WithdrawLegalProcessExpiredAsync(cluster, investor, ListingId, ct),
                _ => throw new InvalidOperationException("Unknown refund instruction."),
            };
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
