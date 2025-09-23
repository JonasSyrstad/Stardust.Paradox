using System;
using System.Collections.Generic;
using System.Linq;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.Scenarios;

/// <summary>
/// Base class for InMemory scenario providers with common functionality
/// </summary>
public abstract class InMemoryScenarioProviderBase : IInMemoryScenarioProvider
{
    public abstract string ScenarioName { get; }
    public abstract string Description { get; }
        
    public virtual void ConfigureScenario(InMemoryGraphDatabase database)
    {
        // Configure vertices and edges
        var (vertices, edges) = GetScenarioData();
        if (vertices?.Length > 0 || edges?.Length > 0)
        {
            PopulateDatabase(database, vertices, edges);
        }

        // Configure custom responses
        ConfigureCustomResponses(database);
    }

    /// <summary>
    /// Override to provide vertex and edge definitions for the scenario
    /// </summary>
    /// <returns>Tuple of vertices and edges</returns>
    protected virtual (ScenarioVertexDefinition[] vertices, ScenarioEdgeDefinition[] edges) GetScenarioData()
    {
        return (null, null);
    }

    /// <summary>
    /// Override to configure custom response patterns beyond basic data
    /// </summary>
    /// <param name="database">The database to configure</param>
    protected virtual void ConfigureCustomResponses(InMemoryGraphDatabase database)
    {
        // Override in derived classes for custom response patterns
    }

    /// <summary>
    /// Helper method to create property KeyValuePairs
    /// </summary>
    protected KeyValuePair<string, object> Prop(string key, object value)
    {
        return new KeyValuePair<string, object>(key, value);
    }

    /// <summary>
    /// Helper method to create multiple properties
    /// </summary>
    protected KeyValuePair<string, object>[] Props(params (string key, object value)[] properties)
    {
        var result = new KeyValuePair<string, object>[properties.Length];
        for (int i = 0; i < properties.Length; i++)
        {
            result[i] = new KeyValuePair<string, object>(properties[i].key, properties[i].value);
        }
        return result;
    }

    /// <summary>
    /// Populate the database with vertices and edges
    /// </summary>
    protected virtual void PopulateDatabase(InMemoryGraphDatabase database, 
        ScenarioVertexDefinition[] vertices, 
        ScenarioEdgeDefinition[] edges)
    {
        // Add vertices first
        if (vertices != null)
        {
            foreach (var vertexDef in vertices)
            {
                var vertex = database.AddVertex(vertexDef.Label, vertexDef.Id);
                if (vertexDef.Properties != null)
                {
                    foreach (var prop in vertexDef.Properties)
                    {
                        vertex.Properties[prop.Key] = prop.Value;
                    }
                }
            }
        }

        // Add edges after vertices exist
        if (edges != null)
        {
            foreach (var edgeDef in edges)
            {
                var edge = database.AddEdge(edgeDef.Label, edgeDef.OutVertexId, edgeDef.InVertexId, edgeDef.Id);
                if (edge != null && edgeDef.Properties != null)
                {
                    foreach (var prop in edgeDef.Properties)
                    {
                        edge.Properties[prop.Key] = prop.Value;
                    }
                }
            }
        }
    }
}
