// ReSharper disable once CheckNamespace
namespace DofusLFGDiscordBot.Core
{
    public class Defined
    {
#if DEBUG
        public const bool IsProd = false;
        public const bool UseProdDatabase = true;
        public const bool UseProdToken = true;
#else
        public const bool IsProd = true;
        public const bool UseProdDatabase = true;
        public const bool UseProdToken = true;
#endif
    }
}
