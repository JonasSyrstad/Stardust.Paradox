using Stardust.Paradox.Data.Mocker;
using System;
using System.Collections.Generic;

namespace Stardust.Paradox.Data.Mocker.Examples
{
    /// <summary>
    /// Example of a custom financial services scenario
    /// </summary>
    public class FinancialServicesScenario : ScenarioProviderBase
    {
        public override string ScenarioName => "FinancialServices";
        public override string Description => "Financial services scenario with accounts, transactions, customers, and credit checks";

        protected override (VertexDefinition[] vertices, EdgeDefinition[] edges) GetScenarioData()
        {
            var vertices = new VertexDefinition[]
            {
                // Customers
                new VertexDefinition("cust1", "customer", Props(
                    ("name", "John Smith"),
                    ("email", "john.smith@email.com"),
                    ("creditScore", 750),
                    ("joinDate", DateTime.UtcNow.AddYears(-3))
                )),
                new VertexDefinition("cust2", "customer", Props(
                    ("name", "Sarah Johnson"),
                    ("email", "sarah.johnson@email.com"),
                    ("creditScore", 820),
                    ("joinDate", DateTime.UtcNow.AddYears(-1))
                )),
                
                // Accounts
                new VertexDefinition("acc1", "account", Props(
                    ("accountNumber", "ACC001"),
                    ("accountType", "Checking"),
                    ("balance", 15000.50),
                    ("openDate", DateTime.UtcNow.AddYears(-2))
                )),
                new VertexDefinition("acc2", "account", Props(
                    ("accountNumber", "ACC002"),
                    ("accountType", "Savings"),
                    ("balance", 45000.00),
                    ("openDate", DateTime.UtcNow.AddYears(-1))
                )),
                new VertexDefinition("acc3", "account", Props(
                    ("accountNumber", "ACC003"),
                    ("accountType", "Credit"),
                    ("balance", -2500.75),
                    ("creditLimit", 10000.00),
                    ("openDate", DateTime.UtcNow.AddMonths(-6))
                )),
                
                // Transactions
                new VertexDefinition("txn1", "transaction", Props(
                    ("transactionId", "TXN001"),
                    ("amount", -150.00),
                    ("type", "Withdrawal"),
                    ("description", "ATM Withdrawal"),
                    ("date", DateTime.UtcNow.AddDays(-5))
                )),
                new VertexDefinition("txn2", "transaction", Props(
                    ("transactionId", "TXN002"),
                    ("amount", 2500.00),
                    ("type", "Deposit"),
                    ("description", "Salary Deposit"),
                    ("date", DateTime.UtcNow.AddDays(-2))
                )),
                new VertexDefinition("txn3", "transaction", Props(
                    ("transactionId", "TXN003"),
                    ("amount", -75.50),
                    ("type", "Purchase"),
                    ("description", "Online Purchase"),
                    ("date", DateTime.UtcNow.AddDays(-1))
                )),
                
                // Products
                new VertexDefinition("prod1", "product", Props(
                    ("productCode", "CHK001"),
                    ("productName", "Premium Checking"),
                    ("monthlyFee", 15.00),
                    ("minimumBalance", 1000.00)
                )),
                new VertexDefinition("prod2", "product", Props(
                    ("productCode", "SAV001"),
                    ("productName", "High Yield Savings"),
                    ("interestRate", 0.025),
                    ("minimumBalance", 5000.00)
                ))
            };

            var edges = new EdgeDefinition[]
            {
                // Customer-Account relationships
                new EdgeDefinition("owns1", "owns", "cust1", "acc1"),
                new EdgeDefinition("owns2", "owns", "cust1", "acc2"),
                new EdgeDefinition("owns3", "owns", "cust2", "acc3"),
                
                // Account-Transaction relationships
                new EdgeDefinition("has_txn1", "has_transaction", "acc1", "txn1"),
                new EdgeDefinition("has_txn2", "has_transaction", "acc1", "txn2"),
                new EdgeDefinition("has_txn3", "has_transaction", "acc3", "txn3"),
                
                // Account-Product relationships
                new EdgeDefinition("uses_prod1", "uses_product", "acc1", "prod1"),
                new EdgeDefinition("uses_prod2", "uses_product", "acc2", "prod2")
            };

            return (vertices, edges);
        }

        protected override void ConfigureCustomResponses(MockGremlinLanguageConnector connector)
        {
            // High-value customers (credit score >= 800)
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('customer'\)\.has\('creditScore', gte\(800\)\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("cust2", "customer", new Dictionary<string, object> 
                    { 
                        { "name", "Sarah Johnson" }, 
                        { "creditScore", 820 } 
                    })
                );
            });

            // Recent transactions (last 7 days)
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('transaction'\)\.has\('date', gte\(.*\)\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("txn1", "transaction", new Dictionary<string, object> 
                    { 
                        { "transactionId", "TXN001" }, 
                        { "amount", -150.00 },
                        { "type", "Withdrawal" }
                    }),
                    ("txn2", "transaction", new Dictionary<string, object> 
                    { 
                        { "transactionId", "TXN002" }, 
                        { "amount", 2500.00 },
                        { "type", "Deposit" }
                    }),
                    ("txn3", "transaction", new Dictionary<string, object> 
                    { 
                        { "transactionId", "TXN003" }, 
                        { "amount", -75.50 },
                        { "type", "Purchase" }
                    })
                );
            });

            // Customer's total balance across all accounts
            connector.ConfigureFunctionResponse(@"g\.V\('cust1'\)\.out\('owns'\)\.values\('balance'\)\.sum\(\)", (query, parameters) =>
            {
                return new[] { new { sum = 60000.50 } }; // Return as dynamic object
            });

            // Account transaction history
            connector.ConfigureFunctionResponse(@"g\.V\('acc1'\)\.out\('has_transaction'\)\.order\(\)\.by\('date', desc\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("txn2", "transaction", new Dictionary<string, object> 
                    { 
                        { "transactionId", "TXN002" }, 
                        { "amount", 2500.00 },
                        { "date", DateTime.UtcNow.AddDays(-2) }
                    }),
                    ("txn1", "transaction", new Dictionary<string, object> 
                    { 
                        { "transactionId", "TXN001" }, 
                        { "amount", -150.00 },
                        { "date", DateTime.UtcNow.AddDays(-5) }
                    })
                );
            });

            // Risk assessment - customers with negative balances
            connector.ConfigureFunctionResponse(@"g\.V\(\)\.hasLabel\('customer'\)\.where\(out\('owns'\)\.has\('balance', lt\(0\)\)\)", (query, parameters) =>
            {
                return MockExtensions.CreateVertexCollection(
                    ("cust2", "customer", new Dictionary<string, object> 
                    { 
                        { "name", "Sarah Johnson" }, 
                        { "creditScore", 820 }
                    })
                );
            });
        }
    }

    /// <summary>
    /// Example usage of the financial services scenario
    /// </summary>
    public static class FinancialServicesExample
    {
        public static void DemonstrateUsage()
        {
            // Register the custom scenario
            ScenarioRegistry.Register(new FinancialServicesScenario());

            // Create connector with the scenario
            var connector = MockGremlinConnectorFactory.CreateWithScenario("FinancialServices");

            // Now you can use the connector in your tests with realistic financial data
            // Example: Test account balance calculation
            // Example: Test transaction history retrieval
            // Example: Test risk assessment queries
        }
    }
}