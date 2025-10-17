using System;
using System.Collections.Generic;
using System.Text;

namespace AI_AI_Agent.Domain.Helpers
{
    /// <summary>
    /// Assembles streaming text chunks into a single cleaned string.
    /// Handles deduplication of consecutive identical chunks and preserves proper spacing.
    /// </summary>
    public class StreamTextAssembler
    {
        private readonly StringBuilder _buffer = new();
        private string? _lastChunk = null;

        /// <summary>
        /// Append a chunk and return ONLY the new delta that wasn't already present
        /// at the end of the current buffer. This prevents stream+final double-append
        /// and overlapping boundary repeats (e.g., SCADA + ADA -> SCADA, not SCADAADA).
        /// No extra spaces are injected here; the chunk text is treated as authoritative
        /// for spacing. Use this in live streaming to emit clean incremental output.
        /// </summary>
        public string AppendAndGetDelta(string? chunk)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                return string.Empty;
            }

            // If provider sometimes sends cumulative content, compute the pure delta
            var current = _buffer.ToString();
            if (chunk.StartsWith(current, StringComparison.Ordinal))
            {
                var deltaCumulative = chunk.Substring(current.Length);
                if (deltaCumulative.Length > 0)
                {
                    _buffer.Append(deltaCumulative);
                }
                _lastChunk = chunk;
                return deltaCumulative;
            }

            // Deduplicate exact consecutive chunks
            if (chunk == _lastChunk)
            {
                return string.Empty;
            }

            // Compute longest overlap between buffer suffix and chunk prefix
            int overlap = GetSuffixPrefixOverlap(current, chunk);
            var delta = chunk.Substring(overlap);

            // If the first word in delta equals the last word in buffer, drop it
            var lastWord = GetLastWord(current);
            if (!string.IsNullOrEmpty(lastWord))
            {
                var (firstWord, firstWordSpan) = GetFirstWord(delta);
                if (!string.IsNullOrEmpty(firstWord) && string.Equals(firstWord, lastWord, StringComparison.OrdinalIgnoreCase))
                {
                    // Remove the leading whitespace + duplicate word span
                    delta = delta.Substring(firstWordSpan);
                }
            }

            if (delta.Length > 0)
            {
                _buffer.Append(delta);
            }

            _lastChunk = chunk;
            return delta;
        }

        /// <summary>
        /// Appends a chunk to the stream with automatic deduplication and spacing.
        /// 
        /// Strategy:
        /// 1. Skip duplicate consecutive chunks (e.g., ["The","The"," query"," query"] -> "The query")
        /// 2. Add space between chunks that don't start with space/punctuation
        /// 3. Never add space after whitespace or before punctuation
        /// </summary>
        /// <param name="chunk">The text chunk to append</param>
        public void Append(string? chunk)
        {
            // Skip null or empty chunks
            if (string.IsNullOrEmpty(chunk))
            {
                return;
            }

            // Deduplication: Skip if this chunk is identical to the last one
            // This handles the common LLM streaming bug where the same token
            // appears twice (once in stream, once in final message)
            if (chunk == _lastChunk)
            {
                return;
            }

            // Determine if we need to add a space before this chunk
            bool needsSpace = ShouldAddSpaceBefore(chunk);

            if (needsSpace && _buffer.Length > 0)
            {
                _buffer.Append(' ');
            }

            _buffer.Append(chunk);
            _lastChunk = chunk;
        }

        /// <summary>
        /// Determines if a space should be added before the chunk.
        /// 
        /// Rules:
        /// - Don't add space if buffer is empty
        /// - Don't add space if chunk starts with whitespace or punctuation
        /// - Don't add space if previous chunk ended with whitespace
        /// - Otherwise, add space to separate tokens
        /// </summary>
        private bool ShouldAddSpaceBefore(string chunk)
        {
            if (_buffer.Length == 0)
            {
                return false;
            }

            // Don't add space if chunk already starts with whitespace
            if (char.IsWhiteSpace(chunk[0]))
            {
                return false;
            }

            // Don't add space before common punctuation
            if (IsPunctuation(chunk[0]))
            {
                return false;
            }

            // Don't add space if previous content ended with whitespace
            if (_buffer.Length > 0 && char.IsWhiteSpace(_buffer[_buffer.Length - 1]))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a character is punctuation that shouldn't have space before it.
        /// Includes closing punctuation, commas, periods, etc.
        /// </summary>
        private static bool IsPunctuation(char c)
        {
            return c is '.' or ',' or '!' or '?' or ';' or ':' or ')' or ']' or '}' or '\'' or '"' 
                     or '(' or '[' or '{'; // Opening brackets also shouldn't have space before them when they're the first char of a chunk
        }

        /// <summary>
        /// Returns the size of the longest string which is a suffix of 'a' and a prefix of 'b'.
        /// </summary>
        private static int GetSuffixPrefixOverlap(string a, string b)
        {
            int max = Math.Min(a.Length, b.Length);
            for (int len = max; len > 0; len--)
            {
                if (a.EndsWith(b.Substring(0, len), StringComparison.Ordinal))
                {
                    return len;
                }
            }
            return 0;
        }

        /// <summary>
        /// Extracts the last alphanumeric word from the given text.
        /// </summary>
        private static string GetLastWord(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            int i = text.Length - 1;
            // Skip trailing non-word chars
            while (i >= 0 && !char.IsLetterOrDigit(text[i])) i--;
            if (i < 0) return string.Empty;
            int end = i;
            while (i >= 0 && char.IsLetterOrDigit(text[i])) i--;
            int start = i + 1;
            return text.Substring(start, end - start + 1);
        }

        /// <summary>
        /// Extracts the first alphanumeric word from the given text and returns the
        /// length to skip to remove it including any leading spaces/punctuation.
        /// </summary>
        private static (string word, int spanLength) GetFirstWord(string text)
        {
            if (string.IsNullOrEmpty(text)) return (string.Empty, 0);
            int i = 0;
            // include leading whitespace/punct in span
            while (i < text.Length && !char.IsLetterOrDigit(text[i])) i++;
            int start = i;
            while (i < text.Length && char.IsLetterOrDigit(text[i])) i++;
            int end = i;
            var word = start < end ? text.Substring(start, end - start) : string.Empty;
            return (word, end);
        }

        /// <summary>
        /// Returns the assembled text and resets the assembler.
        /// </summary>
        public string GetTextAndReset()
        {
            var result = _buffer.ToString().Trim();
            _buffer.Clear();
            _lastChunk = null;
            return result;
        }

        /// <summary>
        /// Returns the current assembled text without resetting.
        /// </summary>
        public string GetText()
        {
            return _buffer.ToString().Trim();
        }

        /// <summary>
        /// Resets the assembler to its initial state.
        /// </summary>
        public void Reset()
        {
            _buffer.Clear();
            _lastChunk = null;
        }

        /// <summary>
        /// Static utility method to assemble chunks in one call.
        /// </summary>
        public static string AssembleChunks(IEnumerable<string> chunks)
        {
            var assembler = new StreamTextAssembler();
            foreach (var chunk in chunks)
            {
                assembler.Append(chunk);
            }
            return assembler.GetText();
        }
    }
}
