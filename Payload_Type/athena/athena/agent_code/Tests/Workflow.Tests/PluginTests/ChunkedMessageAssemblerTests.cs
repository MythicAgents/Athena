using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class ChunkedMessageAssemblerTests
    {
        private ChunkedMessageAssembler _assembler;

        [TestInitialize]
        public void Setup()
        {
            _assembler = new ChunkedMessageAssembler();
        }

        [TestMethod]
        public void SingleChunkMessage_ReturnsComplete()
        {
            var result = _assembler.AddChunk(
                "guid-1", 0, "hello", true, out var msg);

            Assert.AreEqual(
                ChunkedMessageAssembler.Result.Complete, result);
            Assert.AreEqual("hello", msg);
        }

        [TestMethod]
        public void MultiChunkMessage_AssemblesCorrectly()
        {
            var r1 = _assembler.AddChunk(
                "guid-1", 0, "hel", false, out var m1);
            Assert.AreEqual(
                ChunkedMessageAssembler.Result.Accepted, r1);
            Assert.IsNull(m1);

            var r2 = _assembler.AddChunk(
                "guid-1", 1, "lo", true, out var m2);
            Assert.AreEqual(
                ChunkedMessageAssembler.Result.Complete, r2);
            Assert.AreEqual("hello", m2);
        }

        [TestMethod]
        public void OutOfOrderChunk_ReturnsOutOfOrder()
        {
            _assembler.AddChunk(
                "guid-1", 0, "chunk0", false, out _);

            var result = _assembler.AddChunk(
                "guid-1", 5, "chunk5", false, out var msg);

            Assert.AreEqual(
                ChunkedMessageAssembler.Result.OutOfOrder, result);
            Assert.IsNull(msg);
        }

        [TestMethod]
        public void OutOfOrderChunk_ClearsStateForGuid()
        {
            _assembler.AddChunk(
                "guid-1", 0, "chunk0", false, out _);
            _assembler.AddChunk(
                "guid-1", 5, "bad", false, out _);

            var result = _assembler.AddChunk(
                "guid-1", 0, "fresh", true, out var msg);

            Assert.AreEqual(
                ChunkedMessageAssembler.Result.Complete, result);
            Assert.AreEqual("fresh", msg);
        }

        [TestMethod]
        public void InterleavedMessages_TrackedSeparately()
        {
            _assembler.AddChunk("a", 0, "A0", false, out _);
            _assembler.AddChunk("b", 0, "B0", false, out _);
            _assembler.AddChunk("a", 1, "A1", true, out var msgA);
            _assembler.AddChunk("b", 1, "B1", true, out var msgB);

            Assert.AreEqual("A0A1", msgA);
            Assert.AreEqual("B0B1", msgB);
        }

        [TestMethod]
        public void Clear_DiscardsAllPartialMessages()
        {
            _assembler.AddChunk("a", 0, "partial", false, out _);
            _assembler.AddChunk("b", 0, "partial", false, out _);

            _assembler.Clear();

            var result = _assembler.AddChunk(
                "a", 0, "new", true, out var msg);

            Assert.AreEqual(
                ChunkedMessageAssembler.Result.Complete, result);
            Assert.AreEqual("new", msg);
        }

        [TestMethod]
        public void EmptyContent_AssemblesCorrectly()
        {
            var result = _assembler.AddChunk(
                "guid-1", 0, "", true, out var msg);

            Assert.AreEqual(
                ChunkedMessageAssembler.Result.Complete, result);
            Assert.AreEqual("", msg);
        }
    }
}
