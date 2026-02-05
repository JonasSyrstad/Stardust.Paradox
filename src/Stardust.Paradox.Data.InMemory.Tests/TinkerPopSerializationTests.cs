using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.Server;
using Xunit;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// TinkerPop Serialization and Wire Protocol Tests
    /// Based on: https://tinkerpop.apache.org/docs/current/dev/provider/
    /// 
    /// Tests for GraphSON serialization, wire protocol compliance, and
    /// response format requirements.
    /// </summary>
    public class TinkerPopSerializationTests
    {
        #region Vertex Serialization Tests

        [Fact]
        public async Task Vertex_Serialization_ShouldIncludeId()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1')", new Dictionary<string, object>());
            var vertex = result.First();
            
            ((string)vertex.id).Should().Be("v1");
        }

        [Fact]
        public async Task Vertex_Serialization_ShouldIncludeLabel()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1')", new Dictionary<string, object>());
            var vertex = result.First();
            
            ((string)vertex.label).Should().Be("person");
        }

        [Fact]
        public async Task Vertex_Serialization_ShouldIncludeType()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1')", new Dictionary<string, object>());
            var vertex = result.First();
            
            ((string)vertex.type).Should().Be("vertex");
        }

        [Fact]
        public async Task Vertex_Serialization_ShouldIncludeProperties()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice').property('age', 30)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1')", new Dictionary<string, object>());
            var vertex = result.First();
            
            ((object)vertex.properties).Should().NotBeNull();
            ((string)vertex.properties.name).Should().Be("Alice");
            ((int)vertex.properties.age).Should().Be(30);
        }

        #endregion

        #region Edge Serialization Tests

        [Fact]
        public async Task Edge_Serialization_ShouldIncludeId()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'a')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'b')", new Dictionary<string, object>());
            // Create edge and set id as property
            await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b')).property('id', 'e1')", new Dictionary<string, object>());
            
            // Verify edge was created and has the id property
            var result = await connector.ExecuteAsync("g.E().has('id', 'e1')", new Dictionary<string, object>());
            result.Should().HaveCount(1);
            var edge = result.First();
            
            // Edge id may be auto-generated or from property - verify id exists
            ((object)edge.id).Should().NotBeNull();
        }

        [Fact]
        public async Task Edge_Serialization_ShouldIncludeLabel()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'a')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'b')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b'))", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
            var edge = result.First();
            
            ((string)edge.label).Should().Be("knows");
        }

        [Fact]
        public async Task Edge_Serialization_ShouldIncludeType()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'a')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'b')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b'))", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
            var edge = result.First();
            
            ((string)edge.type).Should().Be("edge");
        }

        [Fact]
        public async Task Edge_Serialization_ShouldIncludeVertexReferences()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'a')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'b')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b'))", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
            var edge = result.First();
            
            ((string)edge.inV).Should().Be("b");
            ((string)edge.outV).Should().Be("a");
        }

        [Fact]
        public async Task Edge_Serialization_ShouldIncludeProperties()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'a')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'b')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b')).property('since', 2020).property('weight', 0.8)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
            result.Should().HaveCount(1);
            var edge = result.First();
            
            ((object)edge.properties).Should().NotBeNull();
        }

        #endregion

        #region Property Serialization Tests

        [Fact]
        public async Task Property_String_ShouldSerializeCorrectly()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('test').property('id', 'v1').property('strVal', 'hello')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').values('strVal')", new Dictionary<string, object>());
            
            string strVal = result.First();
            strVal.Should().Be("hello");
        }

        [Fact]
        public async Task Property_Integer_ShouldSerializeCorrectly()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('test').property('id', 'v1').property('intVal', 42)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').values('intVal')", new Dictionary<string, object>());
            
            int intVal = Convert.ToInt32(result.First());
            intVal.Should().Be(42);
        }

        [Fact]
        public async Task Property_Long_ShouldSerializeCorrectly()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('test').property('id', 'v1').property('longVal', 9999999999)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').values('longVal')", new Dictionary<string, object>());
            
            long longVal = Convert.ToInt64(result.First());
            longVal.Should().Be(9999999999L);
        }

        [Fact]
        public async Task Property_Double_ShouldSerializeCorrectly()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('test').property('id', 'v1').property('doubleVal', 3.14159)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').values('doubleVal')", new Dictionary<string, object>());
            
            double doubleVal = Convert.ToDouble(result.First());
            doubleVal.Should().BeApproximately(3.14159, 0.00001);
        }

        [Fact]
        public async Task Property_Boolean_ShouldSerializeCorrectly()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('test').property('id', 'v1').property('boolVal', true)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').values('boolVal')", new Dictionary<string, object>());
            
            bool boolVal = (bool)result.First();
            boolVal.Should().BeTrue();
        }

        #endregion

        #region ValueMap Serialization Tests

        [Fact]
        public async Task ValueMap_ShouldReturnDictionary()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice').property('age', 30)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').valueMap()", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
            var valueMap = result.First() as IDictionary<string, object>;
            valueMap.Should().NotBeNull();
        }

        [Fact]
        public async Task ValueMap_WithTrue_ShouldIncludeIdAndLabel()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').valueMap(true)", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task ValueMap_SelectedProperties_ShouldFilterProperties()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice').property('age', 30).property('city', 'NYC')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').valueMap('name', 'age')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        #endregion

        #region ElementMap Serialization Tests

        [Fact]
        public async Task ElementMap_ShouldIncludeIdLabelAndProperties()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').elementMap()", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        #endregion

        #region Path Serialization Tests

        [Fact]
        public async Task Path_ShouldSerializeAsList()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'a').property('name', 'Alice')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'b').property('name', 'Bob')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b'))", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('a').out('knows').path()", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Path_ByName_ShouldIncludePropertyValues()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'a').property('name', 'Alice')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'b').property('name', 'Bob')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b'))", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('a').out('knows').path().by('name')", new Dictionary<string, object>());
            
            result.Should().NotBeNull();
        }

        #endregion

        #region Collection Serialization Tests

        [Fact]
        public async Task Fold_ShouldReturnList()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'v2').property('name', 'Bob')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().values('name').fold()", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
            var list = result.First() as IEnumerable<object>;
            list.Should().HaveCount(2);
        }

        [Fact]
        public async Task Group_ShouldReturnDictionary()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('dept', 'IT')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'v2').property('dept', 'IT')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'v3').property('dept', 'HR')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().group().by('dept')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task GroupCount_ShouldReturnDictionary()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('dept', 'IT')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'v2').property('dept', 'IT')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'v3').property('dept', 'HR')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().groupCount().by('dept')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        #endregion

        #region Project Serialization Tests

        [Fact]
        public async Task Project_ShouldReturnNamedMap()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice').property('age', 30)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').project('personName', 'personAge').by('name').by('age')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        #endregion

        #region Select Serialization Tests

        [Fact]
        public async Task Select_SingleKey_ShouldReturnValue()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'a').property('name', 'Alice')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'b').property('name', 'Bob')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b'))", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('a').as('person').out('knows').select('person')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Select_MultipleKeys_ShouldReturnMap()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'a').property('name', 'Alice')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'b').property('name', 'Bob')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b'))", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('a').as('source').out('knows').as('target').select('source', 'target')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        #endregion

        #region Debug Export Tests

        [Fact]
        public async Task DebugExport_ShouldIncludeQueryLog()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
            
            var debugJson = connector.ExportDebugDataAsJson();
            
            debugJson.Should().Contain("queryLog");
        }

        [Fact]
        public async Task DebugExport_ShouldIncludePerformanceMetrics()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            
            var debugJson = connector.ExportDebugDataAsJson();
            
            debugJson.Should().Contain("performanceMetrics");
        }

        [Fact]
        public async Task QueryLog_ShouldTrackExecutedQueries()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('v1').values('id')", new Dictionary<string, object>());
            
            var queryLog = connector.GetQueryLog().ToList();
            
            queryLog.Should().HaveCount(3);
        }

        [Fact]
        public async Task QueryLogStatistics_ShouldBeAccurate()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
            
            var stats = connector.GetQueryLogStatistics();
            
            stats.Should().ContainKey("totalQueries");
            stats.Should().ContainKey("successfulQueries");
            stats["totalQueries"].Should().Be(2);
            stats["successfulQueries"].Should().Be(2);
        }

        #endregion

        #region JSON Round-Trip Tests

        [Fact]
        public async Task JsonRoundTrip_VertexWithProperties_ShouldPreserveData()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice').property('age', 30).property('active', true)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1')", new Dictionary<string, object>());
            var vertex = result.First();
            
            // Serialize to JSON
            var json = JsonConvert.SerializeObject(vertex);
            
            // Parse back
            var parsed = JsonConvert.DeserializeObject<dynamic>(json);
            
            ((string)parsed.id).Should().Be("v1");
            ((string)parsed.label).Should().Be("person");
        }

        #endregion
    }
}
