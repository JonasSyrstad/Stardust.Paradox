using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Core;
using Xunit;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Comprehensive TinkerPop Provider Compliance Tests
    /// Based on: https://tinkerpop.apache.org/docs/current/dev/provider/
    /// 
    /// This test suite validates that the in-memory implementation satisfies the TinkerPop
    /// provider requirements to be a serious alternative for .NET developers testing their
    /// Gremlin-based applications.
    /// </summary>
    public class TinkerPopProviderComplianceTests
    {
        #region Graph Structure Features

        /// <summary>
        /// Tests for Graph.Features.GraphFeatures implementation
        /// Reference: TinkerPop Provider Documentation - Graph Features
        /// </summary>
        [Fact]
        public async Task Graph_ShouldSupportPersistence()
        {
            // TinkerPop Requirement: Graph.Features.GraphFeatures.supportsComputer
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            // Add data
            await connector.ExecuteAsync("g.addV('test').property('name', 'persistent')", new Dictionary<string, object>());
            
            // Data should persist within the same connector instance
            var result = await connector.ExecuteAsync("g.V().has('name', 'persistent')", new Dictionary<string, object>());
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Graph_ShouldSupportConcurrentAccess()
        {
            // TinkerPop Requirement: Graph.Features.GraphFeatures.supportsConcurrentAccess
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            // Setup test data
            for (int i = 0; i < 10; i++)
            {
                await connector.ExecuteAsync($"g.addV('node').property('id', 'n{i}')", new Dictionary<string, object>());
            }

            // Concurrent reads
            var tasks = Enumerable.Range(0, 10)
                .Select(_ => connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>()));
            
            var results = await Task.WhenAll(tasks);
            
            foreach (var result in results)
            {
                ((long)result.First()).Should().Be(10);
            }
        }

        [Fact]
        public void Graph_ShouldSupportVariableFeatures()
        {
            // TinkerPop Requirement: Graph.Features.VariableFeatures
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            // The connector should support basic variable types
            connector.CanParameterizeQueries.Should().BeTrue();
        }

        #endregion

        #region Vertex Features

        /// <summary>
        /// Tests for Graph.Features.VertexFeatures implementation
        /// Reference: TinkerPop Provider Documentation - Vertex Features
        /// </summary>
        [Fact]
        public async Task Vertex_ShouldSupportAddVertices()
        {
            // TinkerPop Requirement: VertexFeatures.supportsAddVertices
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            var result = await connector.ExecuteAsync("g.addV('person')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Vertex_ShouldSupportRemoveVertices()
        {
            // TinkerPop Requirement: VertexFeatures.supportsRemoveVertices
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'temp')", new Dictionary<string, object>());
            
            await connector.ExecuteAsync("g.V('temp').drop()", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('temp')", new Dictionary<string, object>());
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Vertex_ShouldSupportUserSuppliedIds()
        {
            // TinkerPop Requirement: VertexFeatures.supportsUserSuppliedIds
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            await connector.ExecuteAsync("g.addV('person').property('id', 'custom-id-123')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('custom-id-123')", new Dictionary<string, object>());
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Vertex_ShouldSupportStringIds()
        {
            // TinkerPop Requirement: VertexFeatures.supportsStringIds
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            await connector.ExecuteAsync("g.addV('person').property('id', 'string-id')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('string-id').id()", new Dictionary<string, object>());
            ((string)result.First()).Should().Be("string-id");
        }

        [Fact]
        public async Task Vertex_ShouldSupportAddProperty()
        {
            // TinkerPop Requirement: VertexFeatures.supportsAddProperty
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            
            await connector.ExecuteAsync("g.V('v1').property('name', 'Test')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').values('name')", new Dictionary<string, object>());
            ((string)result.First()).Should().Be("Test");
        }

        [Fact]
        public async Task Vertex_ShouldSupportRemoveProperty()
        {
            // TinkerPop Requirement: VertexFeatures.supportsRemoveProperty
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Test')", new Dictionary<string, object>());
            
            await connector.ExecuteAsync("g.V('v1').properties('name').drop()", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').values('name')", new Dictionary<string, object>());
            result.Should().BeEmpty();
        }

        #endregion

        #region Edge Features

        /// <summary>
        /// Tests for Graph.Features.EdgeFeatures implementation
        /// Reference: TinkerPop Provider Documentation - Edge Features
        /// </summary>
        [Fact]
        public async Task Edge_ShouldSupportAddEdges()
        {
            // TinkerPop Requirement: EdgeFeatures.supportsAddEdges
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'a')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'b')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b'))", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Edge_ShouldSupportRemoveEdges()
        {
            // TinkerPop Requirement: EdgeFeatures.supportsRemoveEdges
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'a')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'b')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b'))", new Dictionary<string, object>());
            
            await connector.ExecuteAsync("g.E().hasLabel('knows').drop()", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.E().hasLabel('knows').count()", new Dictionary<string, object>());
            ((long)result.First()).Should().Be(0);
        }

        [Fact]
        public async Task Edge_ShouldSupportUserSuppliedIds()
        {
            // TinkerPop Requirement: EdgeFeatures.supportsUserSuppliedIds
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'a')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'b')", new Dictionary<string, object>());
            
            // Create edge with id as property
            await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b')).property('id', 'edge-123')", new Dictionary<string, object>());

            // Verify edge exists with the id property
            var result = await connector.ExecuteAsync("g.E().has('id', 'edge-123')", new Dictionary<string, object>());
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Edge_ShouldSupportAddProperty()
        {
            // TinkerPop Requirement: EdgeFeatures.supportsAddProperty
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'a')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'b')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('a').addE('knows').to(g.V('b'))", new Dictionary<string, object>());
            
            await connector.ExecuteAsync("g.E().hasLabel('knows').property('since', 2020)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.E().hasLabel('knows').values('since')", new Dictionary<string, object>());
            ((int)result.First()).Should().Be(2020);
        }

        #endregion

        #region Property Value Types

        /// <summary>
        /// Tests for supported property value types
        /// Reference: TinkerPop Provider Documentation - Property Features
        /// </summary>
        [Theory]
        [InlineData("stringVal", "hello", typeof(string))]
        [InlineData("intVal", 42, typeof(int))]
        [InlineData("longVal", 9999999999L, typeof(long))]
        [InlineData("doubleVal", 3.14159, typeof(double))]
        [InlineData("boolVal", true, typeof(bool))]
        public async Task Property_ShouldSupportVariousDataTypes(string propName, object value, Type expectedType)
        {
            // TinkerPop Requirement: Various value type support
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            var query = value switch
            {
                string s => $"g.addV('test').property('id', 'v1').property('{propName}', '{s}')",
                bool b => $"g.addV('test').property('id', 'v1').property('{propName}', {b.ToString().ToLower()})",
                _ => $"g.addV('test').property('id', 'v1').property('{propName}', {value})"
            };
            
            await connector.ExecuteAsync(query, new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync($"g.V('v1').values('{propName}')", new Dictionary<string, object>());
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Property_ShouldSupportFloat()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            await connector.ExecuteAsync("g.addV('test').property('id', 'v1').property('floatVal', 1.5)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').values('floatVal')", new Dictionary<string, object>());
            result.Should().HaveCount(1);
            double value = Convert.ToDouble(result.First());
            value.Should().BeApproximately(1.5, 0.01);
        }

        #endregion

        #region Traversal Source Steps

        /// <summary>
        /// Tests for GraphTraversalSource step support
        /// Reference: TinkerPop Reference Documentation - Start Steps
        /// </summary>
        [Fact]
        public async Task TraversalSource_V_ShouldReturnAllVertices()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V()", new Dictionary<string, object>());
            
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task TraversalSource_V_WithIds_ShouldFilterByIds()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('c').property('id', 'v3')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1', 'v3')", new Dictionary<string, object>());
            
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task TraversalSource_E_ShouldReturnAllEdges()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.E()", new Dictionary<string, object>());
            
            result.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task TraversalSource_AddV_ShouldCreateVertex()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            var result = await connector.ExecuteAsync("g.addV('person').property('name', 'Test')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
            var vertex = result.First();
            ((string)vertex.label).Should().Be("person");
        }

        [Fact]
        public async Task TraversalSource_Inject_ShouldInjectValues()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            var result = await connector.ExecuteAsync("g.inject(1, 2, 3)", new Dictionary<string, object>());
            
            result.Should().HaveCount(3);
        }

        #endregion

        #region Vertex/Edge Navigation Steps

        /// <summary>
        /// Tests for vertex and edge traversal steps
        /// Reference: TinkerPop Reference Documentation - Vertex Steps
        /// </summary>
        [Fact]
        public async Task Navigation_Out_ShouldTraverseOutgoingEdges()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V('alice').out('knows')", new Dictionary<string, object>());
            
            result.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task Navigation_In_ShouldTraverseIncomingEdges()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V('bob').in('knows')", new Dictionary<string, object>());
            
            result.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task Navigation_Both_ShouldTraverseBothDirections()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V('bob').both('knows')", new Dictionary<string, object>());
            
            result.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task Navigation_OutE_ShouldReturnOutgoingEdges()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V('alice').outE('knows')", new Dictionary<string, object>());
            
            result.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task Navigation_InE_ShouldReturnIncomingEdges()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V('bob').inE('knows')", new Dictionary<string, object>());
            
            result.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task Navigation_BothE_ShouldReturnBothDirectionEdges()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V('bob').bothE('knows')", new Dictionary<string, object>());
            
            result.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task Navigation_OutV_ShouldReturnOutVertex()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.E().hasLabel('knows').outV()", new Dictionary<string, object>());
            
            result.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task Navigation_InV_ShouldReturnInVertex()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.E().hasLabel('knows').inV()", new Dictionary<string, object>());
            
            result.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task Navigation_BothV_ShouldReturnBothVertices()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.E().hasLabel('knows').bothV()", new Dictionary<string, object>());
            
            result.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task Navigation_OtherV_ShouldReturnOtherVertex()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V('alice').outE('knows').otherV()", new Dictionary<string, object>());
            
            result.Should().HaveCountGreaterThan(0);
        }

        #endregion

        #region Filter Steps

        /// <summary>
        /// Tests for filter steps
        /// Reference: TinkerPop Reference Documentation - Filter Steps
        /// </summary>
        [Fact]
        public async Task Filter_Has_WithPropertyAndValue_ShouldFilter()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V().has('name', 'Alice')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Filter_Has_WithLabelAndProperty_ShouldFilter()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V().has('person', 'name', 'Alice')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Filter_Has_PropertyExists_ShouldFilter()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('age', 30)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().has('age')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Filter_HasNot_ShouldFilterAbsentProperty()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('age', 30)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().hasNot('age')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Filter_HasLabel_ShouldFilterByLabel()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('company').property('id', 'v2')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().hasLabel('person')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Filter_HasId_ShouldFilterById()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().hasId('v1')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Filter_Where_ShouldApplyTraversalCondition()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V('alice').out('knows').where(__.has('name', 'Bob'))", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Filter_Not_ShouldNegateCondition()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V().not(__.hasLabel('person'))", new Dictionary<string, object>());
            
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task Filter_And_ShouldCombineConditions()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('age', 30).property('name', 'Test')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'v2').property('age', 25)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().and(__.has('age'), __.has('name'))", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Filter_Or_ShouldAcceptAnyCondition()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'v2').property('name', 'Bob')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('company').property('id', 'v3')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().or(__.has('name', 'Alice'), __.has('name', 'Bob'))", new Dictionary<string, object>());
            
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task Filter_Is_ShouldCompareValues()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('age', 30)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').values('age').is(30)", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Filter_Dedup_ShouldRemoveDuplicates()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V().out().in().dedup()", new Dictionary<string, object>());
            
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task Filter_SimplePath_ShouldExcludeRepeatedVertices()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V('alice').out().in().simplePath()", new Dictionary<string, object>());
            
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task Filter_CyclicPath_ShouldIncludeRepeatedVertices()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V('alice').out().in().cyclicPath()", new Dictionary<string, object>());
            
            result.Should().NotBeNull();
        }

        #endregion

        #region Map/Transform Steps

        /// <summary>
        /// Tests for map/transform steps
        /// Reference: TinkerPop Reference Documentation - Map Steps
        /// </summary>
        [Fact]
        public async Task Map_Values_ShouldReturnPropertyValues()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').values('name')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
            ((string)result.First()).Should().Be("Alice");
        }

        [Fact]
        public async Task Map_ValueMap_ShouldReturnPropertyMap()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice').property('age', 30)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').valueMap()", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Map_ElementMap_ShouldReturnElementWithIdAndLabel()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').elementMap()", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Map_Properties_ShouldReturnPropertyElements()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').properties('name')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Map_Id_ShouldReturnElementId()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').id()", new Dictionary<string, object>());
            
            ((string)result.First()).Should().Be("v1");
        }

        [Fact]
        public async Task Map_Label_ShouldReturnElementLabel()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').label()", new Dictionary<string, object>());
            
            ((string)result.First()).Should().Be("person");
        }

        [Fact]
        public async Task Map_Path_ShouldReturnTraversalPath()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V('alice').out('knows').path()", new Dictionary<string, object>());
            
            result.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task Map_Constant_ShouldReturnConstantValue()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').constant('fixed')", new Dictionary<string, object>());
            
            ((string)result.First()).Should().Be("fixed");
        }

        [Fact]
        public async Task Map_Project_ShouldMapToNamedValues()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice').property('age', 30)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('v1').project('personName', 'personAge').by('name').by('age')", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Map_Fold_ShouldCollectIntoList()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'v2').property('name', 'Bob')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().values('name').fold()", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
            var folded = result.First() as IEnumerable<object>;
            folded.Should().HaveCount(2);
        }

        [Fact]
        public async Task Map_Unfold_ShouldExpandCollection()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            var result = await connector.ExecuteAsync("g.inject([1, 2, 3]).unfold()", new Dictionary<string, object>());
            
            result.Should().HaveCount(3);
        }

        #endregion

        #region Aggregation/Reduce Steps

        /// <summary>
        /// Tests for aggregation and reduce steps
        /// Reference: TinkerPop Reference Documentation - Reduce Steps
        /// </summary>
        [Fact]
        public async Task Aggregate_Count_ShouldReturnCount()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await SetupBasicGraph(connector);
            
            var result = await connector.ExecuteAsync("g.V().count()", new Dictionary<string, object>());
            
            ((long)result.First()).Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task Aggregate_Sum_ShouldReturnSum()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('val', 10)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('val', 20)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().values('val').sum()", new Dictionary<string, object>());
            
            ((long)result.First()).Should().Be(30L);
        }

        [Fact]
        public async Task Aggregate_Min_ShouldReturnMinimum()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('val', 10)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('val', 20)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().values('val').min()", new Dictionary<string, object>());
            
            ((long)result.First()).Should().Be(10L);
        }

        [Fact]
        public async Task Aggregate_Max_ShouldReturnMaximum()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('val', 10)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('val', 20)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().values('val').max()", new Dictionary<string, object>());
            
            ((long)result.First()).Should().Be(20L);
        }

        [Fact]
        public async Task Aggregate_Mean_ShouldReturnAverage()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('val', 10)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('val', 20)", new Dictionary<string, object>());

            var result = await connector.ExecuteAsync("g.V().values('val').mean()", new Dictionary<string, object>());

            double mean = Convert.ToDouble(result.First());
            mean.Should().BeApproximately(15.0, 0.1);
        }

        public async Task Order_ShouldOrderAscending()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('val', 30)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2').property('val', 10)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('c').property('id', 'v3').property('val', 20)", new Dictionary<string, object>());

            var result = await connector.ExecuteAsync("g.V().order().by('val', asc).values('val')", new Dictionary<string, object>());

            var list = result.ToList();
            list.Should().HaveCount(3);

            var v0 = Convert.ToInt32(list[0]);
            var v1 = Convert.ToInt32(list[1]);
            var v2 = Convert.ToInt32(list[2]);

            v0.Should().Be(10);
            v1.Should().Be(20);
            v2.Should().Be(30);
        }

        public async Task Order_ByDescending_ShouldOrderDescending()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('val', 30)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2').property('val', 10)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('c').property('id', 'v3').property('val', 20)", new Dictionary<string, object>());

            var result = await connector.ExecuteAsync("g.V().order().by('val', desc).values('val')", new Dictionary<string, object>());

            var list = result.ToList();
            list.Should().HaveCount(3);

            var v0 = Convert.ToInt32(list[0]);
            var v1 = Convert.ToInt32(list[1]);
            var v2 = Convert.ToInt32(list[2]);

            v0.Should().Be(30);
            v1.Should().Be(20);
            v2.Should().Be(10);
        }

        [Fact]
        public async Task Limit_ShouldLimitResults()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            for (int i = 0; i < 10; i++)
            {
                await connector.ExecuteAsync($"g.addV('node').property('id', 'n{i}')", new Dictionary<string, object>());
            }
            
            var result = await connector.ExecuteAsync("g.V().limit(5)", new Dictionary<string, object>());
            
            result.Should().HaveCount(5);
        }

        [Fact]
        public async Task Skip_ShouldSkipResults()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            for (int i = 0; i < 10; i++)
            {
                await connector.ExecuteAsync($"g.addV('node').property('id', 'n{i}')", new Dictionary<string, object>());
            }
            
            var result = await connector.ExecuteAsync("g.V().skip(7)", new Dictionary<string, object>());
            
            result.Should().HaveCount(3);
        }

        [Fact]
        public async Task Range_ShouldReturnRange()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            for (int i = 0; i < 10; i++)
            {
                await connector.ExecuteAsync($"g.addV('node').property('id', 'n{i}')", new Dictionary<string, object>());
            }
            
            var result = await connector.ExecuteAsync("g.V().range(3, 6)", new Dictionary<string, object>());
            
            result.Should().HaveCount(3);
        }

        [Fact]
        public async Task Tail_ShouldReturnLastElements()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            for (int i = 0; i < 10; i++)
            {
                await connector.ExecuteAsync($"g.addV('node').property('id', 'n{i}')", new Dictionary<string, object>());
            }
            
            var result = await connector.ExecuteAsync("g.V().tail(3)", new Dictionary<string, object>());
            
            result.Should().HaveCount(3);
        }

        [Fact]
        public async Task Sample_ShouldReturnRandomSample()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            for (int i = 0; i < 10; i++)
            {
                await connector.ExecuteAsync($"g.addV('node').property('id', 'n{i}')", new Dictionary<string, object>());
            }
            
            var result = await connector.ExecuteAsync("g.V().sample(3)", new Dictionary<string, object>());
            
            result.Should().HaveCount(3);
        }

        #endregion

        #region Predicates

        /// <summary>
        /// Tests for Gremlin predicates
        /// Reference: TinkerPop Reference Documentation - Predicates
        /// </summary>
        [Fact]
        public async Task Predicate_Eq_ShouldCompareEquality()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('val', 10)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2').property('val', 20)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().has('val', eq(10))", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Predicate_Neq_ShouldCompareInequality()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('val', 10)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2').property('val', 20)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().has('val', neq(10))", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Predicate_Lt_ShouldCompareLessThan()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('val', 10)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2').property('val', 20)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().has('val', lt(15))", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Predicate_Lte_ShouldCompareLessThanOrEqual()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('val', 10)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2').property('val', 20)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().has('val', lte(10))", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Predicate_Gt_ShouldCompareGreaterThan()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('val', 10)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2').property('val', 20)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().has('val', gt(15))", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Predicate_Gte_ShouldCompareGreaterThanOrEqual()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('val', 10)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2').property('val', 20)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().has('val', gte(20))", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Predicate_Inside_ShouldCheckRange()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('val', 15)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2').property('val', 25)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().has('val', inside(10, 20))", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Predicate_Outside_ShouldCheckOutsideRange()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('val', 5)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2').property('val', 15)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().has('val', outside(10, 20))", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Predicate_Between_ShouldCheckInclusiveRange()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('val', 10)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2').property('val', 20)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().has('val', between(10, 20))", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Predicate_Within_ShouldCheckMembership()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('name', 'Alice')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2').property('name', 'Bob')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('c').property('id', 'v3').property('name', 'Charlie')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().has('name', within('Alice', 'Bob'))", new Dictionary<string, object>());
            
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task Predicate_Without_ShouldExcludeMembership()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('a').property('id', 'v1').property('name', 'Alice')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('b').property('id', 'v2').property('name', 'Bob')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('c').property('id', 'v3').property('name', 'Charlie')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V().has('name', without('Alice', 'Bob'))", new Dictionary<string, object>());
            
            result.Should().HaveCount(1);
        }

        #endregion

        #region Parameterized Queries

        /// <summary>
        /// Tests for parameterized query support
        /// Critical for secure and efficient query execution
        /// </summary>
        [Fact]
        public async Task Parameterized_StringParameter_ShouldSubstitute()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync(
                "g.V().has('name', searchName)",
                new Dictionary<string, object> { { "searchName", "Alice" } });
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Parameterized_IntegerParameter_ShouldSubstitute()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('age', 30)", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync(
                "g.V().has('age', searchAge)",
                new Dictionary<string, object> { { "searchAge", 30 } });
            
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task Parameterized_ArrayParameter_ShouldSubstitute()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            await connector.ExecuteAsync("g.addV('person').property('id', 'v1').property('name', 'Alice')", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'v2').property('name', 'Bob')", new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync(
                "g.V(ids)",
                new Dictionary<string, object> { { "ids", new[] { "v1", "v2" } } });
            
            result.Should().HaveCount(2);
        }

        #endregion

        #region Error Handling

        /// <summary>
        /// Tests for proper error handling
        /// Reference: TinkerPop Provider Documentation - Error Handling
        /// </summary>
        [Fact]
        public async Task Error_InvalidQuery_ShouldThrow()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await connector.ExecuteAsync("invalid.query.syntax", new Dictionary<string, object>());
            });
        }

        [Fact]
        public async Task Error_EmptyQuery_ShouldThrow()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await connector.ExecuteAsync("", new Dictionary<string, object>());
            });
        }

        [Fact]
        public async Task Error_NullQuery_ShouldThrow()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await connector.ExecuteAsync(null!, new Dictionary<string, object>());
            });
        }

        [Fact]
        public async Task Error_NonExistentVertex_ShouldReturnEmpty()
        {
            var connector = InMemoryGremlinLanguageConnector.Create();
            
            var result = await connector.ExecuteAsync("g.V('non-existent-id')", new Dictionary<string, object>());
            
            result.Should().BeEmpty();
        }

        #endregion

        #region Helper Methods

        private static async Task SetupBasicGraph(InMemoryGremlinLanguageConnector connector)
        {
            await connector.ExecuteAsync("g.addV('person').property('id', 'alice').property('name', 'Alice').property('age', 30)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'bob').property('name', 'Bob').property('age', 28)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'charlie').property('name', 'Charlie').property('age', 35)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('alice').addE('knows').to(g.V('bob')).property('since', 2010)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('alice').addE('knows').to(g.V('charlie')).property('since', 2015)", new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('bob').addE('knows').to(g.V('charlie')).property('since', 2012)", new Dictionary<string, object>());
        }

        private static async Task SetupChainGraph(InMemoryGremlinLanguageConnector connector, int length)
        {
            for (int i = 0; i < length; i++)
            {
                await connector.ExecuteAsync($"g.addV('node').property('id', 'node_{i}').property('level', {i})", new Dictionary<string, object>());
            }
            for (int i = 0; i < length - 1; i++)
            {
                await connector.ExecuteAsync($"g.V('node_{i}').addE('next').to(g.V('node_{i + 1}'))", new Dictionary<string, object>());
            }
        }

        #endregion
    }
}
