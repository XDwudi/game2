using System;
using System.IO;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Tests
{
    public sealed class DisplaySettingsTests
    {
        [TestCase(1920,1040)][TestCase(3840,2160)][TestCase(1280,720)][TestCase(800,480)][TestCase(3440,1440)]
        public void DISPLAY_AutomaticWindowFitsEightyFivePercentAndRetainsSixteenNine(int w,int h)
        {
            var size=DisplaySettings.AutomaticWindow(new RectInt(0,0,w,h));
            Assert.That(size.x,Is.LessThanOrEqualTo(Math.Min(1920,(int)(w*.85))));Assert.That(size.y,Is.LessThanOrEqualTo(Math.Min(1080,(int)(h*.85))));
            Assert.That(Math.Abs(size.x*9-size.y*16),Is.LessThan(16));Assert.That(size.x,Is.GreaterThan(0));
        }
        [Test]
        public void SAVE_V06_NormalSlotRejectsDebugPayloadDespiteValidSchemaAndChecksum()
        {
            var text=File.ReadAllText(Path.Combine(Application.dataPath,"Game/Data/P0Config.json"));var cfg=JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(text));cfg.Validate();
            string dir=Path.Combine(Application.temporaryCachePath,"V06Slot_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);string path=Path.Combine(dir,"active-run.json");
            var debug=GameSession.CreateDebugWorkspace(cfg,new MemorySaveStore());
            new AtomicSaveStore(path,cfg,SaveSlotPolicy.Debug).Save(debug.State);
            var original=File.ReadAllBytes(path);var normal=new AtomicSaveStore(path,cfg,SaveSlotPolicy.Normal);
            Assert.That(normal.Load(),Is.Null);Assert.That(normal.LastLoadNotice,Does.Contain("调试"));
            Assert.Throws<InvalidDataException>(()=>normal.Save(debug.State));CollectionAssert.AreEqual(original,File.ReadAllBytes(path));
        }
    }
}
