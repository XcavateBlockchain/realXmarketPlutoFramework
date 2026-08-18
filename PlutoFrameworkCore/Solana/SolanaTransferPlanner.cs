using System.Numerics;
using Solnet.Programs;
using Solnet.Rpc.Models;
using Solnet.Wallet;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Turns "send this much of this token to this address" into instructions.
    /// </summary>
    /// <remarks>
    /// Pure, so every branch is testable without a cluster. Whether the recipient already has
    /// a token account is the one fact it cannot work out for itself, so it is passed in;
    /// <see cref="SolanaTransferModel"/> does that lookup.
    /// </remarks>
    public static class SolanaTransferPlanner
    {
        public static SolanaTransferPlan Build(
            string senderAddress,
            string recipientAddress,
            SolanaTransferBalance token,
            BigInteger baseUnits,
            bool recipientAccountExists)
        {
            if (!SolanaAddressValidator.IsValidAddress(senderAddress))
            {
                throw new ArgumentException("Not a Solana address", nameof(senderAddress));
            }

            if (!SolanaAddressValidator.IsValidAddress(recipientAddress))
            {
                throw new ArgumentException("Not a Solana address", nameof(recipientAddress));
            }

            if (baseUnits <= BigInteger.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(baseUnits), baseUnits, "A transfer must move a positive amount");
            }

            // The popup blocks this, but the planner is the last place that can. Building a
            // transaction certain to fail wastes a signature prompt and a round trip.
            if (baseUnits > token.SpendableBaseUnits)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(baseUnits), baseUnits, "Amount is above the spendable balance");
            }

            var sender = new PublicKey(senderAddress);
            var recipient = new PublicKey(recipientAddress);

            // u64 is the SPL amount type; anything larger cannot exist on chain, and the
            // balance check above already bounds this.
            var amount = (ulong)baseUnits;

            return token.IsNative
                ? BuildSolTransfer(sender, recipient, amount)
                : BuildSplTransfer(sender, recipient, token, amount, recipientAccountExists);
        }

        private static SolanaTransferPlan BuildSolTransfer(
            PublicKey sender, PublicKey recipient, ulong lamports) => new()
            {
                Instructions = [SystemProgram.Transfer(sender, recipient, lamports)],

                // A SOL transfer touches no token account, whatever the recipient's SPL
                // state. Creating one here would charge the sender rent for nothing.
                CreatesRecipientAccount = false,
            };

        private static SolanaTransferPlan BuildSplTransfer(
            PublicKey sender,
            PublicKey recipient,
            SolanaTransferBalance token,
            ulong amount,
            bool recipientAccountExists)
        {
            var mint = new PublicKey(token.Mint);
            var tokenProgram = new PublicKey(token.ProgramId);

            var source = SolanaAssociatedTokenAccount.Derive(sender, mint, tokenProgram);
            var destination = SolanaAssociatedTokenAccount.Derive(recipient, mint, tokenProgram);

            var instructions = new List<TransactionInstruction>();

            if (!recipientAccountExists)
            {
                // Must precede the transfer: moving tokens into an account that does not
                // exist yet fails. The sender pays its rent.
                instructions.Add(SolanaAssociatedTokenAccount.CreateInstruction(
                    sender, recipient, mint, tokenProgram));
            }

            // TransferChecked rather than Transfer: it carries the mint and decimals, so a
            // decimals mistake is rejected by the chain instead of silently sending 1000x.
            var transfer = TokenProgram.TransferChecked(
                source, destination, amount, token.Decimals, sender, mint);

            // Solnet stamps the legacy program id onto that instruction, the same way its
            // associated-account helpers do. Token-2022 takes an identical account layout and
            // instruction encoding, so the accounts and data are kept and only the program is
            // corrected — otherwise a Token-2022 transfer is addressed to the legacy program
            // and rejected.
            instructions.Add(new TransactionInstruction
            {
                ProgramId = tokenProgram.KeyBytes,
                Keys = transfer.Keys,
                Data = transfer.Data,
            });

            return new SolanaTransferPlan
            {
                Instructions = instructions,
                CreatesRecipientAccount = !recipientAccountExists,
            };
        }
    }
}
