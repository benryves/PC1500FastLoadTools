using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace PC1500FastLoadTools.FBin2Wav {
	internal class Program {
		static int Main(string[] args) {

			// Grab the assembly and assembly name.
			var assembly = Assembly.GetExecutingAssembly();
			var assemblyName = assembly.GetName();

			// Extract the options and input/output filenames from the command-line arguments.

			// Option name, whether it requires a value or not.
			var availableOptions = new[] {
				new KeyValuePair<string, bool>("-t", true),
				new KeyValuePair<string, bool>("--type", true),
				new KeyValuePair<string, bool>("-q", false),
				new KeyValuePair<string, bool>("--quiet", false),
				new KeyValuePair<string, bool>("-s", true),
				new KeyValuePair<string, bool>("--sync", true),
				new KeyValuePair<string, bool>("--tap", false),
				new KeyValuePair<string, bool>("--version", false),
				new KeyValuePair<string, bool>("--help", false),
			};

			// Full option name, abbreviated option name.
			var abbreviatedOptions = new[] {
				new KeyValuePair<string, string>("--type", "-t"),
				new KeyValuePair<string, string>("--quiet", "-q"),
				new KeyValuePair<string, string>("--sync", "-s"),
			};

			// Parse arguments
			Dictionary<string, string> options;
			string inputFile;
			string outputFile;
			float sync = 3f;

			try {
				Arguments.Parse(args, availableOptions, abbreviatedOptions, out options, out inputFile, out outputFile);
			} catch (Exception ex) {
				Console.Error.WriteLine("{0}: {1}", assemblyName.Name, ex.Message);
				Console.Error.WriteLine("{0}: Use '{0} --help' to show help.", assemblyName.Name);
				return 1;
			}

			// Should we display the version information?
			if (options.ContainsKey("--version")) {
				Console.WriteLine("{0} version {1}", assemblyName.Name, assemblyName.Version);
				var copyright = (AssemblyCopyrightAttribute[])assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
				if (copyright.Length > 0) Console.WriteLine(copyright[0].Copyright);
				if (!options.ContainsKey("--help")) return 0;
			}

			// Should we display the help information?
			if (options.ContainsKey("--help")) {
				var name = assembly.GetName();
				Console.WriteLine("Usage: {0} [Options] SrcFile(.typ) [DstFile(.wav/.tap)]", name.Name);
				Console.WriteLine(Properties.Resources.Help);
				return 0;
			}

			// Check the supplied options/arguments.
			try {
				if (options.ContainsKey("--type") && options["--type"] != "img" && options["--type"] != "bin" && options["--type"] != "tap") throw new ArgumentException(string.Format("{0} is not a valid option for the output file type.", options["--type"]));
				if (options.ContainsKey("--sync") && !float.TryParse(options["--sync"], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out sync)) throw new ArgumentException(string.Format("Could not parse {0} as sync value.", options["--sync"]));
				if (inputFile == null) throw new ArgumentException("Input filename not specified.");
			} catch (Exception ex) {
				Console.Error.WriteLine("{0}: {1}", assemblyName.Name, ex.Message);
				Console.Error.WriteLine("{0}: Use '{0} --help' to show help.", assemblyName.Name);
				return 1;
			}

			// Load the tap file from the input.
			byte[] tapeBytes;

			// Are we reading a tape file?
			bool readTapeFile;
			if (options.ContainsKey("--type")) {
				readTapeFile = options["--type"] == "tap";
			} else {
				readTapeFile = Path.GetExtension(inputFile).ToLowerInvariant() == ".tap";
			}

			try {

				// Read the raw data.
				var data = File.ReadAllBytes(inputFile);

				if (readTapeFile) {
					// Validate the tap data.
					var length = (data[0] << 8) | data[1];
					if (length != data.Length - (2 + 3)) throw new InvalidDataException("Length prefix on input tap file does not match actual data length.");

					// Calculate the checksum.
					int calculatedChecksum = 0;
					for (int i = 0; i < length; ++i) {
						calculatedChecksum += data[2 + i];
					}
					calculatedChecksum &= 0xFFFFFF;

					// What was the received checksum?
					int receivedChecksum = (data[data.Length - 3] << 16) | (data[data.Length - 2] << 8) | data[data.Length - 1];

					// Do they match?
					if (calculatedChecksum != receivedChecksum) throw new InvalidDataException("Stored checksum does not match calculated checksum.");

					tapeBytes = data;
				} else {
					// Wrap the raw data in a tap file.
					tapeBytes = new byte[2 + data.Length + 1 + 3];
					// Length prefix.
					tapeBytes[0] = (byte)((data.Length + 1) >> 8);
					tapeBytes[1] = (byte)(data.Length + 1);
					// Actual data.
					Array.Copy(data, 0, tapeBytes, 2, data.Length);
					// Terminator.
					tapeBytes[2 + data.Length] = 0xFF;
					// Compute checksum.
					int checksum = 0xFF; // Include 0xFF terminator (not in the source file).
					for (int i = 0; i < data.Length; ++i) {
						checksum += data[i];
					}
					checksum &= 0xFFFFFF;
					// Append checksum.
					tapeBytes[tapeBytes.Length - 3] = (byte)(checksum >> 16);
					tapeBytes[tapeBytes.Length - 2] = (byte)(checksum >> 8);
					tapeBytes[tapeBytes.Length - 1] = (byte)checksum;
				}

			} catch (Exception ex) {
				if (!options.ContainsKey("--quiet")) Console.Error.WriteLine("{0}: Could not read input file - {1}", assemblyName.Name, ex.Message);
				return 1;
			}

			// What's the output filename?
			if (outputFile == null) {
				outputFile = Path.GetFileNameWithoutExtension(inputFile);
				if (options.ContainsKey("--tap")) {
					outputFile += ".tap";
				} else {
					outputFile += ".wav";
				}
			}

			// Are we writing a tap file?
			bool writeTapeFile;
			if (options.ContainsKey("--tap")) {
				writeTapeFile = true;
			} else {
				writeTapeFile = Path.GetExtension(outputFile).ToLowerInvariant() == ".tap";
			}

			// Write the data.
			try {
				using (var file = File.Create(outputFile)) {
					using (var writer = new BinaryWriter(file)) {
						if (writeTapeFile) {
							// Tape files are easy, just dump the raw data to disk.
							writer.Write(tapeBytes);
							
							if (!options.ContainsKey("--quiet")) {
								Console.WriteLine("Read {0} bytes from {1} and wrote {2} bytes to {3}.", tapeBytes.Length - (readTapeFile ? 0 : 6), Path.GetFileName(inputFile), tapeBytes.Length, Path.GetFileName(outputFile));
							}

						} else {
							// Time to write a WAV.

							// Similar format to files generated by PocketTools.
							var channelCount = 1;
							var sampleRate = 20000;
							var bitsPerSample = 8;
							var baudRate = 2500;
							var reversePhase = false;
							var squareWave = sampleRate / baudRate <= 8;

							// Compute wave cycles for 0 bits and 1 bits.
							var cycleSampleCount = sampleRate / baudRate;
							var bits = new byte[2][]; // 0 bits and 1 bits.

							for (int b = 0; b < 2; ++b) {
								bits[b] = new byte[cycleSampleCount * bitsPerSample / 8 * channelCount];
							}

							for (int c = 0; c < cycleSampleCount * channelCount; ++c) {
								// Angle of the wave.
								double a = (((c / channelCount) + (1.0d / cycleSampleCount)) * Math.PI * 2.0d) / cycleSampleCount;

								// 0 bit then 1 bit.
								for (int b = 0; b < 2; ++b) {

									// Angle is doubled (=frequency is doubled) for 1 bits.
									double v = Math.Sin(a * (1.0d + b));
									if (reversePhase) v = -v;

									// Values picked to match PocketTools.
									if (squareWave) v = v > 0 ? +0.706d : -0.705d;

									// Convert to the appropriate bit representation for the bit depth.
									switch (bitsPerSample) {
										case 8:
											bits[b][c] = (byte)Math.Round(Math.Max(byte.MinValue, Math.Min(byte.MaxValue, 127.5d + 127.5d * v)));
											break;
										case 16:
											short vs = (short)Math.Round(Math.Max(short.MinValue, Math.Min(short.MaxValue, (short.MaxValue + 0.5d) * v)));
											bits[b][c * 2 + 0] = (byte)(vs >> 0); bits[b][c * 2 + 1] = (byte)(vs >> 8);
											break;
									}
								}
							}

							// RIFF header
							writer.Write(Encoding.ASCII.GetBytes("RIFF")); // Chunk ID

							var riffDataSizePtr = file.Position;
							writer.Write((uint)0); // file size (we'll write this later)

							writer.Write(Encoding.ASCII.GetBytes("WAVE")); // RIFF type ID

							// Chunk 1 (format)
							writer.Write(Encoding.ASCII.GetBytes("fmt ")); // Chunk ID
							writer.Write((uint)16); // Chunk 1 size
							writer.Write((ushort)1); // Format tag
							writer.Write((ushort)channelCount); // Channel count
							writer.Write((uint)sampleRate); // Sample rate
							writer.Write((uint)(sampleRate * channelCount * bitsPerSample / 8)); // Byte rate
							writer.Write((ushort)(channelCount * bitsPerSample / 8)); // Block align
							writer.Write((ushort)bitsPerSample); // Bits per sample

							// Chunk 2 (data)
							writer.Write(Encoding.ASCII.GetBytes("data")); // Chunk ID
							var waveDataSizePtr = file.Position;
							writer.Write((uint)0); // Wave size (we'll write this later)

							var waveDataStartPtr = file.Position;

							// Initial leader (sync)
							for (int i = 0; i < Math.Ceiling(sampleRate * sync / cycleSampleCount); ++i) {
								writer.Write(bits[1]);
							}

							// Write each byte to the WAV
							for (int i = 0; i < tapeBytes.Length; ++i) {

								// Gap between bytes.
								writer.Write(bits[1]);
								writer.Write(bits[1]);

								// Start bit.
								writer.Write(bits[0]);

								// Eight data bits (LSB first).
								var b = tapeBytes[i];
								for (int j = 0; j < 8; ++j) {
									writer.Write(bits[b & 1]);
									b >>= 1;
								}

								// Stop bit.
								writer.Write(bits[1]);

							}

							// Update wave size
							var waveDataEndPtr = file.Position;

							file.Seek(waveDataSizePtr, SeekOrigin.Begin);
							writer.Write((uint)(waveDataEndPtr - waveDataStartPtr));

							// Update RIFF size
							file.Seek(riffDataSizePtr, SeekOrigin.Begin);
							writer.Write((uint)(waveDataEndPtr - 8));

							if (!options.ContainsKey("--quiet")) {
								var length = TimeSpan.FromSeconds((double)(waveDataEndPtr - waveDataStartPtr) / (double)(sampleRate * channelCount * bitsPerSample / 8));
								Console.WriteLine("Read {0} bytes from {1} and wrote {2} seconds of audio to {3}.", tapeBytes.Length - (readTapeFile ? 0 : 6), Path.GetFileName(inputFile), length.TotalSeconds, Path.GetFileName(outputFile));
							}
						}
					}
				}
			} catch (Exception ex) {
				if (!options.ContainsKey("--quiet")) Console.Error.WriteLine("{0}: Could not write output file - {1}", assemblyName.Name, ex.Message);
				return 1;
			}

			// All good!
			return 0;
		}


	}
}
