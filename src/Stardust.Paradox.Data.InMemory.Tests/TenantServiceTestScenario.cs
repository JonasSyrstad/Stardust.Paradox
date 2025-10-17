using Stardust.Paradox.Data.InMemory.Scenarios;

namespace Stardust.Paradox.Data.InMemory.Tests
{
    // Extension helpers
    public static class DateTimeExtensions
    {
        public static long ToEpoch(this DateTime dateTime)
        {
            return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
        }
    }
    public class SocialNetworkTestScenario : InMemoryScenarioProviderBase
    {
        // Test GUIDs for consistent testing - each person needs unique user ID
        private static readonly string User1Id = "550e8400-e29b-41d4-a716-446655440010";
        private static readonly string User2Id = "550e8400-e29b-41d4-a716-446655440002";
        private static readonly string User3Id = "550e8400-e29b-41d4-a716-446655440003";
        private static readonly string AdminUserId = "550e8440-e29b-41d4-a716-446655440000";
        
        private static readonly string NetworkId = "550e8401-e29b-41d4-a716-446655440001";
        private static readonly string CommunityId = "550e8431-e29b-41d4-a716-446655440001";
        private static readonly string GroupId = "550e8421-e29b-41d4-a716-446655440001";
        private static readonly string Group2Id = "550e8422-e29b-41d4-a716-446655440002";
        private static readonly string PersonId = "550e8411-e29b-41d4-a716-446655440001";
        private static readonly string Person2Id = "550e8412-e29b-41d4-a716-446655440002";
        private static readonly string Person3Id = "550e8413-e29b-41d4-a716-446655440003"; // For integration tests
        private static readonly string InterestGroupId = "550e8451-e29b-41d4-a716-446655440001";
        private static readonly string InterestGroup2Id = "550e8452-e29b-41d4-a716-446655440002"; // Clean group for testing
        private static readonly string ModeratorPersonId = "550e8441-e29b-41d4-a716-446655440001";
        
        // Additional unique IDs for built-in groups and special persons
        private static readonly string NetworkAdminsGroupId = "550e8461-e29b-41d4-a716-446655440001";
        private static readonly string CommunityModsGroupId = "550e8462-e29b-41d4-a716-446655440002";
        private static readonly string InfluencerPersonId = "550e8470-e29b-41d4-a716-446655440012";
        private static readonly string DefaultTestPersonId = "550e8471-e29b-41d4-a716-446655440000";
        private static readonly string Group3Id = "550e8423-e29b-41d4-a716-446655440003"; // Group without member limits

        public override string ScenarioName { get; } = "SocialNetworkTestScenario";
        public override string Description { get; } = "Comprehensive social network test data with communities, groups, and moderators";

