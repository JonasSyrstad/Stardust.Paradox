using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Stardust.Paradox.Data.InMemory;
using Stardust.Paradox.Data.InMemory.Core;
using Stardust.Paradox.Data.InMemory.CosmosDb;
using Xunit;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    /// <summary>
    /// Tests for TinkerPop 3.5.x compliance and Cosmos DB emulation mode
    /// </summary>
    public class TinkerPop35ComplianceTests
    {
        #region Factory Method Tests
        
        [Fact]
        public void CreateCosmosDbEmulator_ShouldSetCosmosDbMode()
        {
            var connector = InMemoryGremlinLanguageConnector.CreateCosmosDbEmulator();
            
            connector.Options.CosmosDbEmulationMode.Should().BeTrue();
            connector.Options.ThrowOnUnsupportedStep.Should().BeTrue();
            connector.Options.SimulateRequestCharges.Should().BeTrue();
            connector.Options.MaxRequestUnitsPerSecond.Should().Be(10000);
            connector.Options.MaxItemsPerQuery.Should().Be(1000);
            connector.Options.EnableRateLimiting.Should().BeTrue();
        }
        
        [Fact]
        public void CreateCosmosDbEmulator_WithPartitionKey_ShouldSetPartitionKeyPath()
        {
            var connector = InMemoryGremlinLanguageConnector.CreateCosmosDbEmulator("/tenantId");
            
            connector.Options.CosmosDbEmulationMode.Should().BeTrue();
            connector.Options.PartitionKeyPath.Should().Be("/tenantId");
            connector.Options.EnforceCrossPartitionQueryRestrictions.Should().BeTrue();
        }
        
        [Fact]
        public void CreateCosmosDbEmulator_WithCustomConfig_ShouldApplyCustomSettings()
        {
            var connector = InMemoryGremlinLanguageConnector.CreateCosmosDbEmulator(options =>
            {
                options.MaxRequestUnitsPerSecond = 5000;
                options.MaxItemsPerQuery = 500;
            });
            
            connector.Options.CosmosDbEmulationMode.Should().BeTrue();
            connector.Options.MaxRequestUnitsPerSecond.Should().Be(5000);
            connector.Options.MaxItemsPerQuery.Should().Be(500);
        }
        
        #endregion
        
        #region Cosmos DB Gremlin Limitations Tests
        
        [Theory]
        [InlineData("V")]
        [InlineData("E")]
        [InlineData("addV")]
        [InlineData("addE")]
        [InlineData("out")]
        [InlineData("in")]
        [InlineData("both")]
        [InlineData("has")]
        [InlineData("hasLabel")]
        [InlineData("count")]
        [InlineData("sum")]
        [InlineData("values")]
        [InlineData("valueMap")]
        public void SupportedSteps_ShouldBeReportedAsSupported(string stepName)
        {
            var isSupported = CosmosDbGremlinLimitations.IsStepSupported(stepName);
            isSupported.Should().BeTrue($"Step '{stepName}' should be supported");
        }
        
        [Theory]
        [InlineData("sideEffect")]
        [InlineData("aggregate")]
        [InlineData("sack")]
        [InlineData("cap")]
        [InlineData("subgraph")]
        [InlineData("cyclicPath")]
        [InlineData("sample")]
        [InlineData("coin")]
        [InlineData("optional")]
        [InlineData("branch")]
        [InlineData("pageRank")]
        [InlineData("peerPressure")]
        [InlineData("math")]
        [InlineData("match")]
        public void UnsupportedSteps_ShouldBeReportedAsUnsupported(string stepName)
        {
            var isSupported = CosmosDbGremlinLimitations.IsStepSupported(stepName);
            isSupported.Should().BeFalse($"Step '{stepName}' should NOT be supported");
        }
        
        [Theory]
        [InlineData("eq")]
        [InlineData("neq")]
        [InlineData("lt")]
        [InlineData("lte")]
        [InlineData("gt")]
        [InlineData("gte")]
        [InlineData("inside")]
        [InlineData("outside")]
        [InlineData("between")]
        [InlineData("within")]
        [InlineData("without")]
        public void SupportedPredicates_ShouldBeReportedAsSupported(string predicateName)
        {
            var isSupported = CosmosDbGremlinLimitations.IsPredicateSupported(predicateName);
            isSupported.Should().BeTrue($"Predicate '{predicateName}' should be supported");
        }
        
        [Theory]
        [InlineData("startingWith")]
        [InlineData("endingWith")]
        [InlineData("containing")]
        public void SupportedTextPredicates_ShouldBeReportedAsSupported(string predicateName)
        {
            var isSupported = CosmosDbGremlinLimitations.IsPredicateSupported(predicateName);
            isSupported.Should().BeTrue($"Text predicate '{predicateName}' should be supported");
        }
        
        [Fact]
        public void GetUnsupportedReason_ShouldReturnMeaningfulMessage()
        {
            var reason = CosmosDbGremlinLimitations.GetUnsupportedReason("sideEffect");
            reason.Should().NotBeNullOrEmpty();
            reason.Should().Contain("lambda", "should explain why sideEffect is not supported");
        }
        
        [Fact]
        public void HasLimitedSupport_ShouldReturnLimitationForRepeat()
        {
            var hasLimited = CosmosDbGremlinLimitations.HasLimitedSupport("repeat", out var limitation);
            hasLimited.Should().BeTrue();
            limitation.Should().Contain("depth", "should mention repeat depth limits");
        }
        
        #endregion
        
        #region Query Validator Tests
        
        [Fact]
        public void Validator_ShouldPassForSupportedSteps()
        {
            var options = new InMemoryDatabaseOptions { CosmosDbEmulationMode = true };
            var validator = new CosmosDbQueryValidator(options);
            var steps = new List<ExecutionEngine.TinkerGraphStep>
            {
                new ExecutionEngine.TinkerGraphStep("V"),
                new ExecutionEngine.TinkerGraphStep("has") { Arguments = { "name", "test" } },
                new ExecutionEngine.TinkerGraphStep("out") { Arguments = { "knows" } },
                new ExecutionEngine.TinkerGraphStep("values") { Arguments = { "name" } }
            };
            
            var result = validator.Validate(steps);
            
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }
        
        [Fact]
        public void Validator_ShouldFailForUnsupportedSteps_WhenThrowOnUnsupportedStepIsTrue()
        {
            var options = new InMemoryDatabaseOptions 
            { 
                CosmosDbEmulationMode = true,
                ThrowOnUnsupportedStep = true
            };
            var validator = new CosmosDbQueryValidator(options);
            var steps = new List<ExecutionEngine.TinkerGraphStep>
            {
                new ExecutionEngine.TinkerGraphStep("V"),
                new ExecutionEngine.TinkerGraphStep("sideEffect")
            };
            
            var result = validator.Validate(steps);
            
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.StepName == "sideEffect");
        }
        
        [Fact]
        public void Validator_ShouldWarnForUnsupportedSteps_WhenThrowOnUnsupportedStepIsFalse()
        {
            var options = new InMemoryDatabaseOptions 
            { 
                CosmosDbEmulationMode = true,
                ThrowOnUnsupportedStep = false
            };
            var validator = new CosmosDbQueryValidator(options);
            var steps = new List<ExecutionEngine.TinkerGraphStep>
            {
                new ExecutionEngine.TinkerGraphStep("V"),
                new ExecutionEngine.TinkerGraphStep("sideEffect")
            };
            
            var result = validator.Validate(steps);
            
            result.IsValid.Should().BeTrue();
            result.HasWarnings.Should().BeTrue();
            result.Warnings.Should().ContainSingle(w => w.StepName == "sideEffect");
        }
        
        [Fact]
        public void Validator_ShouldWarnForHighRepeatDepth()
        {
            var options = new InMemoryDatabaseOptions { CosmosDbEmulationMode = true };
            var validator = new CosmosDbQueryValidator(options);
            var steps = new List<ExecutionEngine.TinkerGraphStep>
            {
                new ExecutionEngine.TinkerGraphStep("V"),
                new ExecutionEngine.TinkerGraphStep("repeat"),
                new ExecutionEngine.TinkerGraphStep("times") { Arguments = { 60 } }
            };
            
            var result = validator.Validate(steps);
            
            result.HasWarnings.Should().BeTrue();
            result.Warnings.Should().Contain(w => w.Code == ValidationWarningCode.PerformanceRisk);
        }
        
        [Fact]
        public void Validator_ShouldFailForExceedingRepeatDepth()
        {
            var options = new InMemoryDatabaseOptions { CosmosDbEmulationMode = true };
            var validator = new CosmosDbQueryValidator(options);
            var steps = new List<ExecutionEngine.TinkerGraphStep>
            {
                new ExecutionEngine.TinkerGraphStep("V"),
                new ExecutionEngine.TinkerGraphStep("repeat"),
                new ExecutionEngine.TinkerGraphStep("times") { Arguments = { 150 } }
            };
            
            var result = validator.Validate(steps);
            
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Code == ValidationErrorCode.ExceedsLimit);
        }
        
        [Fact]
        public void Validator_ShouldWarnForMissingPartitionKey()
        {
            var options = new InMemoryDatabaseOptions 
            { 
                CosmosDbEmulationMode = true,
                PartitionKeyPath = "/pk",
                EnforceCrossPartitionQueryRestrictions = true
            };
            var validator = new CosmosDbQueryValidator(options);
            var steps = new List<ExecutionEngine.TinkerGraphStep>
            {
                new ExecutionEngine.TinkerGraphStep("V"),
                new ExecutionEngine.TinkerGraphStep("has") { Arguments = { "name", "test" } }
            };
            
            var result = validator.Validate(steps);
            
            result.HasWarnings.Should().BeTrue();
            result.Warnings.Should().Contain(w => w.Code == ValidationWarningCode.CrossPartitionQuery);
        }
        
        [Fact]
        public void Validator_ShouldNotWarnForPartitionKeyFilter()
        {
            var options = new InMemoryDatabaseOptions 
            { 
                CosmosDbEmulationMode = true,
                PartitionKeyPath = "/pk",
                EnforceCrossPartitionQueryRestrictions = true
            };
            var validator = new CosmosDbQueryValidator(options);
            var steps = new List<ExecutionEngine.TinkerGraphStep>
            {
                new ExecutionEngine.TinkerGraphStep("V"),
                new ExecutionEngine.TinkerGraphStep("has") { Arguments = { "pk", "tenant1" } }
            };
            
            var result = validator.Validate(steps);
            
            result.Warnings.Should().NotContain(w => w.Code == ValidationWarningCode.CrossPartitionQuery);
        }
        
        #endregion
        
        #region Rate Limiter Tests
        
        [Fact]
        public void RateLimiter_ShouldAllowRequestWithinBudget()
        {
            var limiter = new CosmosDbRateLimiter(1000);
            var result = limiter.TryConsume(100);
            
            result.IsAllowed.Should().BeTrue();
            result.ConsumedRU.Should().Be(100);
        }
        
        [Fact]
        public void RateLimiter_ShouldDenyRequestExceedingBudget()
        {
            var limiter = new CosmosDbRateLimiter(100);
            var result = limiter.TryConsume(200);
            
            result.IsAllowed.Should().BeFalse();
            result.RetryAfterMs.Should().BeGreaterThan(0);
        }
        
        [Fact]
        public void RateLimiter_ShouldReplenishOverTime()
        {
            var limiter = new CosmosDbRateLimiter(100);
            limiter.TryConsume(50);
            System.Threading.Thread.Sleep(100);
            var available = limiter.AvailableRU;
            
            available.Should().BeGreaterThan(50);
        }
        
        [Fact]
        public void RateLimiter_Reset_ShouldRestoreFullCapacity()
        {
            var limiter = new CosmosDbRateLimiter(1000);
            limiter.TryConsume(800);
            limiter.Reset();
            
            limiter.AvailableRU.Should().Be(1000);
        }
        
        #endregion
        
        #region RU Cost Calculator Tests
        
        [Fact]
        public void RUCostCalculator_CalculateReadCost_ShouldReturnMinimumCost()
        {
            var settings = new CosmosDbRUCostSettings();
            var calculator = new RUCostCalculator(settings);
            var cost = calculator.CalculateReadCost(1, 100, false);
            
            cost.Should().BeGreaterOrEqualTo(1.0);
        }
        
        [Fact]
        public void RUCostCalculator_CalculateReadCost_ShouldApplyCrossPartitionMultiplier()
        {
            var settings = new CosmosDbRUCostSettings { CrossPartitionMultiplier = 2.5 };
            var calculator = new RUCostCalculator(settings);
            var singlePartitionCost = calculator.CalculateReadCost(10, 1024, false);
            var crossPartitionCost = calculator.CalculateReadCost(10, 1024, true);
            
            crossPartitionCost.Should().BeGreaterThan(singlePartitionCost);
            crossPartitionCost.Should().BeApproximately(singlePartitionCost * 2.5, 0.01);
        }
        
        [Fact]
        public void RUCostCalculator_CalculateVertexCreateCost_ShouldIncludeSizeCost()
        {
            var settings = new CosmosDbRUCostSettings { CreateVertexRU = 5.0, WritePerKBRU = 5.0 };
            var calculator = new RUCostCalculator(settings);
            var smallVertexCost = calculator.CalculateVertexCreateCost(100);
            var largeVertexCost = calculator.CalculateVertexCreateCost(2048);
            
            largeVertexCost.Should().BeGreaterThan(smallVertexCost);
        }
        
        #endregion
        
        #region Integration Tests
        
        [Fact]
        public async Task CosmosDbEmulatorMode_ShouldExecuteSupportedQueries()
        {
            var connector = InMemoryGremlinLanguageConnector.CreateCosmosDbEmulator();
            
            await connector.ExecuteAsync("g.addV('person').property('id', 'p1').property('name', 'John')", 
                new Dictionary<string, object>());
            await connector.ExecuteAsync("g.addV('person').property('id', 'p2').property('name', 'Jane')", 
                new Dictionary<string, object>());
            await connector.ExecuteAsync("g.V('p1').addE('knows').to(g.V('p2'))", 
                new Dictionary<string, object>());
            
            var result = await connector.ExecuteAsync("g.V('p1').out('knows').values('name')", 
                new Dictionary<string, object>());
            
            var resultList = result.Cast<object>().Select(x => x?.ToString()).ToList();
            resultList.Should().ContainSingle();
            resultList.First().Should().Be("Jane");
        }
        
        [Fact]
        public async Task CosmosDbEmulatorMode_ShouldTrackConsumedRU()
        {
            var connector = InMemoryGremlinLanguageConnector.CreateCosmosDbEmulator();
            var initialRU = connector.ConsumedRU;
            
            await connector.ExecuteAsync("g.addV('person').property('id', 'p1')", 
                new Dictionary<string, object>());
            
            connector.ConsumedRU.Should().BeGreaterThan(initialRU);
        }
        
        #endregion
    }
}
