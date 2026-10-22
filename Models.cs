using System;
using System.Collections.Generic;

namespace MIPVizsga
{
    public enum Seniority
    {
        Junior,
        Mid,
        Senior
    }

    public abstract class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Employee : Person
    {
        public decimal MonthlySalary { get; set; }

        public virtual string GetRoleName()
        {
            return "Employee";
        }

        public decimal GetHourlyRate()
        {
            return MonthlySalary / 160m;
        }

        public override string ToString()
        {
            return Name + " (" + GetRoleName() + ")";
        }
    }

    public class Programmer : Employee
    {
        public Seniority Seniority { get; set; }

        public override string GetRoleName()
        {
            return Seniority + " programmer";
        }
    }

    public class ProjectManager : Employee
    {
        public override string GetRoleName()
        {
            return "Project manager";
        }
    }

    public class Director : Employee
    {
        public override string GetRoleName()
        {
            return "Director";
        }
    }

    public class HrDirector : Director
    {
        public override string GetRoleName()
        {
            return "HR director";
        }
    }

    public class ProjectAssignment
    {
        public Programmer Programmer { get; set; }
        public decimal AllocatedHours { get; set; }
    }

    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal EstimatedBudget { get; set; }
        public decimal CalculatedCost { get; set; }
        public ProjectManager ProjectManager { get; set; }
        public decimal PmHours { get; set; }
        public List<ProjectAssignment> Assignments { get; set; }

        public Project()
        {
            Assignments = new List<ProjectAssignment>();
        }
    }
}