        protected override (ScenarioVertexDefinition[] vertices, ScenarioEdgeDefinition[] edges) GetScenarioData()
        {
            var currentTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            var vertices = new ScenarioVertexDefinition[]
            {
                // Social Network vertex - ENABLE INTEREST GROUPS
                new ScenarioVertexDefinition(NetworkId, "socialEntity", Props(
                    ("name", "Test Social Network"),
                    ("pk", NetworkId.ToLower()),
                    ("entityType", "network"),
                    ("networkType", "public"),
                    ("privacyMode", "Open"),
                    ("isPrivate", false),
                    ("isDisabled", false),
                    ("sn_toggle_InterestGroups", "true"), // CRITICAL: Enable interest groups
                    ("createDateTime", currentTime),
                    ("createdBy", "system")
                )),
                
                // Built-in Interest Groups (like moderator groups)
                new ScenarioVertexDefinition(NetworkAdminsGroupId, "socialEntity", Props(
                    ("pk", NetworkId.ToLower()), // CRITICAL: Make sure pk matches networkId
                    ("createDateTime", currentTime),
                    ("entityType", "interestGroup"),
                    ("name", "NetworkAdmins"),
                    ("builtIn", "true") // CRITICAL: Must be string "true" for built-in groups
                )),
                // CRITICAL: Community Moderators group  
                new ScenarioVertexDefinition(CommunityModsGroupId, "socialEntity", Props(
                    ("pk", NetworkId.ToLower()), // CRITICAL: Make sure pk matches networkId
                    ("createDateTime", currentTime),
                    ("entityType", "interestGroup"),
                    ("name", "CommunityMods"),
                    ("builtIn", "true") // CRITICAL: Must be string "true" for built-in groups
                )),
                
                // Social Groups
                new ScenarioVertexDefinition(GroupId, "socialEntity", Props(
                    ("name", "Test Social Group"),
                    ("pk", NetworkId.ToLower()),
                    ("entityType", "socialGroup"),
                    ("communityId", CommunityId),
                    ("createDateTime", currentTime),
                    ("createdBy", "system"),
                    ("privacyLevel", "public"),
                    ("isActive", true),
                    ("interestTags", "technology,gaming,social"),
                    ("autoAcceptMembers", false),
                    ("membershipCap", 100),
                    ("requiresApproval", false),
                    ("groupType", "interest"),
                    ("category", "Technology"),
                    ("description", "A group for tech enthusiasts"),
                    ("status", "active")
                    // Removed memberLimit and sn_memberLimit to allow interest groups
                )),
                // CRITICAL: Group without member limitations (like unlimited communities)
                new ScenarioVertexDefinition(Group3Id, "socialEntity", Props(
                    ("autoAcceptMembers", "false"),
                    ("isActive", "true"),
                    ("membershipCap", "50"),
                    ("privacyLevel", "private"),
                    ("interestTags", "photography,art"),
                    ("requiresApproval", "true"),
                    ("modifiedDateTime", currentTime),
                    ("description", "Photography and art enthusiasts"),
                    ("modifiedBy", "system"),
                    ("pk", NetworkId.ToLower()),
                    ("createDateTime", currentTime),
                    ("entityType", "socialGroup"),
                    ("createdBy", "system"),
                    ("groupType", "hobby"),
                    ("communityId", CommunityId),
                    ("category", "Arts"),
                    ("name", "Test Group Without Member Limits"),
                    ("status", "active")
                )),
                
                // Person profiles - each with unique user ID
                new ScenarioVertexDefinition(PersonId, "socialEntity", Props(
                    ("name", "Test User Person"),
                    ("pk", NetworkId.ToLower()),
                    ("entityType", "person"),
                    ("userId", User1Id),
                    ("email", "testuser@social.com"),
                    ("isBot", false),
                    ("createDateTime", currentTime),
                    ("createdBy", "system"),
                    ("displayName", "Test User"),
                    ("bio", "A test user for social networking"),
                    ("location", "Virtual City"),
                    ("joinDate", currentTime)
                )),
                new ScenarioVertexDefinition(Person2Id, "socialEntity", Props(
                    ("name", "Test User Person 2"),
                    ("pk", NetworkId.ToLower()),
                    ("entityType", "person"),
                    ("userId", User2Id),
                    ("email", "testuser2@social.com"),
                    ("isBot", false),
                    ("createDateTime", currentTime),
                    ("createdBy", "system"),
                    ("displayName", "Test User 2"),
                    ("bio", "Another test user for social networking"),
                    ("location", "Digital Town"),
                    ("joinDate", currentTime)
                )),
                new ScenarioVertexDefinition(Person3Id, "socialEntity", Props(
                    ("name", "Integration Test Person"),
                    ("pk", NetworkId.ToLower()),
                    ("entityType", "person"),
                    ("userId", User3Id),
                    ("email", "integration@social.com"),
                    ("isBot", false),
                    ("createDateTime", currentTime),
                    ("createdBy", "system"),
                    ("displayName", "Integration Tester"),
                    ("bio", "Integration test user"),
                    ("location", "Test Environment"),
                    ("joinDate", currentTime)
                )),
                new ScenarioVertexDefinition(ModeratorPersonId, "socialEntity", Props(
                    ("name", "Moderator User Person"),
                    ("pk", NetworkId.ToLower()),
                    ("entityType", "person"),
                    ("userId", AdminUserId),
                    ("email", "moderator@social.com"),
                    ("isBot", false),
                    ("createDateTime", currentTime),
                    ("createdBy", "system"),
                    ("displayName", "Moderator"),
                    ("bio", "Community moderator"),
                    ("location", "Moderation Central"),
                    ("joinDate", currentTime)
                )),
                
                // CRITICAL: Person for Influencer user (550e8400-e29b-41d4-a716-446655440012)
                new ScenarioVertexDefinition(InfluencerPersonId, "socialEntity", Props(
                    ("name", "Influencer Test User"),
                    ("pk", NetworkId.ToLower()),
                    ("entityType", "person"),
                    ("userId", "550e8400-e29b-41d4-a716-446655440012"), // This matches the TestsBase user ID
                    ("email", "influencer@social.com"),
                    ("isBot", false),
                    ("createDateTime", currentTime),
                    ("createdBy", "system"),
                    ("displayName", "Social Influencer"),
                    ("bio", "Test influencer account"),
                    ("location", "Social Hub"),
                    ("joinDate", currentTime)
                )),
                
                // CRITICAL: Person for the DEFAULT test user (550e8400-e29b-41d4-a716-446655440000) who needs NetworkAdmin privileges
                new ScenarioVertexDefinition(DefaultTestPersonId, "socialEntity", Props(
                    ("name", "Default Test User"),
                    ("pk", NetworkId.ToLower()),
                    ("entityType", "person"),
                    ("userId", "550e8400-e29b-41d4-a716-446655440000"), // This is the user being checked in the logs
                    ("email", "defaulttest@social.com"),
                    ("isBot", false),
                    ("sn_userRole", "admin"), // CRITICAL: Add the admin user role
                    ("createDateTime", currentTime),
                    ("createdBy", "system"),
                    ("displayName", "Default Admin"),
                    ("bio", "Default test administrator"),
                    ("location", "Admin Center"),
                    ("joinDate", currentTime)
                )),
                
                // Interest Group
                new ScenarioVertexDefinition(InterestGroupId, "socialEntity", Props(
                    ("name", "Test Interest Group"),
                    ("pk", NetworkId.ToLower()),
                    ("entityType", "interestGroup"),
                    ("builtIn", false),
                    ("createDateTime", currentTime),
                    ("createdBy", "system"),
                    ("topic", "Gaming"),
                    ("description", "Gaming enthusiasts group")
                )),
                new ScenarioVertexDefinition("550e8452-e29b-41d4-a716-44665544000f", "socialEntity", Props(
                    ("name", "Test Interest Group2"),
                    ("pk", NetworkId.ToLower()),
                    ("entityType", "interestGroup"),
                    ("builtIn", false),
                    ("createDateTime", currentTime),
                    ("createdBy", "system"),
                    ("topic", "Music"),
                    ("description", "Music lovers group")
                )),
                new ScenarioVertexDefinition(InterestGroup2Id, "socialEntity", Props(
                    ("name", "Clean Test Interest Group"),
                    ("pk", NetworkId.ToLower()),
                    ("entityType", "interestGroup"),
                    ("builtIn", false),
                    ("createDateTime", currentTime),
                    ("createdBy", "system"),
                    ("topic", "Books"),
                    ("description", "Book reading club")
                ))
            };

            var edges = new ScenarioEdgeDefinition[]
            {
                // CRITICAL: Influencer user membership in NetworkAdmins group
                new ScenarioEdgeDefinition("members_NetworkAdmins_Influencer", "members", NetworkAdminsGroupId, DefaultTestPersonId, Props(
                    ("memberType", "person"),
                    ("name", "Influencer Test User"),
                    ("createDateTime", currentTime),
                    ("userId", "550e8400-e29b-41d4-a716-446655440000"), // This is the user ID being checked in authorization
                    ("createdBy", "system"),
                    ("email", "influencer@social.com"),
                    ("isBot", false),
                    ("isModerator", true) // Set as moderator to have proper privileges
                )),
                new ScenarioEdgeDefinition("memberOf_Influencer_NetworkAdmins", "memberOf", DefaultTestPersonId, NetworkAdminsGroupId, Props(
                    ("memberType", "person"),
                    ("name", "Influencer Test User"),
                    ("createDateTime", currentTime),
                    ("userId", "550e8400-e29b-41d4-a716-446655440000"), // This is the user ID being checked in authorization
                    ("createdBy", "system"),
                    ("email", "influencer@social.com"),
                    ("isBot", false),
                    ("isModerator", true) // Set as moderator to have proper privileges
                )),
                
                // Influencer user network membership
                new ScenarioEdgeDefinition($"memberOf_Influencer_{NetworkId}", "memberOf", InfluencerPersonId, NetworkId, Props(
                    ("name", "Influencer Test User"),
                    ("memberType", "person"),
                    ("createDateTime", currentTime),
                    ("userId", "550e8400-e29b-41d4-a716-446655440012"),
                    ("membershipLevel", "premium"),
                    ("membershipStatus", "active"),
                    ("email", "influencer@social.com"),
                    ("isModerator", true)
                )),
                
                // Regular test user memberships
                new ScenarioEdgeDefinition($"memberOf_{PersonId}_{NetworkId}", "memberOf", PersonId, NetworkId, Props(
                    ("name", "Test User Person"),
                    ("memberType", "person"),
                    ("createDateTime", currentTime),
                    ("userId", User1Id),
                    ("membershipLevel", "basic"),
                    ("membershipStatus", "active"),
                    ("email", "testuser@social.com")
                )),
                
                // Moderator membership for proper authorization
                new ScenarioEdgeDefinition($"memberOf_{ModeratorPersonId}_{NetworkId}", "memberOf", ModeratorPersonId, NetworkId, Props(
                    ("name", "Moderator User Person"),
                    ("memberType", "person"),
                    ("createDateTime", currentTime),
                    ("userId", AdminUserId),
                    ("membershipLevel", "premium"),
                    ("membershipStatus", "active"),
                    ("email", "moderator@social.com"),
                    ("isModerator", true)
                )),
                
                // Person memberships to social groups
                new ScenarioEdgeDefinition($"memberOf_{PersonId}_{GroupId}", "memberOf", PersonId, GroupId, Props(
                    ("name", "Test User Person"),
                    ("memberType", "person"),
                    ("createDateTime", currentTime),
                    ("userId", User1Id),
                    ("membershipLevel", "basic"),
                    ("membershipStatus", "active"),
                    ("email", "testuser@social.com")
                )),
                new ScenarioEdgeDefinition($"memberOf_{Person2Id}_{GroupId}", "memberOf", Person2Id, GroupId, Props(
                    ("name", "Test User Person 2"),
                    ("memberType", "person"),
                    ("createDateTime", currentTime),
                    ("userId", User2Id),
                    ("membershipLevel", "active"),
                    ("membershipStatus", "active"),
                    ("email", "testuser2@social.com")
                )),
                new ScenarioEdgeDefinition($"memberOf_{ModeratorPersonId}_{GroupId}", "memberOf", ModeratorPersonId, GroupId, Props(
                    ("name", "Moderator User Person"),
                    ("memberType", "person"),
                    ("createDateTime", currentTime),
                    ("userId", AdminUserId),
                    ("membershipLevel", "moderator"),
                    ("membershipStatus", "active"),
                    ("email", "moderator@social.com"),
                    ("isModerator", true)
                )),
                
                // Person membership in interest group
                new ScenarioEdgeDefinition($"members_{InterestGroupId}_{PersonId}", "members", InterestGroupId, PersonId, Props(
                    ("name", "Test User Person"),
                    ("memberType", "person"),
                    ("createDateTime", currentTime),
                    ("userId", User1Id)
                )),
                
                // Moderator relationships for group moderation
                new ScenarioEdgeDefinition($"moderates_{ModeratorPersonId}_{GroupId}", "moderates", ModeratorPersonId, GroupId, Props(
                    ("name", "Moderator User Person"),
                    ("memberType", "person"),
                    ("createDateTime", currentTime),
                    ("userId", AdminUserId),
                    ("membershipLevel", "moderator"),
                    ("isModerator", true)
                )),
                
                // Default test user network membership  
                new ScenarioEdgeDefinition($"memberOf_DefaultUser_{NetworkId}", "memberOf", DefaultTestPersonId, NetworkId, Props(
                    ("name", "Default Test User"),
                    ("memberType", "person"),
                    ("createDateTime", currentTime),
                    ("userId", "550e8400-e29b-41d4-a716-446655440000"),
                    ("membershipLevel", "premium"),
                    ("membershipStatus", "active"),
                    ("email", "defaulttest@social.com"),
                    ("isModerator", true)
                )),
                
                // CRITICAL: Default test user membership in NetworkAdmins group (this is the user being checked)
                new ScenarioEdgeDefinition("members_NetworkAdmins_DefaultUser", "members", NetworkAdminsGroupId, DefaultTestPersonId, Props(
                    ("memberType", "personMember"), // Match social network pattern
                    ("name", "Default Test User"),
                    ("createDateTime", currentTime),
                    ("userId", "550e8400-e29b-41d4-a716-446655440000"), // This is the user ID being checked in authorization
                    ("createdBy", "system"),
                    ("joinedDate", currentTime - 86400), // Day before creation
                    ("email", "defaulttest@social.com"),
                    ("isBot", false),
                    ("isModerator", false) // Match social network pattern
                )),
                new ScenarioEdgeDefinition("memberOf_DefaultUser_NetworkAdmins", "memberOf", DefaultTestPersonId, NetworkAdminsGroupId, Props(
                    ("memberType", "personMember"), // Match social network pattern
                    ("name", "Default Test User"),
                    ("createDateTime", currentTime),
                    ("userId", "550e8400-e29b-41d4-a716-446655440000"), // This is the user ID being checked in authorization
                    ("createdBy", "system"),
                    ("joinedDate", currentTime - 86400), // Day before creation
                    ("email", "defaulttest@social.com"),
                    ("isBot", false),
                    ("isModerator", false) // Match social network pattern
                )),
                
                // CRITICAL: Person memberships to groups (matching social network pattern exactly)
                new ScenarioEdgeDefinition("memberOf_DefaultUser_Group3", "memberOf", DefaultTestPersonId, Group3Id, Props(
                    ("memberType", "personMembership"),
                    ("name", "Default Test User"),
                    ("createDateTime", currentTime),
                    ("userId", "550e8400-e29b-41d4-a716-446655440000"),
                    ("createdBy", "system"),
                    ("joinedDate", currentTime - 86400),
                    ("email", "defaulttest@social.com"),
                    ("isBot", "false")
                )),
                new ScenarioEdgeDefinition("members_Group3_DefaultUser", "members", Group3Id, DefaultTestPersonId, Props(
                    ("memberType", "personMembership"),
                    ("name", "Default Test User"),
                    ("createDateTime", currentTime),
                    ("userId", "550e8400-e29b-41d4-a716-446655440000"),
                    ("createdBy", "system"),
                    ("joinedDate", currentTime - 86400),
                    ("email", "defaulttest@social.com"),
                    ("isBot", "false")
                )),
                
                // Friend relationships
                new ScenarioEdgeDefinition($"follows_{PersonId}_{Person2Id}", "follows", PersonId, Person2Id, Props(
                    ("followType", "friend"),
                    ("status", "accepted"),
                    ("createDateTime", currentTime),
                    ("followedDate", currentTime - 3600)
                )),
                new ScenarioEdgeDefinition($"follows_{Person2Id}_{PersonId}", "follows", Person2Id, PersonId, Props(
                    ("followType", "friend"),
                    ("status", "accepted"),
                    ("createDateTime", currentTime),
                    ("followedDate", currentTime - 3600)
                )),
            };

            return (vertices, edges);
        }
    }
}