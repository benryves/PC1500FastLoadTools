using System.IO;

namespace PC1500FastLoadTools.FWav2Bin {

	public enum WaveCycleType {
		StartData,
		WaveCycle,
		EndData,
		EndFile,
	};

	/// <summary>
	/// Reads wave cycles from a <see cref="WavReader"/> instance.
	/// </summary>
	internal class WaveCycleReader : WavReader  {

		readonly float threshold = 0.2f;
		bool inStream = false;

		public WaveCycleReader(Stream input) : base(input) {

		}

		public WaveCycleType ReadWaveCycle(out int frequency) {
			
			// Default return frequency is 0Hz
			frequency = 0;

			for (int startSample = this.SamplePosition; startSample < this.SampleCount; ++startSample) {

				// What's the length of the wave?
				int waveLength = 0;

				// Read the starting sample.
				float startLevel = this.ReadSample();

				// Try to decode a full wave.
				if (startLevel > +threshold || startLevel < -threshold) {
					for (int midSample = startSample + 1; midSample < this.SampleCount; ++midSample) {
						float midLevel = this.ReadSample();
						if ((startLevel > +threshold && midLevel < -threshold) || (startLevel < -threshold && midLevel > +threshold)) {
							for (int endSample = midSample + 1; endSample < this.SampleCount; ++endSample) {
								float endLevel = this.ReadSample();
								if ((midLevel > +threshold && endLevel < -threshold) || (midLevel < -threshold && endLevel > +threshold)) {
									waveLength = endSample - startSample;
									break;
								}
							}
							break;
						}
					}

					if (waveLength > 0) {
						// We found a wave.
						this.SamplePosition = startSample + waveLength;
						if (!this.inStream) {
							this.inStream = true;
							return WaveCycleType.StartData;
						} else {
							// What's the frequency of the wave?
							frequency = this.SampleRate / waveLength;
							return WaveCycleType.WaveCycle;
						}
					} else {
						// No wave found yet.
						this.SamplePosition = startSample + 1;
						if (inStream) {
							// End of the data stream.
							this.inStream = false;
							return WaveCycleType.EndData;
						}
					}

				}

			}

			return WaveCycleType.EndFile;

		}

	}
}
