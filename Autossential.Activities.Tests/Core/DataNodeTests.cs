using Autossential.Activities.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Autossential.Activities.Tests.Core
{
    public class DataNodeTests
    {
        // ─── Construction & NodeType ─────────────────────────────────────────────

        [Test]
        [Arguments(null)]
        [Arguments("hello")]
        [Arguments(42)]
        [Arguments(true)]
        [Arguments(3.14)]
        public async Task Constructor_ScalarValues_YieldScalarType(object value)
        {
            var node = new DataNode(value);
            await Assert.That(node.Type).IsEqualTo(NodeType.Scalar);
        }

        [Test]
        public async Task Constructor_Dictionary_YieldsMapType()
        {
            var node = new DataNode(new Dictionary<string, object> { ["k"] = 1 });
            await Assert.That(node.Type).IsEqualTo(NodeType.Map);
        }

        [Test]
        public async Task Constructor_List_YieldsSequenceType()
        {
            var node = new DataNode(new List<object> { 1, 2 });
            await Assert.That(node.Type).IsEqualTo(NodeType.Sequence);
        }

        [Test]
        public async Task Constructor_DataNodeValue_UnwrapsRawValue()
        {
            var inner = new DataNode("unwrapped");
            var node = new DataNode(inner);
            await Assert.That(node.RawValue).IsEqualTo("unwrapped");
        }

        [Test]
        public async Task DefaultConstructor_CreatesEmptyMap()
        {
            var node = new DataNode();
            await Assert.That(node.Type).IsEqualTo(NodeType.Map);
            await Assert.That(node.Keys).IsEmpty();
        }

        [Test]
        public async Task Empty_CreatesEmptyMapWithCulture()
        {
            var culture = CultureInfo.GetCultureInfo("pt-BR");
            var node = DataNode.Empty(culture);
            await Assert.That(node.Type).IsEqualTo(NodeType.Map);
            await Assert.That(node.Culture).IsEqualTo(culture);
        }

        [Test]
        public async Task Constructor_NullCulture_FallsBackToInvariant()
        {
            var node = new DataNode("x", null);
            await Assert.That(node.Culture).IsEqualTo(CultureInfo.InvariantCulture);
        }

        // ─── Normalize: IDictionary / IEnumerable ────────────────────────────────

        [Test]
        public async Task Normalize_Hashtable_ConvertsToDictionaryStringObject()
        {
            var ht = new System.Collections.Hashtable { ["key"] = 99 };
            var node = new DataNode(ht);
            await Assert.That(node.Type).IsEqualTo(NodeType.Map);
            await Assert.That(node["key"].AsInt()).IsEqualTo(99);
        }

        [Test]
        public async Task Normalize_Array_ConvertsToSequence()
        {
            var node = new DataNode(new[] { "a", "b", "c" });
            await Assert.That(node.Type).IsEqualTo(NodeType.Sequence);
            await Assert.That(node.AsSequence<string>().Count).IsEqualTo(3);
        }

        // ─── HasValue / Exists ───────────────────────────────────────────────────

        [Test]
        [Arguments("value", true)]
        [Arguments(null, false)]
        public async Task HasValue_ReflectsNullness(object value, bool expected)
        {
            var node = new DataNode(value);
            await Assert.That(node.HasValue()).IsEqualTo(expected);
        }

        [Test]
        public async Task ContainsPath_Contains_ReturnsTrue()
        {
            var node = new DataNode(new Dictionary<string, object> { ["a"] = 1 });
            await Assert.That(node.HasNode("a")).IsTrue();
        }

        [Test]
        public async Task ContainsPath_Missing_ReturnsFalse()
        {
            var node = new DataNode(new Dictionary<string, object> { ["a"] = 1 });
            await Assert.That(node.HasNode("z")).IsFalse();
        }

        [Test]
        public async Task HasValue_KeyPath_FalseForNullEntry()
        {
            var node = new DataNode(new Dictionary<string, object> { ["x"] = null });
            await Assert.That(node.HasValue("x")).IsFalse();
        }

        // ─── Path Navigation ─────────────────────────────────────────────────────

        [Test]
        public async Task GetNode_DotSeparatedPath_ReturnsCorrectNode()
        {
            var node = new DataNode(new Dictionary<string, object>
            {
                ["a"] = new Dictionary<string, object> { ["b"] = "deep" }
            });
            await Assert.That(node.GetNode("a.b").AsString()).IsEqualTo("deep");
        }

        [Test]
        public async Task GetNode_IndexPath_ReturnsCorrectElement()
        {
            var node = new DataNode(new Dictionary<string, object>
            {
                ["items"] = new List<object> { "x", "y", "z" }
            });
            await Assert.That(node.GetNode("items[1]").AsString()).IsEqualTo("y");
        }

        [Test]
        public async Task GetNode_QuotedKey_NavigatesDottedKey()
        {
            var node = new DataNode(new Dictionary<string, object>
            {
                ["error.rate"] = 0.5
            });
            await Assert.That(node.GetNode("['error.rate']").AsDouble()).IsEqualTo(0.5);
        }

        [Test]
        public async Task GetNode_MissingPath_ReturnsNullScalar()
        {
            var node = new DataNode(new Dictionary<string, object> { ["a"] = 1 });
            var result = node.GetNode("missing");
            await Assert.That(result.Type).IsEqualTo(NodeType.Scalar);
            Assert.Null(result.RawValue);
        }

        [Test]
        public async Task GetNode_OutOfBoundsIndex_ReturnsNullScalar()
        {
            var node = new DataNode(new Dictionary<string, object>
            {
                ["list"] = new List<object> { 1 }
            });
            var result = node.GetNode("list[99]");
            Assert.Null(result.RawValue);
        }

        [Test]
        [Arguments("server.host")]
        [Arguments("SERVER.Host")]
        [Arguments("server.Security.ROLES.admin")]
        public async Task GetNode_InsensitiveCase_ReturnsValue(string keyPath)
        {
            var node = new DataNode(new Dictionary<string, object>
            {
                ["server"] = new Dictionary<string, object>
                {
                    ["host"] = "api.example.com",
                    ["port"] = 443,
                    ["protocols"] = new string[] { "https", "http2" },
                    ["features"] = new List<string> { "gzip", "caching", "rate-limiting" },
                    ["security"] = new Dictionary<string, object>
                    {
                        ["enabled"] = true,
                        ["allowedIPs"] = new string[] { "192.168.1.10", "10.0.0.5" },
                        ["roles"] = new Dictionary<string, object>
                        {
                            ["admin"] = new List<string> { "create", "delete", "update", "read" },
                            ["user"] = new List<string> { "read", "update" },
                            ["guest"] = new string[] { "read" }
                        }
                    }
                }
            });

            var result = node.GetNode(keyPath);
            Assert.NotNull(result);
        }

        [Test]
        public async Task Indexer_Set_WritesValueIntoMap()
        {
            var node = new DataNode();
            node["greeting"] = new DataNode("hello");
            await Assert.That(node["greeting"].AsString()).IsEqualTo("hello");
        }

        [Test]
        public async Task Indexer_Set_CreatesIntermediateMaps()
        {
            var node = new DataNode();
            node["a.b"] = new DataNode(42);
            await Assert.That(node["a.b"].AsInt()).IsEqualTo(42);
        }

        [Test]
        public async Task Assign_NewKey_CaseSensitive()
        {
            var node = new DataNode();
            node["A.B.C"] = new DataNode(100);
            await Assert.That(node.HasNode("a.B.c")).IsFalse();
            await Assert.That(node.HasNode("A.B.C")).IsTrue();
        }

        [Test]
        public async Task Assign_RawValue_DictionaryConvertedToCaseSensitive()
        {
            var node = new DataNode();
            node["A"] = new DataNode(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "B", 100 }
            });
            await Assert.That(node.HasNode("a.b")).IsFalse();
            await Assert.That(node.HasNode("A.B")).IsTrue();
        }

        // ─── Keys ────────────────────────────────────────────────────────────────

        [Test]
        public async Task Keys_MapNode_ReturnsAllKeys()
        {
            var node = new DataNode(new Dictionary<string, object> { ["x"] = 1, ["y"] = 2 });
            await Assert.That(node.Keys.OrderBy(x => x)).IsEquivalentTo(["x", "y"]);
        }

        [Test]
        public async Task Keys_NonMapNode_ReturnsEmpty()
        {
            var node = new DataNode("scalar");
            await Assert.That(node.Keys).IsEmpty();
        }

        // ─── Scalar Conversions ──────────────────────────────────────────────────

        [Test]
        [Arguments("42", 42)]
        [Arguments(42, 42)]
        [Arguments(42.3, 42)]
        [Arguments(42.9, 43)]
        public async Task AsInt_VariousInputs_Converts(object value, int expected)
        {
            await Assert.That(new DataNode(value).AsInt()).IsEqualTo(expected);
        }

        [Test]
        [Arguments("3.14")]
        [Arguments("not-a-number")]
        public async Task AsInt_InvalidInput_ThrowsOrReturnsDefault(string value)
        {
            var node = new DataNode(value);
            Assert.Throws<FormatException>(() => node.AsInt());
            await Assert.That(node.AsIntOrDefault(-1)).IsEqualTo(-1);
        }

        [Test]
        [Arguments("true", true)]
        [Arguments("1", true)]
        [Arguments("false", false)]
        [Arguments("0", false)]
        [Arguments(true, true)]
        public async Task AsBool_StringAndBoolInputs_Converts(object value, bool expected)
        {
            await Assert.That(new DataNode(value).AsBool()).IsEqualTo(expected);
        }

        [Test]
        public async Task AsBool_InvalidString_Throws()
        {
            Assert.Throws<FormatException>(() => new DataNode("yes").AsBool());
        }

        [Test]
        [Arguments("1.5", 1.5f)]
        [Arguments(1.5f, 1.5f)]
        public async Task AsFloat_Converts(object value, float expected)
        {
            await Assert.That(new DataNode(value).AsFloat()).IsEqualTo(expected);
        }

        [Test]
        [Arguments("1.5", 1.5)]
        [Arguments(1.5, 1.5)]
        public async Task AsDouble_Converts(object value, double expected)
        {
            await Assert.That(new DataNode(value).AsDouble()).IsEqualTo(expected);
        }

        [Test]
        [Arguments("3.14")]
        [Arguments("0")]
        public async Task AsDecimal_ValidString_Converts(string value)
        {
            var expected = decimal.Parse(value, CultureInfo.InvariantCulture);
            await Assert.That(new DataNode(value).AsDecimal()).IsEqualTo(expected);
        }

        [Test]
        public async Task AsString_NullValue_ReturnsNull()
        {
            Assert.Null(new DataNode(null).AsString());
        }

        [Test]
        public async Task AsStringOrDefault_NullValue_ReturnsDefault()
        {
            await Assert.That(new DataNode(null).AsStringOrDefault("default")).IsEqualTo("default");
        }

        [Test]
        public async Task AsDateTime_ValidString_Converts()
        {
            var dt = new DateTime(2024, 6, 1);
            var node = new DataNode(dt.ToString(CultureInfo.InvariantCulture));
            await Assert.That(node.AsDateTime().Date).IsEqualTo(dt.Date);
        }

        [Test]
        public async Task AsDateTimeOrDefault_Invalid_ReturnsDefault()
        {
            var fallback = new DateTime(2000, 1, 1);
            await Assert.That(new DataNode("not-a-date").AsDateTimeOrDefault(fallback)).IsEqualTo(fallback);
        }

        [Test]
        public async Task AsRegex_ValidPattern_ReturnsRegex()
        {
            var node = new DataNode(@"\d+");
            var regex = node.AsRegex();
            await Assert.That(regex.IsMatch("123")).IsTrue();
        }

        [Test]
        public async Task AsRegex_WithOptions_AppliesOptions()
        {
            var node = new DataNode("hello");
            var regex = node.AsRegex(RegexOptions.IgnoreCase);
            await Assert.That(regex.IsMatch("HELLO")).IsTrue();
        }

        [Test]
        public async Task AsRegexOrDefault_NullValue_ReturnsDefault()
        {
            var fallback = new Regex("fallback");
            var result = new DataNode(null).AsRegexOrDefault(fallback);
            await Assert.That(result).IsSameReferenceAs(fallback);
        }

        // ─── OrDefault shortcuts (null node) ────────────────────────────────────

        [Test]
        public async Task OrDefault_NullNode_ReturnsDefault()
        {
            var node = new DataNode(null);
            await Assert.That(node.AsIntOrDefault(99)).IsEqualTo(99);
            await Assert.That(node.AsLongOrDefault(99)).IsEqualTo(99L);
            await Assert.That(node.AsFloatOrDefault(9.9f)).IsEqualTo(9.9f);
            await Assert.That(node.AsDoubleOrDefault(9.9)).IsEqualTo(9.9);
            await Assert.That(node.AsDecimalOrDefault(9.9m)).IsEqualTo(9.9m);
            await Assert.That(node.AsBoolOrDefault(true)).IsTrue();
        }

        // ─── AsSequence ──────────────────────────────────────────────────────────

        [Test]
        public async Task AsSequence_IntList_ConvertsElements()
        {
            var node = new DataNode(new List<object> { 1, 2, 3 });
            var result = node.AsSequence<int>();
            await Assert.That(result).IsEquivalentTo([1, 2, 3]);
        }

        [Test]
        public async Task AsSequence_NonSequenceNode_Throws()
        {
            var node = new DataNode("scalar");
            Assert.Throws<InvalidOperationException>(() => node.AsSequence<string>());
        }

        [Test]
        public async Task AsSequenceOrDefault_NonSequenceNode_ReturnsDefault()
        {
            var fallback = new List<string> { "default" };
            var result = new DataNode("scalar").AsSequenceOrDefault(fallback);
            await Assert.That(result).IsSameReferenceAs(fallback);
        }

        [Test]
        public async Task AsSequence_DataNode_ConvertsElements()
        {
            var node = new DataNode(new Dictionary<string, object>
            {
                ["NotesArray"] = new string[] { "Fourth level", "Arrays included", "Mixed types" }
            });

            var result = node.AsSequence<DataNode>("NotesArray");
            await Assert.That(result.Count).IsEqualTo(3);
        }

        // ─── AsMap ───────────────────────────────────────────────────────────────

        [Test]
        public async Task AsMap_MapNode_ReturnsDictionary()
        {
            var node = new DataNode(new Dictionary<string, object> { ["k"] = "v" });
            var map = node.AsMap();
            await Assert.That(map["k"]).IsEqualTo("v");
        }

        [Test]
        public async Task AsMap_NonMapNode_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new DataNode("x").AsMap());
        }

        // ─── Merge ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Merge_NewKey_AddsToTarget()
        {
            var target = new DataNode(new Dictionary<string, object> { ["a"] = 1 });
            var source = new DataNode(new Dictionary<string, object> { ["b"] = 2 });
            target.Merge(source);
            await Assert.That(target["b"].AsInt()).IsEqualTo(2);
        }

        [Test]
        public async Task Merge_OverlappingScalar_SourceOverwrites()
        {
            var target = new DataNode(new Dictionary<string, object> { ["x"] = "old" });
            var source = new DataNode(new Dictionary<string, object> { ["x"] = "new" });
            target.Merge(source);
            await Assert.That(target["x"].AsString()).IsEqualTo("new");
        }

        [Test]
        public async Task Merge_NestedMap_MergesRecursively()
        {
            var target = new DataNode(new Dictionary<string, object>
            {
                ["outer"] = new Dictionary<string, object> { ["a"] = 1 }
            });
            var source = new DataNode(new Dictionary<string, object>
            {
                ["outer"] = new Dictionary<string, object> { ["b"] = 2 }
            });
            target.Merge(source);
            await Assert.That(target["outer.a"].AsInt()).IsEqualTo(1);
            await Assert.That(target["outer.b"].AsInt()).IsEqualTo(2);
        }

        [Test]
        public async Task Merge_NullSource_LeavesTargetUnchanged()
        {
            var target = new DataNode(new Dictionary<string, object> { ["a"] = 1 });
            target.Merge(null);
            await Assert.That(target["a"].AsInt()).IsEqualTo(1);
        }

        [Test]
        public async Task Merge_NonMapTarget_Throws()
        {
            var node = new DataNode("scalar");
            Assert.Throws<InvalidOperationException>(() =>
                node.Merge(new DataNode(new Dictionary<string, object>())));
        }

        // ─── ToString ────────────────────────────────────────────────────────────

        [Test]
        [Arguments("hello", "hello")]
        [Arguments(null, "(null)")]
        public async Task ToString_Scalar_ReturnsExpected(object value, string expected)
        {
            await Assert.That(new DataNode(value).ToString()).IsEqualTo(expected);
        }

        [Test]
        public async Task ToString_Sequence_ReturnsItemCount()
        {
            var node = new DataNode(new List<object> { 1, 2 });
            await Assert.That(node.ToString()).IsEqualTo("[Sequence, 2 items]");
        }

        [Test]
        public async Task ToString_Map_ReturnsKeyCount()
        {
            var node = new DataNode(new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 });
            await Assert.That(node.ToString()).IsEqualTo("[Map, 2 keys]");
        }

        // ─── Navigation Methods (partial class) ──────────────────────────────────

        [Test]
        public async Task NavigationMethods_AsInt_ReturnsValueAtPath()
        {
            var node = new DataNode(new Dictionary<string, object>
            {
                ["count"] = 7
            });
            await Assert.That(node.AsInt("count")).IsEqualTo(7);
            await Assert.That(node.AsIntOrDefault("missing", 0)).IsEqualTo(0);
        }

        [Test]
        public async Task NavigationMethods_AsSequence_ReturnsListAtPath()
        {
            var node = new DataNode(new Dictionary<string, object>
            {
                ["tags"] = new List<object> { "a", "b" }
            });
            await Assert.That(node.AsSequence<string>("tags")).IsEquivalentTo(["a", "b"]);
        }

        [Test]
        public async Task NavigationMethods_AsRegex_ReturnsRegexAtPath()
        {
            var node = new DataNode(new Dictionary<string, object>
            {
                ["pattern"] = @"\w+"
            });
            var regex = node.AsRegex("pattern");
            await Assert.That(regex.IsMatch("word")).IsTrue();
        }

        [Test]
        public async Task NavigationMethods_AsRegexOrDefault_MissingPath_ReturnsDefault()
        {
            var fallback = new Regex("fallback");
            var node = new DataNode(new Dictionary<string, object>());
            var result = node.AsRegexOrDefault("missing", fallback);
            await Assert.That(result).IsSameReferenceAs(fallback);
        }
    }
}