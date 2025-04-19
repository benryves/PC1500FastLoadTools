using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PC1500FastLoadTools.FWav2Bin {
	
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
				new KeyValuePair<string, bool>("--tap", false),
				new KeyValuePair<string, bool>("--version", false),
				new KeyValuePair<string, bool>("--help", false),
			};

			// Full option name, abbreviated option name.
			var abbreviatedOptions = new[] {
				new KeyValuePair<string, string>("--type", "-t"),
				new KeyValuePair<string, string>("--quiet", "-q"),
			};

			// Parse arguments
			Dictionary<string, string> options;
			string inputFile;
			string outputFile;

			try {
				Arguments.Parse(args, availableOptions, abbreviatedOptions, out options, out inputFile, out outputFile);
				// Check the supplied options/arguments.
				if (options.ContainsKey("--type") && options["--type"] != "img" && options["--type"] != "bin" && options["--type"] != "tap") throw new ArgumentException(string.Format("{0} is not a valid option for the output file type.", options["--type"]));
				if (inputFile == null) throw new ArgumentException("Input filename not specified.");
			} catch (Exception ex) {
				Console.Error.WriteLine("{0}: {1}", assemblyName.Name, ex.Message);
				Console.Error.WriteLine("{0}: Use '{0} --help' to show help.", assemblyName.Name);
				return 1;
			}

			// Should we display the help information?
			if (options.ContainsKey("--help")) {
				var name = assembly.GetName();
				Console.WriteLine("Usage: {0} [Options] SrcFile(.wav/.tap) [DstFile(.typ)]", name.Name);
				return 0;
			}

			// Should we display the version information?
			if (options.ContainsKey("--version")) {
				Console.WriteLine("{0} version {1}", assemblyName.Name, assemblyName.Version);
				var copyright = (AssemblyCopyrightAttribute[])assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
				if (copyright.Length > 0) Console.WriteLine(copyright[0].Copyright);
				return 0;
			}

			// Are we reading a tap file?
			var readTapeFile = options.ContainsKey("--tap") || Path.GetExtension(inputFile).ToLowerInvariant() == ".tap";

			// Read the input file to a set of tape bytes.
			byte[] tapeBytes;
			try {
				using (var file = File.OpenRead(inputFile)) {
					if (readTapeFile) {
						tapeBytes = GetTapeBytesFromTape(file);
					} else {
						tapeBytes = GetTapeBytesFromWave(file);
					}
				}
			} catch (Exception ex) {
				if (!options.ContainsKey("--quiet")) Console.Error.WriteLine("{0}: {1}", assemblyName.Name, ex.Message);
				return 1;
			}

			// What's the output filename?
			if (outputFile == null) {
				outputFile = Path.GetFileNameWithoutExtension(inputFile);
				if (options.ContainsKey("--type")) {
					outputFile += "." + options["--type"];
				} else {
					outputFile += ".img";
				}
			}

			// Are we writing a tap file?
			bool writeTapeFile;
			if (options.ContainsKey("--type")) {
				writeTapeFile = options["--type"] == "tap";
			} else {
				writeTapeFile = Path.GetExtension(outputFile).ToLowerInvariant() == ".tap";
			}

			// Write the output data.
			try {
				using (var file = File.Create(outputFile)) {
					using (var writer = new BinaryWriter(file)) {
						if (writeTapeFile) {
							// Write a raw tape file.
							writer.Write(tapeBytes);
						} else {
							// Write the attached data only.
							writer.Write(tapeBytes, 2, tapeBytes.Length - 6);
						}
					}
				}
			} catch (Exception ex) {
				if (!options.ContainsKey("--quiet")) Console.Error.WriteLine("{0}: {1}", assemblyName.Name, ex.Message);
				return 1;
			}

			// All good!
			if (!options.ContainsKey("--quiet")) {
				Console.WriteLine("Read {0} bytes from {1} and wrote {2} bytes to {3}.", tapeBytes.Length, Path.GetFileName(inputFile), tapeBytes.Length - (writeTapeFile ? 0 : 6), Path.GetFileName(outputFile));
			}

			return 0;
		}

		static byte[] GetTapeBytesFromTape(Stream file) {
			using (var reader = new BinaryReader(file)) {
				var data = reader.ReadBytes(checked((int)file.Length));

				if (data.Length >= 5) {

					// Check the length prefix.
					var length = (ushort)((data[0] << 8) | data[1]);
					if (data.Length != length + 2 + 3) throw new InvalidDataException("Data length does not match amount of data in file.");

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

					// Yes, so return all bytes.
					return data;
				}
			}

			throw new InvalidDataException("Could not extract data from the tape file.");
		}

		static byte[] GetTapeBytesFromWave(Stream file) {

			using (var reader = new WaveCycleReader(file)) {

				var highFrequency = 5000;
				var lowFrequency = highFrequency / 2;
				var midFrequency = (highFrequency + lowFrequency) / 2;

				while (reader.SamplePosition < reader.SampleCount) {
					switch (reader.ReadWaveCycle(out int frequency)) {
						case WaveCycleType.StartData:
							bool readingData = true;
							var totalByteCycleCount = 0;
							byte workingByte = 0;
							var data = new List<byte>();
							while (readingData && reader.SamplePosition < reader.SampleCount) {
								switch (reader.ReadWaveCycle(out frequency)) {
									case WaveCycleType.WaveCycle:
										if (totalByteCycleCount == 0) {
											// Still waiting for the start bit.
											if (frequency < midFrequency) {
												// Got the start bit!
												totalByteCycleCount = 2;
											} else {
												// Still in the pilot tone...
											}
										} else {
											// General data bit.
											if (frequency < midFrequency) {
												// It's a full 0 bit.
												if ((totalByteCycleCount & 1) != 0) throw new InvalidDataException("Received a 0 bit in the middle of a 1 bit.");
												totalByteCycleCount += 2;
												workingByte >>= 1;
											} else {
												// It's half of a 1 bit.
												++totalByteCycleCount;
												if ((totalByteCycleCount & 1) == 0) {
													workingByte >>= 1;
													workingByte |= 0x80;
												}
											}

											// Have we received all cycles yet?
											if (totalByteCycleCount == 18) {
												data.Add(workingByte);
												totalByteCycleCount = 0;
											}
										}
										break;
									case WaveCycleType.StartData:
										throw new InvalidOperationException();
									case WaveCycleType.EndData:
									case WaveCycleType.EndFile:
										if (totalByteCycleCount != 0) throw new InvalidDataException("Received partial byte.");
										readingData = false;
										break;
								}
							}

							// Was there any attached data?
							if (data.Count >= 5) {

								// Check the length prefix.
								var length = (ushort)((data[0] << 8) | data[1]);
								if (data.Count != length + 2 + 3) throw new InvalidDataException("Data length does not match amount of data received.");

								// Calculate the checksum.
								int calculatedChecksum = 0;
								for (int i = 0; i < length; ++i) {
									calculatedChecksum += data[2 + i];
								}
								calculatedChecksum &= 0xFFFFFF;

								// What was the received checksum?
								int receivedChecksum = (data[data.Count - 3] << 16) | (data[data.Count - 2] << 8) | data[data.Count - 1];

								// Do they match?
								if (calculatedChecksum != receivedChecksum) throw new InvalidDataException("Received checksum does not match calculated checksum.");

								// Yes, so return all bytes.
								return data.ToArray();
							}
							break;
						case WaveCycleType.WaveCycle:
							throw new InvalidOperationException("Received a wave cycle outside a data block.");
						case WaveCycleType.EndData:
							throw new InvalidOperationException("Received an end of data cycle outside a data block.");
						case WaveCycleType.EndFile:
							break;
					}
				}
			}
			throw new InvalidDataException("Could not extract data from the wave file.");
		}
	}
}
