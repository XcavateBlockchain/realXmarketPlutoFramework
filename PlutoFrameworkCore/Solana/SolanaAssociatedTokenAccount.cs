using Solnet.Programs;
using Solnet.Rpc.Models;
using Solnet.Wallet;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Associated token account derivation and creation, parameterised by token program.
    /// </summary>
    /// <remarks>
    /// Solnet has this, but not in a released package. <c>Solana.Programs</c> 8.7.0 — the
    /// version pinned here, and the newest published — exposes only
    /// <c>DeriveAssociatedTokenAccount(owner, mint)</c> and
    /// <c>CreateAssociatedTokenAccount(payer, owner, mint)</c>, both of which hardcode the
    /// legacy SPL token program. The overloads taking a <c>tokenProgramId</c> exist on the
    /// repository's master branch and have never shipped.
    ///
    /// Without them the <c>ProgramId</c> field on <see cref="SolanaTokenWhitelistEntry"/> is
    /// decorative: a Token-2022 mint would derive a legacy account, and the transfer would go
    /// to an address that cannot hold it. <see cref="LegacyDerivationMatchesSolnetsOwn"/> in
    /// the tests pins this implementation against Solnet's for the legacy case, so the seed
    /// order is checked against an independent implementation rather than against itself.
    /// </remarks>
    public static class SolanaAssociatedTokenAccount
    {
        /// <summary>
        /// The account address for one owner, mint and token program.
        /// </summary>
        public static PublicKey Derive(PublicKey owner, PublicKey mint, PublicKey tokenProgramId)
        {
            // Seed order is part of the address. Getting it wrong produces a valid-looking
            // account that no wallet will ever find.
            var seeds = new List<byte[]> { owner.KeyBytes, tokenProgramId.KeyBytes, mint.KeyBytes };

            if (!PublicKey.TryFindProgramAddress(
                    seeds, AssociatedTokenAccountProgram.ProgramIdKey, out var address, out _))
            {
                // Every seed triple has a valid bump in practice; a failure here means the
                // inputs were not keys at all.
                throw new InvalidOperationException(
                    "Could not derive an associated token account for these inputs");
            }

            return address;
        }

        /// <summary>
        /// The instruction creating that account, paid for by <paramref name="payer"/>.
        /// </summary>
        /// <remarks>
        /// The account layout matches Solnet's own: payer, the new account, its owner, the
        /// mint, the system program, the token program and the rent sysvar, with no
        /// instruction data. The only difference is that the token program is an argument.
        /// </remarks>
        public static TransactionInstruction CreateInstruction(
            PublicKey payer, PublicKey owner, PublicKey mint, PublicKey tokenProgramId) => new()
            {
                ProgramId = AssociatedTokenAccountProgram.ProgramIdKey.KeyBytes,
                Keys =
                [
                    AccountMeta.Writable(payer, true),
                    AccountMeta.Writable(Derive(owner, mint, tokenProgramId), false),
                    AccountMeta.ReadOnly(owner, false),
                    AccountMeta.ReadOnly(mint, false),
                    AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
                    AccountMeta.ReadOnly(tokenProgramId, false),
                    AccountMeta.ReadOnly(SysVars.RentKey, false),
                ],
                Data = [],
            };

        /// <summary>
        /// The CreateIdempotent variant: same accounts, instruction data <c>[1]</c>. It
        /// succeeds whether or not the account already exists, so it is safe to prepend
        /// in front of an instruction that needs the account without racing a concurrent
        /// creation.
        /// </summary>
        public static TransactionInstruction CreateIdempotentInstruction(
            PublicKey payer, PublicKey owner, PublicKey mint, PublicKey tokenProgramId) => new()
            {
                ProgramId = AssociatedTokenAccountProgram.ProgramIdKey.KeyBytes,
                Keys = CreateInstruction(payer, owner, mint, tokenProgramId).Keys,
                Data = [1],
            };
    }
}
