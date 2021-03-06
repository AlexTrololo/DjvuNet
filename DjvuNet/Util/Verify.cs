﻿using System;

namespace DjvuNet.Utilities
{
    public static class Verify
    {
        public const int SubsambpleMin = 1;
        public const int SubsampleMax = 12;

        public static void SubsampleRange(int subsample)
        {
            if (subsample < SubsambpleMin || subsample > SubsampleMax)
                throw new ArgumentException(
                    $"Argument is outside of allowed values expected from {SubsambpleMin} to {SubsampleMax}" +
                    $" actual value {subsample}", nameof(subsample));
        }
    }
}
