﻿using System;
using System.IO;
using Ugomemo.NET.Animation;
using Ugomemo.NET.Exceptions;
using Ugomemo.NET.IO;

namespace Ugomemo.NET
{
    public sealed partial class Flipnote
    {
        private static readonly char[] MAGIC_STRING = { 'P', 'A', 'R', 'A' };
        private const uint FORMAT_VERSION = 0x24;

        /// <summary>
        /// Constant epoch, always at the 1st of January 2000.
        /// </summary>
        private static readonly DateTimeOffset EPOCH = DateTimeOffset.FromUnixTimeSeconds(946684800);

        /// <summary>
        /// Whether the flipnote parser should actually generate images for every frame.
        /// </summary>
        private bool ShouldGenerateImagesForFrames { get; set; }

        /// <summary>
        /// The size of the animation data.
        /// </summary>
        private uint AnimationDataSize { get; set; }

        /// <summary>
        /// The size of the sound data.
        /// </summary>
        private uint SoundDataSize { get; set; }

        /// <summary>
        /// The size of the background music track.
        /// </summary>
        private uint BGMTrackSize { get; set; }

        /// <summary>
        /// The size of the first sound effect track.
        /// </summary>
        private uint Sound1TrackSize { get; set; }

        /// <summary>
        /// The size of the second sound effect track.
        /// </summary>
        private uint Sound2TrackSize { get; set; }

        /// <summary>
        /// The size of the third sound effect track.
        /// </summary>
        private uint Sound3TrackSize { get; set; }

        private void Parse(string filename, bool generateFrameImages)
        {
            ShouldGenerateImagesForFrames = generateFrameImages;

            using var fileStream = new FileStream(filename, FileMode.Open);
            using var bitReader = new BinaryBitReaderEx(fileStream, System.Text.Encoding.ASCII);

            ParseHeader(filename, bitReader);
            ParseMetadata(bitReader);
            ParseThumbnail(bitReader);
            ParseAnimationHeader(bitReader);
            ParseSoundHeader(bitReader);
        }

        /// <summary>
        /// Parses the header of the flipnote.
        /// </summary>
        private void ParseHeader(string filename, BinaryBitReaderEx reader)
        {
            if (reader.ReadByte() != MAGIC_STRING[0] ||
                reader.ReadByte() != MAGIC_STRING[1] ||
                reader.ReadByte() != MAGIC_STRING[2] ||
                reader.ReadByte() != MAGIC_STRING[3])
            {
                throw new NotAFlipnoteException($"{filename} is not a flipnote - invalid magic!");
            }

            AnimationDataSize = reader.ReadUInt(32);
            SoundDataSize = reader.ReadUInt(32);
            FrameCount = reader.ReadUInt(16);

            var formatVersion = reader.ReadUInt(16);
            if (formatVersion != FORMAT_VERSION)
                throw new NotAFlipnoteException($"{filename} is not a flipnote - invalid format version!");
        }

        /// <summary>
        /// Parses the metadata of the flipnote.
        /// </summary>
        private void ParseMetadata(BinaryBitReaderEx reader)
        {
            Locked = reader.ReadUInt(16) == 1;

            // TODO: Parse all of the skipped data.
            //       - Author name
            //       - Author ID
            //       - Filename
            reader.ReadBytes(136);

            var timestamp = reader.ReadUInt();
            CreatedOn = EPOCH.AddSeconds(timestamp);

            // NOTE: The last 2 bytes of the metadata are always null and ignored.
            reader.ReadUInt(16);
        }

        /// <summary>
        /// Parses the tiny thumbnail of the flipnote.
        /// </summary>
        private void ParseThumbnail(BinaryBitReaderEx reader)
        {
            Thumbnail = new Thumbnail(reader);
        }

