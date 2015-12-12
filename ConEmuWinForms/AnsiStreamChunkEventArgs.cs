using System;
using System.Text;

using JetBrains.Annotations;

namespace ConEmu.WinForms
{
	/// <summary>
	/// Holds the new chunk of the console ANSI stream.
	/// </summary>
	public class AnsiStreamChunkEventArgs : EventArgs
	{
		[NotNull]
		private readonly byte[] _chunk;

		public AnsiStreamChunkEventArgs([NotNull] byte[] chunk)
		{
			if(chunk == null)
				throw new ArgumentNullException(nameof(chunk));
			if(chunk.Length == 0)
				throw new ArgumentOutOfRangeException(nameof(chunk), chunk, "Empty data is not a valid stream chunk.");
			_chunk = chunk;
		}

		/// <summary>
		/// Gets the raw bytes of the chunk.
		/// </summary>
		[NotNull]
		public byte[] Chunk
		{
			get
			{
				return _chunk;
			}
		}

		/// <summary>
		/// Assuming the stream is encoded in the current system's MBCS encoding, gets its text.
		/// </summary>
		public string GetMbcsText()
		{
			return Encoding.Default.GetString(_chunk);
		}

		/// <summary>
		/// Gets the text of the chunk assuming it's in the specific encoding.
		/// </summary>
		public string GetText([NotNull] Encoding encoding)
		{
			if(encoding == null)
				throw new ArgumentNullException(nameof(encoding));
			return encoding.GetString(_chunk);
		}

		public override string ToString()
		{
			try
			{
				return $"({_chunk.Length:N0} bytes) {GetMbcsText()}";
			}
			catch(Exception ex)
			{
				return $"({_chunk.Length:N0} bytes) Error getting chunk text. {ex.Message}";
			}
		}
	}
}