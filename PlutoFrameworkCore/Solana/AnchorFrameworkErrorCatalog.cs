namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Anchor's framework error codes - the failures every Anchor program shares, below the
    /// custom-error range (6000+). The bundled IDLs only catalog a program's custom errors,
    /// so without this a framework rejection surfaces as "an error this app version cannot
    /// decode". Messages mirror anchor-lang-error 1.1.2, the version the Xcavate programs
    /// build with; codes below 6000 that this table does not know still fall through to the
    /// undecodable message.
    /// </summary>
    public static class AnchorFrameworkErrorCatalog
    {
        /// <summary>The first custom (program-defined) error code - everything below is the framework's.</summary>
        public const int CustomErrorOffset = 6000;

        private static readonly IReadOnlyDictionary<int, string> Messages = new Dictionary<int, string>
        {
            // Instructions
            [100] = "Instruction discriminator not provided",
            [101] = "Fallback functions are not supported",
            [102] = "The program could not deserialize the given instruction",
            [103] = "The program could not serialize the given instruction",

            // IDL instructions
            [1000] = "The program was compiled without idl instructions",
            [1001] = "Invalid program given to the IDL instruction",
            [1002] = "IDL account must be empty in order to resize, try closing first",

            // Event instructions
            [1500] = "The program was compiled without `event-cpi` feature",

            // Constraints
            [2000] = "A mut constraint was violated",
            [2001] = "A has one constraint was violated",
            [2002] = "A signer constraint was violated",
            [2003] = "A raw constraint was violated",
            [2004] = "An owner constraint was violated",
            [2005] = "A rent exemption constraint was violated",
            [2006] = "A seeds constraint was violated",
            [2007] = "An executable constraint was violated",
            [2008] = "A state constraint was violated",
            [2009] = "An associated constraint was violated",
            [2010] = "An associated init constraint was violated",
            [2011] = "A close constraint was violated",
            [2012] = "An address constraint was violated",
            [2013] = "Expected zero account discriminant",
            [2014] = "A token mint constraint was violated",
            [2015] = "A token owner constraint was violated",
            [2016] = "A mint mint authority constraint was violated",
            [2017] = "A mint freeze authority constraint was violated",
            [2018] = "A mint decimals constraint was violated",
            [2019] = "A space constraint was violated",
            [2020] = "A required account for the constraint is None",
            [2021] = "A token account token program constraint was violated",
            [2022] = "A mint token program constraint was violated",
            [2023] = "An associated token account token program constraint was violated",

            // Accounts
            [3000] = "The account discriminator was already set on this account",
            [3001] = "No discriminator was found on the account",
            [3002] = "Account discriminator did not match what was expected",
            [3003] = "Failed to deserialize the account",
            [3004] = "Failed to serialize the account",
            [3005] = "Not enough account keys given to the instruction",
            [3006] = "The given account is not mutable",
            [3007] = "The given account is owned by a different program than expected",
            [3008] = "Program ID was not as expected",
            [3009] = "Program account is not executable",
            [3010] = "The given account did not sign",
            [3011] = "The given account is not owned by the system program",
            [3012] = "The program expected this account to be already initialized",
            [3013] = "The given account is not a program data account",
            [3014] = "The given account is not the associated token account",
            [3015] = "The given public key does not match the required sysvar",
            [3016] = "The account reallocation exceeds the MAX_PERMITTED_DATA_INCREASE limit",
            [3017] = "The account was duplicated for more than one reallocation",

            // Miscellaneous
            [4100] = "The declared program id does not match the actual program id",
            [4101] = "You cannot/should not initialize the payer account as a program account",
            [4102] = "Invalid numeric conversion",
        };

        /// <summary>The framework error's message, or null when the code is not one Anchor defines.</summary>
        public static bool TryDescribe(long code, out string message)
        {
            if (Messages.TryGetValue((int)code, out var known))
            {
                message = known;
                return true;
            }

            message = string.Empty;
            return false;
        }
    }
}
