using System;
using System.Reflection;
using DiceCraft.Verification;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Tests
{
    public sealed class PlayerVerificationDisplayContractTests
    {
        [Serializable] private sealed class Persisted { public DisplayChoice confirmed; public bool pending; }
        private static DisplayChoice Requested() => new DisplayChoice { displayName = "Probe monitor", width = 2560, height = 1440, mode = FullScreenMode.Windowed };
        private static void Validate(DisplayChoice requested, DisplayChoice actual, Persisted saved)
        {
            var method = typeof(PlayerVerification).GetMethod("RequireConfirmedDisplay", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { requested, actual, JsonUtility.ToJson(saved) });
        }
        [Test] public void VERIFY_ConfirmedWindowRequiresMatchingActualAndDurableRequestedChoice()
        {
            var requested = Requested();
            Assert.DoesNotThrow(() => Validate(requested, requested.Clone(), new Persisted { confirmed = requested.Clone(), pending = false }));
        }
        [TestCase("native-height")][TestCase("native-mode")][TestCase("native-display")]
        [TestCase("saved-height")][TestCase("pending")][TestCase("missing-confirmed")]
        public void VERIFY_ConfirmedWindowRejectsPostConfirmationDivergence(string mismatch)
        {
            var requested = Requested(); var actual = requested.Clone();
            var saved = new Persisted { confirmed = requested.Clone(), pending = false };
            switch (mismatch)
            {
                case "native-height": actual.height = 1417; break; // Actual failing Player readback.
                case "native-mode": actual.mode = FullScreenMode.FullScreenWindow; break;
                case "native-display": actual.displayName = "Different monitor"; break;
                case "saved-height": saved.confirmed.height = 1417; break;
                case "pending": saved.pending = true; break;
                case "missing-confirmed": saved.confirmed = null; break;
            }
            var error = Assert.Throws<TargetInvocationException>(() => Validate(requested, actual, saved));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
        }
    }
}
