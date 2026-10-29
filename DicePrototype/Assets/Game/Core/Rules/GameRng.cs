using System;
using System.Globalization;

namespace DiceCraft.Core
{
    /// <summary>Persist State after every accepted transaction. Visuals must never use this source.</summary>
    public sealed class GameRng
    {
        public uint State { get; private set; }
        public GameRng(uint state) { State = state == 0u ? 0x9E3779B9u : state; }
        public GameRng Clone() => new GameRng(State);

        public uint NextNonZeroUInt()
        {
            unchecked
            {
                uint x = State;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                State = x;
                return x;
            }
        }

        public int NextIndex(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            uint n = (uint)count;
            uint limit = (uint.MaxValue / n) * n;
            uint u;
            do { u = NextNonZeroUInt() - 1u; } while (u >= limit);
            return (int)(u % n);
        }

        public static bool TryParseSeed(string text, out uint seed)
        {
            // NumberStyles.None deliberately rejects signs, whitespace, decimals and hashes.
            return uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out seed);
        }
    }
}
