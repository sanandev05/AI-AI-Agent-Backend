using System;
using System.Collections.Generic;
using AI_AI_Agent.Domain.Helpers;
using Xunit;

namespace AI.Agent.Tests.Domain.Helpers
{
    public class StreamTextAssemblerTests
    {
        [Fact]
        public void AssembleChunks_DeduplicatesConsecutiveIdenticalChunks()
        {
            // Arrange: Duplicate chunks that appear in both stream and final message
            var chunks = new[] { "The", "The", " query", " query" };

            // Act
            var result = StreamTextAssembler.AssembleChunks(chunks);

            // Assert: Should deduplicate to "The query"
            Assert.Equal("The query", result);
        }

        [Fact]
        public void AssembleChunks_AddsSpacesBetweenTokens()
        {
            // Arrange: Token-by-token streaming without spaces
            var chunks = new[] { "This", "is", "a", "test" };

            // Act
            var result = StreamTextAssembler.AssembleChunks(chunks);

            // Assert: Should add spaces between tokens
            Assert.Equal("This is a test", result);
        }

        [Fact]
        public void AssembleChunks_PreservesExistingSpaces()
        {
            // Arrange: Chunks that already include spaces
            var chunks = new[] { "Hello ", "world" };

            // Act
            var result = StreamTextAssembler.AssembleChunks(chunks);

            // Assert: Should not add extra space
            Assert.Equal("Hello world", result);
        }

        [Fact]
        public void AssembleChunks_HandlesLeadingSpaces()
        {
            // Arrange: Chunks with leading spaces
            var chunks = new[] { "Hello", " world", " today" };

            // Act
            var result = StreamTextAssembler.AssembleChunks(chunks);

            // Assert: Should preserve leading spaces in chunks
            Assert.Equal("Hello world today", result);
        }

        [Fact]
        public void AssembleChunks_HandlesTrailingSpaces()
        {
            // Arrange: Chunks with trailing spaces
            var chunks = new[] { "Hello ", "world ", "today" };

            // Act
            var result = StreamTextAssembler.AssembleChunks(chunks);

            // Assert: Should handle trailing spaces correctly
            Assert.Equal("Hello world today", result);
        }

        [Fact]
        public void AssembleChunks_DoesNotAddSpaceBeforePunctuation()
        {
            // Arrange: Punctuation should attach to previous word
            var chunks = new[] { "Hello", "world", "!" };

            // Act
            var result = StreamTextAssembler.AssembleChunks(chunks);

            // Assert: No space before !
            Assert.Equal("Hello world!", result);
        }

        [Fact]
        public void AssembleChunks_HandlesSentenceWithPunctuation()
        {
            // Arrange: Complete sentence with various punctuation
            var chunks = new[] { "Hello", ",", " ", "how", " ", "are", " ", "you", "?" };

            // Act
            var result = StreamTextAssembler.AssembleChunks(chunks);

            // Assert: Proper spacing around punctuation
            Assert.Equal("Hello, how are you?", result);
        }

        [Fact]
        public void AssembleChunks_IgnoresNullAndEmptyChunks()
        {
            // Arrange: Mix of valid and invalid chunks
            var chunks = new[] { "Hello", null!, "", "world", null!, "!" };

            // Act
            var result = StreamTextAssembler.AssembleChunks(chunks!);

            // Assert: Should skip null/empty chunks
            Assert.Equal("Hello world!", result);
        }

        [Fact]
        public void AssembleChunks_HandlesComplexDuplication()
        {
            // Arrange: Complex pattern with multiple duplications
            var chunks = new[] { "The", "The", " ", " ", "quick", "quick", " ", "fox" };

            // Act
            var result = StreamTextAssembler.AssembleChunks(chunks);

            // Assert: Should deduplicate all consecutive duplicates
            Assert.Equal("The quick fox", result);
        }

        [Fact]
        public void Append_DeduplicatesStreamPlusFinalDoubleAppend()
        {
            // Arrange: Simulate common LLM streaming bug
            var assembler = new StreamTextAssembler();

            // Act: Stream chunks, then final message duplicates last chunk
            assembler.Append("Hello");
            assembler.Append(" ");
            assembler.Append("world");
            assembler.Append("world"); // Duplicate from final message

            // Assert: Should not duplicate "world"
            Assert.Equal("Hello world", assembler.GetText());
        }

        [Fact]
        public void GetTextAndReset_ClearsState()
        {
            // Arrange
            var assembler = new StreamTextAssembler();
            assembler.Append("Hello");
            assembler.Append("world");

            // Act
            var firstResult = assembler.GetTextAndReset();
            assembler.Append("New");
            assembler.Append("text");
            var secondResult = assembler.GetText();

            // Assert
            Assert.Equal("Hello world", firstResult);
            Assert.Equal("New text", secondResult);
        }

        [Fact]
        public void Reset_ClearsAllState()
        {
            // Arrange
            var assembler = new StreamTextAssembler();
            assembler.Append("Hello");
            assembler.Append("Hello"); // Would be skipped due to dedup

            // Act
            assembler.Reset();
            assembler.Append("Hello"); // Should not be deduplicated after reset

            // Assert
            Assert.Equal("Hello", assembler.GetText());
        }

        [Fact]
        public void AssembleChunks_HandlesRealWorldLLMStreamingPattern()
        {
            // Arrange: Realistic LLM streaming pattern with duplicates and spacing issues
            var chunks = new[] 
            { 
                "I", 
                " can",
                " can",  // Duplicate
                " help",
                " help", // Duplicate
                " you",
                " with",
                " that",
                ".",
                "." // Duplicate punctuation
            };

            // Act
            var result = StreamTextAssembler.AssembleChunks(chunks);

            // Assert: Should produce clean sentence
            Assert.Equal("I can help you with that.", result);
        }

        [Fact]
        public void AssembleChunks_HandlesMultipleSentences()
        {
            // Arrange: Multiple sentences with proper spacing
            var chunks = new[] 
            { 
                "First",
                " sentence",
                ".",
                " ",
                "Second",
                " sentence",
                "."
            };

            // Act
            var result = StreamTextAssembler.AssembleChunks(chunks);

            // Assert
            Assert.Equal("First sentence. Second sentence.", result);
        }

        [Fact]
        public void AssembleChunks_HandlesQuotesAndParentheses()
        {
            // Arrange: Text with quotes and parentheses (realistic streaming pattern where quotes come attached)
            var chunks = new[]
            {
                "He",
                " said",
                ",",
                " \"Hello\"",
                " (loudly)"
            };

            // Act
            var result = StreamTextAssembler.AssembleChunks(chunks);

            // Assert: Punctuation should be preserved as streamed
            Assert.Equal("He said, \"Hello\" (loudly)", result);
        }

        [Fact]
        public void AssembleChunks_EmptyInput_ReturnsEmpty()
        {
            // Arrange
            var chunks = Array.Empty<string>();

            // Act
            var result = StreamTextAssembler.AssembleChunks(chunks);

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void GetText_DoesNotModifyState()
        {
            // Arrange
            var assembler = new StreamTextAssembler();
            assembler.Append("Hello");

            // Act
            var first = assembler.GetText();
            var second = assembler.GetText();

            // Assert: Both calls should return the same result
            Assert.Equal("Hello", first);
            Assert.Equal("Hello", second);
        }
    }
}
