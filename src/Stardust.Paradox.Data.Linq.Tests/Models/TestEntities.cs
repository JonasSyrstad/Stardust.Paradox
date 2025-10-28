using Stardust.Paradox.Data.Annotations;

namespace Stardust.Paradox.Data.Linq.Tests.Models
{
    [VertexLabel("person")]
    public interface IPerson : IVertex
    {
        string Name { get; set; }
        string Email { get; set; }
        int Age { get; set; }
        bool IsActive { get; set; }
        decimal Score { get; set; }
        string City { get; set; }
        
        [OutLabel("worksAt")]
        IEdgeCollection<ICompany> Companies { get; }
        
        [OutLabel("friendsWith")]
        IEdgeCollection<IPerson> Friends { get; }

        [OutLabel("assignedTo")]
        IEdgeCollection<IProject> Projects { get; }
     
        [OutLabel("hasSkill")]
        IEdgeCollection<ISkill> Skills { get; }
    }
    
    [VertexLabel("company")]
    public interface ICompany : IVertex
    {
        string Name { get; set; }
        string Industry { get; set; }
        int EmployeeCount { get; set; }
        int Founded { get; set; }
        
        [InLabel("worksAt")]
        IEdgeCollection<IPerson> Employees { get; }
    }
    
    [VertexLabel("project")]
    public interface IProject : IVertex
    {
        string Name { get; set; }
        string Status { get; set; }
        int Budget { get; set; }
        int Priority { get; set; }
        
        [InLabel("assignedTo")]
        IEdgeCollection<IPerson> TeamMembers { get; }
    }
    
    [VertexLabel("skill")]
    public interface ISkill : IVertex
    {
        string Name { get; set; }
        string Category { get; set; }
        string Level { get; set; }
        
        [InLabel("hasSkill")]
        IEdgeCollection<IPerson> Practitioners { get; }
    }
    
    [InLabel("worksAt")]
    public interface IEmployment : IEdge<IPerson, ICompany>
    {
        string Role { get; set; }
        string StartDate { get; set; }
        int Salary { get; set; }
    }
    
    [InLabel("friendsWith")]
    public interface IFriendship : IEdge<IPerson, IPerson>
    {
        string Since { get; set; }
        string Strength { get; set; }
    }
    
    [InLabel("assignedTo")]
    public interface IAssignment : IEdge<IPerson, IProject>
    {
        string Role { get; set; }
        int HoursPerWeek { get; set; }
    }
    
    [InLabel("hasSkill")]
    public interface IUserSkill : IEdge<IPerson, ISkill>
    {
        int YearsExperience { get; set; }
        int Proficiency { get; set; }
    }
}