        /// <summary>
        /// Parses the animation header of the flipnote.
        /// </summary>
        private void ParseAnimationHeader(BinaryBitReaderEx reader)
        {
            var frameOffsetTableSize = reader.ReadUInt(16);

            // NOTE: The docs actually specify that this unknown value is supposed to be only
            //       16 bits, but from my own experimentation the value is actually 32 bits long.
            reader.ReadUInt(32);

            var flags = reader.ReadUInt(16);
            AnimationInfo = new AnimationInfo
            {
                Looping = (flags & 0x2) != 0,
                HideLayer1 = (flags & 0x10) != 0,
                HideLayer2 = (flags & 0x20) != 0
            };

            ParseAnimationFrameOffsetTable(reader, frameOffsetTableSize);
        }

        /// <summary>
        /// Parses the animation frame offset table of the flipnote.
        /// </summary>
        private void ParseAnimationFrameOffsetTable(BinaryBitReaderEx reader, uint size)
        {
            // Every frame offset is a 4 byte uint32.
            var frameOffsetCount = size / 4;
            var frameOffsetTable = new uint[frameOffsetCount];

            // Calculate all of the frame positions with the offset already applied, to make seeking easier.
            const int DEFAULT_FRAME_TABLE_POSITION = 0x06A8;
            for (var i = 0; i < frameOffsetCount; i++)
                frameOffsetTable[i] = DEFAULT_FRAME_TABLE_POSITION + size + reader.ReadUInt();

            ParseFrames(reader, frameOffsetTable);
        }

        /// <summary>
        /// Parses all of the frames this flipnote has.
        /// </summary>
        private void ParseFrames(BinaryBitReaderEx reader, uint[] offsets)
        {
            Frames = new Frame[offsets.Length];

            for (var i = 0; i < FrameCount + 1; i++)
            {
                reader.Seek(offsets[i]);

                var frame = new Frame(reader);
                if (frame.FrameInfo.Type == FrameType.Interframe)
                    frame.MergeWithFrame(Frames[i - 1]);

                if (ShouldGenerateImagesForFrames)
                    frame.GenerateImage(AnimationInfo);

                Frames[i] = frame;
            }
        }

        /// <summary>
        /// Parse the sound flags for each frame.
        /// </summary>
        private void ParseSoundFlags(BinaryBitReaderEx reader)
        {
            const int ANIMATION_DATA_BEGIN_OFFSET = 0x6A0;
            reader.Seek(ANIMATION_DATA_BEGIN_OFFSET + AnimationDataSize);

            for (var i = 0; i < FrameCount + 1; i++)
            {
                var flags = reader.ReadByte();
                Frames[i].SoundFlags = new SoundFlags
                {
                    Sound1 = (flags & 0x1) != 0,
                    Sound2 = (flags & 0x2) != 0,
                    Sound3 = (flags & 0x4) != 0
                };
            }
        }

        /// <summary>
        /// Parses the sound header of the flipnote.
        /// </summary>
        private void ParseSoundHeader(BinaryBitReaderEx reader)
        {
            ParseSoundFlags(reader);

            // NOTE: If the current reader position isn't an even number, we must round the position
            //       to the nearest multiple of 4.
            const int ANIMATION_DATA_BEGIN_OFFSET = 0x6A0;
            var position = ANIMATION_DATA_BEGIN_OFFSET + AnimationDataSize + (FrameCount + 1);
            if ((position % 4) != 0)
                position += 4 - (position % 4);

            reader.Seek(position);

            BGMTrackSize = reader.ReadUInt();
            Sound1TrackSize = reader.ReadUInt();
            Sound2TrackSize = reader.ReadUInt();
            Sound3TrackSize = reader.ReadUInt();

            // NOTE: According to the docs, the framerate values are backwards for some reason.
            AnimationInfo.PlaybackSpeed = FRAMERATE_TABLE[8 - reader.ReadByte()];
            AnimationInfo.BGMPlaybackSpeed = FRAMERATE_TABLE[8 - reader.ReadByte()];
        }
    }
}
