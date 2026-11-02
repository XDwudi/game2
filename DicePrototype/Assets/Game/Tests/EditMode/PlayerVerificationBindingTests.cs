using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DiceCraft.Core;
using DiceCraft.Verification;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Tests
{
    // Report-binding coverage only; real writer termination and native reader recovery
    // remain separate built-player requirements, never inferred from these tests.
    public sealed class PlayerVerificationBindingTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject host;
        private GameApp app;
        private PlayerVerification probe;
        private TextAsset configAsset;
        private FieldInfo directoryField;
        private object previousDirectory;
        private string directory;
        [Serializable] private sealed class Report { public string configSha256, configVersion, buildIdentity, status; public bool stressCompleted; }
        [Serializable] private sealed class Identity { public string contentConfigSha256; }

        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Application.temporaryCachePath, "V07ReportBinding_" + Guid.NewGuid().ToString("N"));
            directoryField = typeof(PlayerVerification).GetField("configuredDirectory", BindingFlags.Static | BindingFlags.NonPublic);
            previousDirectory = directoryField.GetValue(null); directoryField.SetValue(null, directory);
            host = new GameObject("Inactive verification report contract"); host.SetActive(false);
            app = host.AddComponent<GameApp>(); probe = host.AddComponent<PlayerVerification>();
            configAsset = new TextAsset(File.ReadAllText(Path.Combine(Application.dataPath, "Game/Data/P0Config.json"), Encoding.UTF8));
            app.configAsset = configAsset;
            var config = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(configAsset.text)); config.Validate();
            typeof(GameApp).GetProperty("Config").GetSetMethod(true).Invoke(app, new object[] { config });
            typeof(PlayerVerification).GetField("app", PrivateInstance).SetValue(probe, app);
        }
        [TearDown] public void TearDown()
        {
            directoryField?.SetValue(null, previousDirectory);
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            if (configAsset != null) UnityEngine.Object.DestroyImmediate(configAsset);
        }
        [TestCase("awaiting_external_process_termination", -1)]
        [TestCase("recovery_passed", 0)]
        public void VERIFY_RecoveryRoleReportBindsActualConfigWithoutReplayInitialization(string status, int exitCode)
        {
            Invoke("InitializeConfigBinding");
            Invoke("WriteReport", status, null, exitCode);
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(directory, "player-report.json"), Encoding.UTF8));
            string actual;
            using (var sha = SHA256.Create()) actual = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(configAsset.text))).Replace("-", "").ToLowerInvariant();
            Assert.That(report.configSha256, Is.EqualTo(actual));
            Assert.That(report.configSha256, Is.EqualTo(JsonUtility.FromJson<Identity>(Resources.Load<TextAsset>("BuildIdentity").text).contentConfigSha256));
            Assert.That(report.configVersion, Is.EqualTo(app.Config.configVersion));
            Assert.That(report.buildIdentity, Is.EqualTo(Resources.Load<TextAsset>("BuildIdentity").text));
            Assert.That(report.status, Is.EqualTo(status)); Assert.That(report.stressCompleted, Is.False);
        }
        [Test] public void VERIFY_RecoveryBindingRejectsActualConfigBytesThatDifferFromBuildIdentity()
        {
            var changed = new TextAsset(configAsset.text + " ");
            try
            {
                app.configAsset = changed; // Still valid JSON and the same version, but different actual bytes.
                var error = Assert.Throws<TargetInvocationException>(() => Invoke("InitializeConfigBinding"));
                Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(error.InnerException.Message, Does.Contain("configuration hash differs"));
            }
            finally { app.configAsset = configAsset; UnityEngine.Object.DestroyImmediate(changed); }
        }
        private void Invoke(string name, params object[] args)
        {
            var method = typeof(PlayerVerification).GetMethod(name, PrivateInstance);
            Assert.That(method, Is.Not.Null, name); method.Invoke(probe, args);
        }
    }
}
