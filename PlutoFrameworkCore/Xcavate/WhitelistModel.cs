using Substrate.NetApi;
using Substrate.NetApi.Model.Types.Base;
using XcavatePaseo.NetApi.Generated;
using XcavatePaseo.NetApi.Generated.Model.pallet_xcavate_whitelist.pallet;
using XcavatePaseo.NetApi.Generated.Model.sp_core.crypto;
using XcavatePaseo.NetApi.Generated.Storage;

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
        ModuleCreator = 6,
        ModuleSponsor = 7,
        ModuleBooker = 8,
        ModuleDeliverer = 9,
        ModuleAIAgent = 10,
        ModuleRecipient = 11,
    }
    public class WhitelistModel
    {
        private static HashSet<XcavateRole>? roles = null;

        public static void Clear()
        {
            roles = null;
        }

        public static Task<HashSet<XcavateRole>> GetRolesCachedAsync(SubstrateClientExt client, string address, CancellationToken token)
        {
            if (roles != null && roles.Count > 0)
                return Task.FromResult(roles);

            return GetRolesAsync(client, address, token);
        }

        public static async Task<HashSet<XcavateRole>> GetRolesAsync(SubstrateClientExt client, string address, CancellationToken token)
        {
            var accountId32 = new AccountId32();
            accountId32.Create(Utils.GetPublicKeyFrom(address));

            var roleParameter = new EnumRole();
            roleParameter.Create(Role.RealEstateInvestor);

            // 0x + Twox64 pallet + Twox64 storage + Blake2_128Concat accountId32
            var keyPrefixLength = 162;

            var keyPrefix = Utils.HexToByteArray(XcavateWhitelistStorage.AccountRolesParams(new BaseTuple<AccountId32, EnumRole>(accountId32, roleParameter)).Substring(0, keyPrefixLength));

            var fullKeys = await client.State.GetKeysPagedAsync(keyPrefix, 100, null, string.Empty, token).ConfigureAwait(false);

            if (fullKeys == null || !fullKeys.Any())
            {
                return new HashSet<XcavateRole>();
            }

            var storageChangeSets = await client.State.GetQueryStorageAtAsync(fullKeys.Select(p => Utils.HexToByteArray(p.ToString())).ToList(), string.Empty, token).ConfigureAwait(false);

            roles = new HashSet<XcavateRole>();

            foreach (var change in storageChangeSets.First().Changes)
            {
                if (change[1] == null)
                {
                    continue;
                }

                var key = change[0];
                var roleValue = Convert.ToInt32(key.Substring(key.Length - 2), 16);

                if (Enum.IsDefined(typeof(XcavateRole), roleValue))
                {
                    roles.Add((XcavateRole)roleValue);
                }
            }

            return roles;
        }
    }
}
