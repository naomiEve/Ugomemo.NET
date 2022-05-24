using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Ugomemo.NET.Exceptions;

namespace Ugomemo.NET.Tests
{
    [TestClass]
    [DeploymentItem("TestFiles/pekira_beach.ppm")]
    public class ParsingTests
    {
        private Flipnote flipnote;

        private bool CompareImages(Image<Rgb24> lhs, Image<Rgb24> rhs)
        {
            for (var x = 0; x < lhs.Width; x++)
            {
                for (var y = 0; x < lhs.Height; x++)
                {
                    if (lhs[x, y] != rhs[x, y])
                        return false;
                }
            }

            return true;
        }

        [TestInitialize]
        public void InitializeParsingTests()
        {
            flipnote = new Flipnote("pekira_beach.ppm", false);
        }

        [TestMethod]
        [DeploymentItem("TestFiles/not_a_flipnote.ppm")]
        public void ParseValidFlipnote()
        {
            Assert.IsTrue(File.Exists("pekira_beach.ppm"));
            Assert.IsTrue(File.Exists("not_a_flipnote.ppm"));

            try
            {
                var badFlipnote = new Flipnote("not_a_flipnote.ppm");
                Assert.Fail("Parsed a bad flipnote!");
            }
            catch (NotAFlipnoteException)
            {

            }

            try
            {
                var goodFlipnote = new Flipnote("pekira_beach.ppm");
            }
            catch (NotAFlipnoteException)
            {
                Assert.Fail("Failed to parse good known flipnote!");
            }
        }

        [TestMethod]
        public void EnsureCorrectFrameCount()
        {
            Assert.AreEqual(flipnote.FrameCount, 186u);
        }

        [TestMethod]
        public void EnsureCorrectLocked()
        {
            Assert.IsTrue(flipnote.Locked);
        }

        [TestMethod]
        public void EnsureCorrectCreatedOn()
        {
            Assert.AreEqual(flipnote.CreatedOn.Year, 2011);
            Assert.AreEqual(flipnote.CreatedOn.Month, 7);
            Assert.AreEqual(flipnote.CreatedOn.Day, 30);
        }

        [TestMethod]
        [DeploymentItem("TestFiles/sample_thumbnail.png")]
        public void EnsureThumbnailPixelsMatch()
        {
            Assert.IsTrue(File.Exists("sample_thumbnail.png"));
            using var image = Image.Load<Rgb24>("sample_thumbnail.png");

            Assert.IsTrue(CompareImages(flipnote.Thumbnail.Image, image));
        }

        [TestMethod]
        public void EnsureFlipnoteAnimationInfoIsCorrect()
        {
            Assert.IsTrue(flipnote.AnimationInfo.Looping);
            Assert.IsFalse(flipnote.AnimationInfo.HideLayer1);
            Assert.IsFalse(flipnote.AnimationInfo.HideLayer2);
        }

        [TestMethod]
        public void EnsureFrameInfoIsCorrect()
        {
            Assert.AreEqual(flipnote.Frames[0].FrameInfo.Type, Animation.FrameType.Keyframe);
            Assert.AreEqual(flipnote.Frames[0].FrameInfo.PaperColor, Animation.PaperColor.White);
            Assert.AreEqual(flipnote.Frames[0].FrameInfo.Layer1Color, Animation.PenColor.InverseOfPaper);
            Assert.AreEqual(flipnote.Frames[0].FrameInfo.Layer2Color, Animation.PenColor.Blue);

            Assert.AreEqual(flipnote.Frames[1].FrameInfo.Type, Animation.FrameType.Interframe);
            Assert.AreEqual(flipnote.Frames[1].FrameInfo.PaperColor, Animation.PaperColor.White);
            Assert.AreEqual(flipnote.Frames[1].FrameInfo.Layer1Color, Animation.PenColor.Red);
            Assert.AreEqual(flipnote.Frames[1].FrameInfo.Layer2Color, Animation.PenColor.Blue);
        }

        [TestMethod]
        [DeploymentItem("TestFiles/sample_frame.png")]
        public void EnsureFramesAreCorrect()
        {
            Assert.IsTrue(File.Exists("sample_frame.png"));

            var flipnote = new Flipnote("pekira_beach.ppm");
            using var image = Image.Load<Rgb24>("sample_frame.png");
            Assert.IsTrue(CompareImages(flipnote.Frames[85].Image, image));
        }

        [TestMethod]
        public void EnsureSoundHeaderIsCorrect()
        {
            Assert.AreEqual(flipnote.AnimationInfo.PlaybackSpeed, 1 / 6f);
            Assert.AreEqual(flipnote.AnimationInfo.BGMPlaybackSpeed, 1 / 6f);
        }
    }
}
