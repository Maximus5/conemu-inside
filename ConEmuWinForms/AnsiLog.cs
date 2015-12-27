using System;
using System.IO;

using JetBrains.Annotations;

namespace ConEmu.WinForms
{
	/// <summary>
	/// Manages reading the ANSI log output of the conemu and firing events with its data to the user.
	/// </summary>
	class AnsiLog : IDisposable
	{
		/// <summary>
		/// When we found the ansi file.
		/// </summary>
		[CanBeNull]
		private FileStream _fstream;

		private bool _isDisposed;

		public AnsiLog([NotNull] DirectoryInfo directory)
		{
			if(directory == null)
				throw new ArgumentNullException(nameof(directory));
			Directory = directory;

			// Ensure exists for conemu to create the log file in
			directory.Create();
		}

		/// <summary>
		/// The directory into which we instruct ConEmu to write its ansi log.
		/// </summary>
		[NotNull]
		public readonly DirectoryInfo Directory;

		public void Dispose()
		{
			// If dispose is not called by owner: not critical, even if the file is open, it would close itself upon finalization
			// Multiple calls OK

			// Must pump out the rest of the stream, if there's something left
			PumpStream();

			// AFTER the final pumpout
			_isDisposed = true;

			// Close the file if open
			_fstream?.Dispose();
			_fstream = null;
		}

		/// <summary>
		/// A function which processes the part of the stream which gets available (or does the rest of it at the end).
		/// </summary>
		public void PumpStream()
		{
			if(_isDisposed)
				return; // Nonfiring, yep

			// Try acquiring the stream if not yet
			_fstream = _fstream ?? FindAnsiLogFile();

			// No ANSI stream file (yet?)
			if(_fstream == null)
				return;

			// Read the available chunk
			long length = _fstream.Length; // Take the length and keep it for the rest of the iteration, might change right as we're reading
			if(_fstream.Position >= length)
				return; // Nothing new
			var buffer = new byte[length - _fstream.Position]; // TODO: buffer pooling to save mem traffic
			int nRead = _fstream.Read(buffer, 0, buffer.Length);
			if(nRead <= 0)
				return; // Hmm should have succeeded, but anyway

			// Make a smaller buffer if we got less data (unlikely)
			AnsiStreamChunkEventArgs args;
			if(nRead < buffer.Length)
			{
				var subbuffer = new byte[nRead];
				Buffer.BlockCopy(buffer, 0, subbuffer, 0, nRead);
				args = new AnsiStreamChunkEventArgs(subbuffer);
			}
			else
				args = new AnsiStreamChunkEventArgs(buffer);

			// Fire
			AnsiStreamChunkReceived?.Invoke(this, args);
		}

		/// <summary>
		///     <para>Fires when the console process writes into its output or error stream. Gets a chunk of the raw ANSI stream contents.</para>
		/// </summary>
		public event EventHandler<AnsiStreamChunkEventArgs> AnsiStreamChunkReceived;

		[CanBeNull]
		private FileStream FindAnsiLogFile()
		{
			// NOTE: it's theoretically possible to get ANSI log file path with “BeginGuiMacro("GetInfo").WithParam("AnsiLog")”
			// However, this does not provide reliable means for reading the full ANSI output for short-lived processes, we might be too late to ask it for its path
			// So we specify a new empty folder for the log and expect the single log file to appear in that folder; we'll catch that just as good even after the process exits

			foreach(FileInfo fiLog in Directory.GetFiles("ConEmu*.log"))
				return fiLog.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

			return null;
		}
	}
}