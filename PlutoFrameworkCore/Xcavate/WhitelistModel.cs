using PlutoFrameworkCore.Solana;
using StrawberryShake;
using XcavateDevnetIndexer;

namespace PlutoFramework.Model.Xcavate
{
    public enum XcavateRole
    {
        RegionalOperator = 0,
        RealEstateInvestor = 1,
        RealEstateDeveloper = 2,
        Lawyer = 3,
        LettingAgent = 4,
        SpvConfirmation = 5,
        // Not in the Solana whitelist program's Role enum, so nothing below can currently be
        // granted. Kept because the module programs are still to come and the badge view
        // already renders them; treat them as "never held" rather than as roles to check.
        ModuleCreator = 6,
        ModuleSponsor = 7,
        ModuleBooker = 8,
        ModuleDeliverer = 9,
        ModuleAIAgent = 10,
        ModuleRecipient = 11,
    }

    /// <summary>
    /// Reads Xcavate roles from the whitelist Solana program, through the Xcavate indexer.
    /// <para>
    /// Addresses here are Solana addresses (base58), not Substrate ones. The whitelist moved
    /// off the XcavatePaseo pallet, so a wallet's Substrate key says nothing about its roles.
    /// </para>
    /// </summary>
    public class WhitelistModel
    {
        /// <summary>
        /// The cluster whose whitelist program roles are read from.
        /// <para>
        /// Devnet, and deliberately not the user's selected Solana network: the Xcavate
        /// programs are only deployed on devnet, so following the network picker would leave
        /// every mainnet user with no roles and no explanation. Once
        /// <see cref="XcavateWhitelistIndexer.MainnetUrl"/> is real, this is the one line to
        /// change - to a per-call cluster from the caller, or to mainnet outright.
        /// </para>
        /// </summary>
        public const SolanaCluster WhitelistCluster = SolanaCluster.Devnet;

        /// <summary>
        /// Enough to hold every role a wallet can possibly have: the program keys assignments
        /// by (user, role) - which is why the indexer's <c>roleAssignment</c> returns a single
        /// row - and its Role enum has six members. So one page is always the whole answer,
        /// and there is no paging loop below that could ever run a second iteration.
        /// </summary>
        private const int RolePageSize = 6;

        private static HashSet<XcavateRole>? roles = null;
        private static string? rolesAddress = null;
        private static SolanaCluster? rolesCluster = null;

        public static void Clear()
        {
            roles = null;
            rolesAddress = null;
            rolesCluster = null;
        }

        public static Task<HashSet<XcavateRole>> GetRolesCachedAsync(string address, CancellationToken token)
            => GetRolesCachedAsync(address, WhitelistCluster, token);

        /// <summary>
        /// The cached roles when they belong to this exact address and cluster, otherwise a
        /// fresh query. An empty result is never served from cache: a user who has just been
        /// whitelisted would otherwise stay locked out for the rest of the session.
        /// </summary>
        public static Task<HashSet<XcavateRole>> GetRolesCachedAsync(string address, SolanaCluster cluster, CancellationToken token)
        {
            if (roles is not null && roles.Count > 0 && rolesAddress == address && rolesCluster == cluster)
            {
                return Task.FromResult(roles);
            }

            return GetRolesAsync(address, cluster, token);
        }

        public static Task<HashSet<XcavateRole>> GetRolesAsync(string address, CancellationToken token)
            => GetRolesAsync(address, WhitelistCluster, token);

        /// <summary>
        /// Every role the whitelist program currently grants <paramref name="address"/>.
        /// Removed, renounced and revoked assignments are filtered out by the query, so every
        /// role returned is one the program will actually honour.
        /// </summary>
        public static async Task<HashSet<XcavateRole>> GetRolesAsync(string address, SolanaCluster cluster, CancellationToken token)
        {
            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var result = await client.UserRoles
                .ExecuteAsync(address, RolePageSize, 0, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            var assignments = result.Data?.RoleAssignments.Nodes;

            var found = new HashSet<XcavateRole>();

            foreach (var assignment in assignments ?? [])
            {
                if (FromIndexerRole(assignment.Role) is XcavateRole role)
                {
                    found.Add(role);
                }
            }

            roles = found;
            rolesAddress = address;
            rolesCluster = cluster;

            return found;
        }

        public static Task<bool> HasRoleAsync(string address, XcavateRole role, CancellationToken token)
            => HasRoleAsync(address, role, WhitelistCluster, token);

        /// <summary>
        /// Whether <paramref name="address"/> holds <paramref name="role"/>, asked of the
        /// indexer directly instead of fetching every assignment and filtering here.
        /// </summary>
        public static async Task<bool> HasRoleAsync(string address, XcavateRole role, SolanaCluster cluster, CancellationToken token)
        {
            // A role the whitelist program does not know about cannot have been granted.
            if (ToIndexerRole(role) is not Role indexerRole)
            {
                return false;
            }

            var client = XcavateWhitelistIndexer.GetClient(cluster);

            var result = await client.UserHasRole
                .ExecuteAsync(address, indexerRole, token)
                .ConfigureAwait(false);

            result.EnsureNoErrors();

            // Compliant, not HasRole: an assignment whose permission was revoked still exists,
            // but the program rejects its holder's instructions.
            return result.Data?.CheckAccess.Compliant ?? false;
        }

        private static XcavateRole? FromIndexerRole(Role role) => role switch
        {
            Role.RegionalOperator => XcavateRole.RegionalOperator,
            Role.RealEstateInvestor => XcavateRole.RealEstateInvestor,
            Role.RealEstateDeveloper => XcavateRole.RealEstateDeveloper,
            Role.Lawyer => XcavateRole.Lawyer,
            Role.LettingAgent => XcavateRole.LettingAgent,
            Role.SpvConfirmation => XcavateRole.SpvConfirmation,
            _ => null,
        };

        private static Role? ToIndexerRole(XcavateRole role) => role switch
        {
            XcavateRole.RegionalOperator => Role.RegionalOperator,
            XcavateRole.RealEstateInvestor => Role.RealEstateInvestor,
            XcavateRole.RealEstateDeveloper => Role.RealEstateDeveloper,
            XcavateRole.Lawyer => Role.Lawyer,
            XcavateRole.LettingAgent => Role.LettingAgent,
            XcavateRole.SpvConfirmation => Role.SpvConfirmation,
            _ => null,
        };
    }
}
