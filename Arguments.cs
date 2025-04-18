using System;
using System.Collections.Generic;

namespace PC1500FastLoadTools {

	internal class Arguments {

		public static void Parse(string[] args, KeyValuePair<string, bool>[] availableOptions, KeyValuePair<string, string>[] abbreviatedOptions, out Dictionary<string, string> options, out string inputFile, out string outputFile) {
			
			// Initialse outputs.
			options = new Dictionary<string, string>();
			inputFile = null;
			outputFile = null;

			// Use a queue to handle the conversion.
			var argumentQueue = new Queue<string>(args);

			// Decode the options from the list of arguments.
			while (argumentQueue.Count > 0 && argumentQueue.Peek().StartsWith("-")) {

				var argument = argumentQueue.Dequeue();

				var foundOption = false;
				foreach (var availableOption in availableOptions) {
					if (argument.StartsWith(availableOption.Key)) {

						// We can only specify each option once.
						if (options.ContainsKey(availableOption.Key)) {
							throw new ArgumentException(string.Format("Option {0} has already been specified.", availableOption.Key));
						}

						// Remove the option name from the argument.
						var value = argument.Substring(availableOption.Key.Length).Trim();

						if (!availableOption.Value) {
							// The option doesn't take any values.
							if (value.Length > 0) {
								throw new ArgumentException(string.Format("Option {0} does not take a value.", availableOption.Key));
							} else {
								options.Add(availableOption.Key, null);
								foundOption = true;
							}
						} else {
							// We need an option to follow the value.
							if (availableOption.Key.StartsWith("--")) {
								// --option can be followed by an equals sign
								if (value.StartsWith("=")) {
									// --option with =
									value = value.Substring(1).Trim();
									if (value.Length > 0) {
										options.Add(availableOption.Key, value);
										foundOption = true;
									} else if (argumentQueue.Count > 0) {
										options.Add(availableOption.Key, argumentQueue.Dequeue());
										foundOption = true;
									} else {
										throw new ArgumentException(string.Format("Option {0} requires a value.", availableOption.Key));
									}
								} else {
									// --option without =
									if (value.Length > 0) {
										throw new ArgumentException(string.Format("Option {0} requires a space or equals sign before the value.", availableOption.Key));
									} else if (argumentQueue.Count > 0) {
										options.Add(availableOption.Key, argumentQueue.Dequeue());
										foundOption = true;
									} else {
										throw new ArgumentException(string.Format("Option {0} requires a value.", availableOption.Key));
									}
								}
							} else {
								// -o cannot be followed by an equals sign
								if (value.Length > 0) {
									options.Add(availableOption.Key, value);
									foundOption = true;
								} else if (argumentQueue.Count > 0) {
									options.Add(availableOption.Key, argumentQueue.Dequeue());
									foundOption = true;
								} else {
									throw new ArgumentException(string.Format("Option {0} requires a value.", availableOption.Key));
								}
							}
						}
					}

					// If we found the option, stop searching.
					if (foundOption) break;
				}

				// If we haven't found the option, flag it as an error.
				if (!foundOption) {
					throw new ArgumentException(string.Format("Unrecognised option {0}.", argument));
				}
			}

			// Check for any conflicting abbreviated options.
			foreach (var abbreviatedOption in abbreviatedOptions) {
				if (options.ContainsKey(abbreviatedOption.Key) && options.ContainsKey(abbreviatedOption.Value)) {
					// We have the same option twice.
					if (options[abbreviatedOption.Key] == options[abbreviatedOption.Value]) {
						// Just remove the duplicate.
						options.Remove(abbreviatedOption.Value);
					} else {
						// The same option twice, but differenet values.
						throw new ArgumentException(string.Format("Conflicting options specified for {0} and {1}.", abbreviatedOption.Key, abbreviatedOption.Value));
					}
				} else if (options.ContainsKey(abbreviatedOption.Value)) {
					// Just move the abbreviated option to the full option.
					options.Add(abbreviatedOption.Key, options[abbreviatedOption.Value]);
					options.Remove(abbreviatedOption.Value);
				}
			}

			// Pull the input and output filename from the argument queue.
			if (argumentQueue.Count > 0) inputFile = argumentQueue.Dequeue();
			if (argumentQueue.Count > 0) outputFile = argumentQueue.Dequeue();

			if (argumentQueue.Count > 0) {
				throw new ArgumentException("Too many arguments.");
			}
		}
	}
}
