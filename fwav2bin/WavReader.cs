using System;
using System.IO;
using System.Text;

namespace PC1500FastLoadTools.FWav2Bin {

	/// <summary>
	/// Reads samples from RIFF WAVE files.
	/// </summary>
	internal class WavReader : BinaryReader {

		readonly long riffPosition;
		readonly uint riffLength;

		readonly long fmtPosition;
		readonly uint fmtLength;

		readonly long dataPosition;
		readonly uint dataLength;

		readonly ushort fmtFormatTag;
		readonly ushort fmtChannelCount;
		readonly uint fmtSampleRate;
		readonly uint fmtByteRate;
		readonly ushort fmtBlockAlign;
		readonly ushort fmtBitsPerSample;

		public int ChannelCount {
			get { return (int)this.fmtChannelCount; }
		}

		public int SampleRate {
			get { return (int)this.fmtSampleRate; }
		}

		public int SampleCount {
			get { return (int)(this.dataLength / this.fmtBlockAlign); }
		}

		public int SamplePosition {
			get {
				return (int)((this.BaseStream.Position - this.dataPosition) / this.fmtBlockAlign);
			}
			set {
				if (value < 0 || value >= this.SampleCount) throw new ArgumentOutOfRangeException();
				this.BaseStream.Seek(this.dataPosition + value * this.fmtBlockAlign, SeekOrigin.Begin);
			}
		}

		public TimeSpan Length {
			get { return TimeSpan.FromTicks((long)this.SampleCount * 10000000L / (long)this.SampleRate); }
		}

		public string ReadString(int length) {
			return Encoding.ASCII.GetString(this.ReadBytes(length));
		}

		public WavReader(Stream input) : base(input, Encoding.ASCII) {

			// Must be a RIFF file.
			if (this.ReadString(4) != "RIFF") throw new InvalidDataException("Missing RIFF identifier.");
			this.riffLength = this.ReadUInt32();
			this.riffPosition = this.BaseStream.Position;

			// Where's the end of the file in the stream?
			long riffEnd = this.riffPosition + this.riffLength + (this.riffLength & 1);

			// Must be a WAVE file.
			if (this.ReadString(4) != "WAVE") throw new InvalidDataException("Missing WAVE identifier.");

			// Try to find the position of the data and the format in the file.
			var foundData = false;
			var foundFormat = false;

			while (!(foundFormat && foundData)) {

				// Have we run out of file yet?
				if (this.BaseStream.Position >= riffEnd) {
					throw new InvalidDataException("Could not find WAVE fmt and data in file.");
				}

				// Get the chunk type and size
				var chunkType = this.ReadString(4);
				var chunkSize = this.ReadUInt32();

				switch (chunkType) {
					case "fmt ":
						if (foundFormat) throw new InvalidDataException("Found more than one data chunk in file.");
						foundFormat = true;
						this.fmtPosition = this.BaseStream.Position;
						this.fmtLength = chunkSize;
						break;
					case "data":
						if (foundData) throw new InvalidDataException("Found more than one data chunk in file.");
						foundData = true;
						this.dataPosition = this.BaseStream.Position;
						this.dataLength = chunkSize;
						break;
				}

				// Advance to the next chunk
				this.BaseStream.Seek(chunkSize + (chunkSize & 1), SeekOrigin.Current);
			}

			// At this point we know we have the fmt and data chunks, so try to decode the format.
			this.BaseStream.Seek(this.fmtPosition, SeekOrigin.Begin);

			// Format must be at least 16 bytes in length.
			if (this.fmtLength < 16) throw new InvalidDataException("WAVE fmt is not at least 16 bytes in length.");

			// Read the values from the format chunk.
			this.fmtFormatTag = this.ReadUInt16();
			this.fmtChannelCount = this.ReadUInt16();
			this.fmtSampleRate = this.ReadUInt32();
			this.fmtByteRate = this.ReadUInt32();
			this.fmtBlockAlign = this.ReadUInt16();
			this.fmtBitsPerSample = this.ReadUInt16();

			// Check that the format is valid for our needs.
			if (this.fmtFormatTag != 1) throw new InvalidDataException("Only integer PCM WAV files are supported.");
			if (this.fmtChannelCount != 1) throw new InvalidDataException("Only mono WAV files are supported.");
			if (this.fmtBitsPerSample != 8 && this.fmtBitsPerSample != 16) throw new InvalidDataException("Only 8- or 16-bit WAV files are supported.");

			// Seek to the start of the data.
			this.BaseStream.Seek(dataPosition, SeekOrigin.Begin);

		}

		public float ReadSample() {

			// Value to return.
			float sample = 0;

			// Keep track of where we were in the file.
			long startPosition = this.BaseStream.Position;

			// Read and normalise the sample.
			switch (this.fmtBitsPerSample) {
				case 8:
					sample = this.ReadByte();
					sample /= byte.MaxValue;
					sample -= 0.5f;
					sample *= 2.0f;
					break;
				case 16:
					sample = this.ReadInt16();
					if (sample < 0) {
						sample /= -short.MinValue;
					} else {
						sample /= +short.MaxValue;
					}
					break;
			}

			// Clamp bounds
			if (sample < -1.0f) sample = -1.0f;
			if (sample > +1.0f) sample = +1.0f;

			// Ensure we've seeked to the next sample.
			if (this.BaseStream.Position != startPosition + this.fmtBlockAlign) {
				this.BaseStream.Seek(startPosition + this.fmtBlockAlign, SeekOrigin.Begin);
			}

			return sample;

		}



	}
}