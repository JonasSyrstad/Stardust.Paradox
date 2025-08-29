using System;
using System.Collections.Generic;
using System.Linq;
using Stardust.Paradox.Data.InMemory.Scenarios;

namespace Stardust.Paradox.Data.InMemory
{
    /// <summary>
    /// Example custom scenario for library management
    /// </summary>
    public class LibraryManagementScenario : InMemoryScenarioProviderBase
    {
        public override string ScenarioName => "LibraryManagement";
        public override string Description => "Library system with books, authors, genres, and borrowing relationships";

        protected override (Scenarios.InMemoryVertexDefinition[] vertices, Scenarios.InMemoryEdgeDefinition[] edges) GetScenarioData()
        {
            var vertices = new Scenarios.InMemoryVertexDefinition[]
            {
                // Authors
                new Scenarios.InMemoryVertexDefinition("tolkien", "author", Props(
                    ("name", "J.R.R. Tolkien"),
                    ("birthYear", 1892),
                    ("nationality", "British")
                )),
                new Scenarios.InMemoryVertexDefinition("asimov", "author", Props(
                    ("name", "Isaac Asimov"),
                    ("birthYear", 1920),
                    ("nationality", "American")
                )),

                // Books
                new Scenarios.InMemoryVertexDefinition("lotr", "book", Props(
                    ("title", "The Lord of the Rings"),
                    ("publicationYear", 1954),
                    ("isbn", "978-0544003415"),
                    ("availableCopies", 3)
                )),
                new Scenarios.InMemoryVertexDefinition("foundation", "book", Props(
                    ("title", "Foundation"),
                    ("publicationYear", 1951),
                    ("isbn", "978-0553293357"),
                    ("availableCopies", 2)
                )),
                new Scenarios.InMemoryVertexDefinition("hobbit", "book", Props(
                    ("title", "The Hobbit"),
                    ("publicationYear", 1937),
                    ("isbn", "978-0547928227"),
                    ("availableCopies", 1)
                )),

                // Genres
                new Scenarios.InMemoryVertexDefinition("fantasy", "genre", Props(
                    ("name", "Fantasy"),
                    ("description", "Fantasy fiction")
                )),
                new Scenarios.InMemoryVertexDefinition("scifi", "genre", Props(
                    ("name", "Science Fiction"),
                    ("description", "Science fiction literature")
                )),

                // Patrons
                new Scenarios.InMemoryVertexDefinition("patron1", "patron", Props(
                    ("name", "Alice Reader"),
                    ("membershipNumber", "L001"),
                    ("joinDate", DateTime.UtcNow.AddYears(-2))
                )),
                new Scenarios.InMemoryVertexDefinition("patron2", "patron", Props(
                    ("name", "Bob Bookworm"),
                    ("membershipNumber", "L002"),
                    ("joinDate", DateTime.UtcNow.AddMonths(-6))
                ))
            };

            var edges = new Scenarios.InMemoryEdgeDefinition[]
            {
                // Author-Book relationships
                new Scenarios.InMemoryEdgeDefinition("wrote", "tolkien", "lotr"),
                new Scenarios.InMemoryEdgeDefinition("wrote", "tolkien", "hobbit"),
                new Scenarios.InMemoryEdgeDefinition("wrote", "asimov", "foundation"),

                // Book-Genre relationships
                new Scenarios.InMemoryEdgeDefinition("belongs_to", "lotr", "fantasy"),
                new Scenarios.InMemoryEdgeDefinition("belongs_to", "hobbit", "fantasy"),
                new Scenarios.InMemoryEdgeDefinition("belongs_to", "foundation", "scifi"),

                // Borrowing relationships
                new Scenarios.InMemoryEdgeDefinition("borrowed", "patron1", "lotr", Props(
                    ("borrowDate", DateTime.UtcNow.AddDays(-10)),
                    ("dueDate", DateTime.UtcNow.AddDays(4))
                )),
                new Scenarios.InMemoryEdgeDefinition("borrowed", "patron2", "foundation", Props(
                    ("borrowDate", DateTime.UtcNow.AddDays(-5)),
                    ("dueDate", DateTime.UtcNow.AddDays(9))
                ))
            };

            return (vertices, edges);
        }

        protected override void ConfigureCustomResponses(InMemoryGraphDatabase database)
        {
            // Books by genre
            database.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('book'\)\.out\('belongs_to'\)\.has\('name', 'Fantasy'\)", (query, parameters) =>
            {
                return new[] { database.GetVertex("fantasy")?.ToGremlinResponse() }.Where(x => x != null);
            });

            // Overdue books
            database.RegisterCustomResponse(@"g\.E\(\)\.hasLabel\('borrowed'\)\.has\('dueDate', lt\(.*\)\)", (query, parameters) =>
            {
                // In a real scenario, you'd check actual dates
                var borrowEdge = database.GetAllEdges().FirstOrDefault(e => e.Label == "borrowed" && e.OutVertexId == "patron1");
                return borrowEdge != null ? new[] { borrowEdge.ToGremlinResponse() } : new dynamic[0];
            });

            // Prolific authors (those who wrote multiple books)
            database.RegisterCustomResponse(@"g\.V\(\)\.hasLabel\('author'\)\.where\(out\('wrote'\)\.count\(\)\.is\(gt\(1\)\)\)", (query, parameters) =>
            {
                return new[] { database.GetVertex("tolkien")?.ToGremlinResponse() }.Where(x => x != null);
            });
        }
    }
}