using System.Activities;

namespace Autossential.Activities.Tests.Activities
{
    public class UpdateDictionaryTests
    {
        private static void Invoke<TKey, TValue>(Dictionary<TKey, TValue> dictionary, Dictionary<TKey, TValue> entries)
        {
            WorkflowInvoker.Invoke(new UpdateDictionary<TKey, TValue>(), new Dictionary<string, object>
            {
                { "Dictionary", dictionary },
                { "Entries", entries }
            });
        }

        [Test]
        public async Task NewKeys_AddsEntriesToDictionary()
        {
            var dict = new Dictionary<string, int> { ["a"] = 1 };
            Invoke(dict, new Dictionary<string, int> { ["b"] = 2, ["c"] = 3 });

            await Assert.That(dict.Count).IsEqualTo(3);
            await Assert.That(dict["b"]).IsEqualTo(2);
            await Assert.That(dict["c"]).IsEqualTo(3);
        }

        [Test]
        public async Task ExistingKeys_OverwritesValues()
        {
            var dict = new Dictionary<string, string> { ["x"] = "old" };
            Invoke(dict, new Dictionary<string, string> { ["x"] = "new" });

            await Assert.That(dict.Count).IsEqualTo(1);
            await Assert.That(dict["x"]).IsEqualTo("new");
        }

        [Test]
        public async Task MixedKeys_AddsAndOverwritesCorrectly()
        {
            var dict = new Dictionary<string, int> { ["keep"] = 1, ["overwrite"] = 2 };
            Invoke(dict, new Dictionary<string, int> { ["overwrite"] = 99, ["new"] = 3 });

            await Assert.That(dict["keep"]).IsEqualTo(1);
            await Assert.That(dict["overwrite"]).IsEqualTo(99);
            await Assert.That(dict["new"]).IsEqualTo(3);
        }

        [Test]
        public async Task NullEntries_LeaveDictionaryUnchanged()
        {
            var dict = new Dictionary<string, int> { ["a"] = 1 };
            Invoke(dict, null);

            await Assert.That(dict.Count).IsEqualTo(1);
            await Assert.That(dict["a"]).IsEqualTo(1);
        }

        [Test]
        public async Task EmptyEntries_LeaveDictionaryUnchanged()
        {
            var dict = new Dictionary<string, int> { ["a"] = 1 };
            Invoke(dict, []);

            await Assert.That(dict.Count).IsEqualTo(1);
        }

        [Test]
        public async Task IntKeys_WorksCorrectly()
        {
            var dict = new Dictionary<int, string> { [1] = "one" };
            Invoke(dict, new Dictionary<int, string> { [2] = "two", [1] = "ONE" });

            await Assert.That(dict[1]).IsEqualTo("ONE");
            await Assert.That(dict[2]).IsEqualTo("two");
        }

        [Test]
        public async Task NullDictionary_ThrowsInvalidOperationException()
        {
            await Assert.That(() =>
                Invoke<string, int>(null, new Dictionary<string, int> { ["a"] = 1 }))
                .Throws<InvalidOperationException>();
        }
    }
}