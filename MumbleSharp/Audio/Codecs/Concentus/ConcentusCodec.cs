using System;
using System.Collections.Generic;
using Concentus.Structs;
using Concentus.Enums;

namespace MumbleSharp.Audio.Codecs.Concentus
{
    public class ConcentusCodec
        : IVoiceCodec
    {
        private readonly OpusDecoder _decoder;
        private readonly OpusEncoder _encoder;
        private readonly int _sampleRate;
        private readonly ushort _frameSize;

        private readonly float[] _permittedFrameSizesInMillisec = {
            2.5f, 5, 10,
            20, 40, 60
        };

        private int[] _permittedFrameSizes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcentusCodec"/> class.
        /// </summary>
        /// <param name="sampleRate">The sample rate in Hertz (samples per second).</param>
        /// <param name="sampleBits">The sample bit depth.</param>
        /// <param name="sampleChannels">The sample channels (1 for mono, 2 for stereo).</param>
        /// <param name="frameSize">Size of the frame in samples.</param>
        public ConcentusCodec(int sampleRate = Constants.DEFAULT_AUDIO_SAMPLE_RATE, byte sampleBits = Constants.DEFAULT_AUDIO_SAMPLE_BITS, byte channels = Constants.DEFAULT_AUDIO_SAMPLE_CHANNELS, ushort frameSize = Constants.DEFAULT_AUDIO_FRAME_SIZE)
        {
            _sampleRate = sampleRate;
            _frameSize = frameSize;
            _decoder = OpusDecoder.Create(sampleRate, channels);
            _encoder = OpusEncoder.Create(sampleRate, channels, OpusApplication.OPUS_APPLICATION_VOIP);
            _encoder.UseInbandFEC = true;
            _encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
            _encoder.Bitrate = 12000; //TODO: Change this!

            _permittedFrameSizes = new int[_permittedFrameSizesInMillisec.Length];
            for (var i = 0; i < _permittedFrameSizesInMillisec.Length; i++)
                _permittedFrameSizes[i] = (int)(sampleRate / 1000f * _permittedFrameSizesInMillisec[i]);
        }

        public byte[] Decode(byte[] encodedData)
        {
            int frameSize = OpusPacketInfo.GetNumSamples(encodedData, 0, encodedData.Length, _sampleRate);
            if (frameSize < 1)
            {
                return null;
            }

            short[] outputBuffer = new short[frameSize];

            int length = _decoder.Decode(encodedData, 0, encodedData.Length, outputBuffer, 0, frameSize, decode_fec: true);

            if (length != outputBuffer.Length)
            {
                Array.Resize(ref outputBuffer, length);
            }

            byte[] dst = new byte[outputBuffer.Length * 2];
            Buffer.BlockCopy(outputBuffer, 0, dst, 0, outputBuffer.Length);

            return dst;

            //if (encodedData == null)
            //{
            //    _decoder.Decode(null, 0, 0, new byte[_sampleRate / _frameSize], 0);
            //    return null;
            //}

            //int samples = OpusDecoder.GetSamples(encodedData, 0, encodedData.Length, _sampleRate);
            //if (samples < 1)
            //    return null;

            //byte[] dst = new byte[samples * sizeof(ushort)];
            //int length = _decoder.Decode(encodedData, 0, encodedData.Length, dst, 0);
            //if (dst.Length != length)
            //    Array.Resize(ref dst, length);
            //return dst;
        }

        public IEnumerable<int> PermittedEncodingFrameSizes
        {
            get
            {
                return _permittedFrameSizes;
            }
        }

        public byte[] Encode(ArraySegment<byte> pcm)
        {
            var samples = pcm.Count / sizeof(ushort);
            var numberOfBytes = samples * _encoder.LSBDepth / 8 * _encoder.NumChannels;
            //var numberOfBytes = _encoder.FrameSizeInBytes(samples);

            byte[] dst = new byte[numberOfBytes];

            short[] pcmShort = new short[pcm.Count / 2];
            Buffer.BlockCopy(pcm.Array, pcm.Offset, pcmShort, 0, pcm.Count);

            try
            {
                int encodedBytes = _encoder.Encode(pcmShort, 0, samples, dst, 0, dst.Length);
                //without it packet will have huge zero-value-tale
                Array.Resize(ref dst, encodedBytes);
            }
            catch (Exception ex)
            {
                // TODO: Fail more gracefully!
                throw ex;
            }

            return dst;
        }
    }
}
