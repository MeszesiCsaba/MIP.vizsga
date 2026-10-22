using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace MIPVizsga
{
    public class SqliteRepository
    {
        private readonly string connectionString;

        public SqliteRepository()
        {
            string databasePath = TraceLogger.GetSubmissionFilePath("agency.db");
            connectionString = "Data Source=" + databasePath + ";Version=3;Foreign Keys=True;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string sql = @"
                    CREATE TABLE IF NOT EXISTS Employees (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Role TEXT NOT NULL,
                        Seniority TEXT NULL,
                        MonthlySalary REAL NOT NULL CHECK (MonthlySalary > 0)
                    );
                    CREATE TABLE IF NOT EXISTS Projects (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        EstimatedBudget REAL NOT NULL,
                        CalculatedCost REAL NOT NULL,
                        PmEmployeeId INTEGER NOT NULL,
                        PmHours REAL NOT NULL,
                        FOREIGN KEY (PmEmployeeId) REFERENCES Employees(Id)
                    );
                    CREATE TABLE IF NOT EXISTS ProjectAssignments (
                        ProjectId INTEGER NOT NULL,
                        EmployeeId INTEGER NOT NULL,
                        AllocatedHours REAL NOT NULL CHECK (AllocatedHours > 0),
                        PRIMARY KEY (ProjectId, EmployeeId),
                        FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
                        FOREIGN KEY (EmployeeId) REFERENCES Employees(Id)
                    );";
                using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }

            TraceLogger.Initialize();
        }

        public List<Employee> GetEmployees()
        {
            List<Employee> employees = new List<Employee>();
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            using (SQLiteCommand command = new SQLiteCommand(
                "SELECT Id, Name, Role, Seniority, MonthlySalary FROM Employees ORDER BY Role, Name", connection))
            {
                connection.Open();
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        employees.Add(CreateEmployee(
                            Convert.ToInt32(reader["Id"]),
                            Convert.ToString(reader["Name"]),
                            Convert.ToString(reader["Role"]),
                            reader["Seniority"] == DBNull.Value ? null : Convert.ToString(reader["Seniority"]),
                            Convert.ToDecimal(reader["MonthlySalary"])));
                    }
                }
            }

            return employees;
        }

        public int GenerateEmployees()
        {
            List<Employee> existingEmployees = GetEmployees();
            Dictionary<string, int> roleCounts = existingEmployees
                .GroupBy(employee => employee.GetRoleName())
                .ToDictionary(group => group.Key, group => group.Count());
            List<Employee> newEmployees = new List<Employee>();

            AddRoleDeficit(newEmployees, roleCounts, "Junior programmer", 30, Seniority.Junior);
            AddRoleDeficit(newEmployees, roleCounts, "Mid programmer", 20, Seniority.Mid);
            AddRoleDeficit(newEmployees, roleCounts, "Senior programmer", 10, Seniority.Senior);
            AddRoleDeficit(newEmployees, roleCounts, "Project manager", 10, null);
            AddRoleDeficit(newEmployees, roleCounts, "Director", 2, null);
            AddRoleDeficit(newEmployees, roleCounts, "HR director", 2, null);

            int currentCount = existingEmployees.Count + newEmployees.Count;
            for (int index = currentCount; index < 100; index++)
            {
                newEmployees.Add(new Employee
                {
                    Name = "Employee " + (index + 1).ToString("000"),
                    MonthlySalary = 5000m + (index % 6) * 500m
                });
            }

            if (newEmployees.Count == 0)
            {
                return 0;
            }

            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    foreach (Employee employee in newEmployees)
                    {
                        using (SQLiteCommand command = new SQLiteCommand(
                            "INSERT INTO Employees (Name, Role, Seniority, MonthlySalary) VALUES (@name, @role, @seniority, @salary)",
                            connection, transaction))
                        {
                            command.Parameters.AddWithValue("@name", employee.Name);
                            command.Parameters.AddWithValue("@role", employee.GetRoleName());
                            command.Parameters.AddWithValue("@seniority", employee is Programmer
                                ? (object)((Programmer)employee).Seniority.ToString() : DBNull.Value);
                            command.Parameters.AddWithValue("@salary", employee.MonthlySalary);
                            command.ExecuteNonQuery();
                            employee.Id = Convert.ToInt32(connection.LastInsertRowId);
                        }
                    }

                    transaction.Commit();
                }
            }

            foreach (Employee employee in newEmployees)
            {
                TraceLogger.LogDatabaseInsert("Employees", employee.Id);
            }

            return newEmployees.Count;
        }

        public int SaveProject(Project project)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    using (SQLiteCommand command = new SQLiteCommand(
                        "INSERT INTO Projects (Name, EstimatedBudget, CalculatedCost, PmEmployeeId, PmHours) VALUES (@name, @budget, @cost, @pmId, @pmHours)",
                        connection, transaction))
                    {
                        command.Parameters.AddWithValue("@name", project.Name);
                        command.Parameters.AddWithValue("@budget", project.EstimatedBudget);
                        command.Parameters.AddWithValue("@cost", project.CalculatedCost);
                        command.Parameters.AddWithValue("@pmId", project.ProjectManager.Id);
                        command.Parameters.AddWithValue("@pmHours", project.PmHours);
                        command.ExecuteNonQuery();
                        project.Id = Convert.ToInt32(connection.LastInsertRowId);
                    }

                    foreach (ProjectAssignment assignment in project.Assignments)
                    {
                        using (SQLiteCommand command = new SQLiteCommand(
                            "INSERT INTO ProjectAssignments (ProjectId, EmployeeId, AllocatedHours) VALUES (@projectId, @employeeId, @hours)",
                            connection, transaction))
                        {
                            command.Parameters.AddWithValue("@projectId", project.Id);
                            command.Parameters.AddWithValue("@employeeId", assignment.Programmer.Id);
                            command.Parameters.AddWithValue("@hours", assignment.AllocatedHours);
                            command.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }

            TraceLogger.LogDatabaseInsert("Projects", project.Id);
            TraceLogger.LogProjectAccepted(project);
            return project.Id;
        }

        public List<Project> GetProjects()
        {
            List<Project> projects = new List<Project>();
            Dictionary<int, Employee> employees = GetEmployees().ToDictionary(employee => employee.Id);

            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(
                    "SELECT Id, Name, EstimatedBudget, CalculatedCost, PmEmployeeId, PmHours FROM Projects ORDER BY Id DESC", connection))
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        projects.Add(new Project
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            Name = Convert.ToString(reader["Name"]),
                            EstimatedBudget = Convert.ToDecimal(reader["EstimatedBudget"]),
                            CalculatedCost = Convert.ToDecimal(reader["CalculatedCost"]),
                            ProjectManager = (ProjectManager)employees[Convert.ToInt32(reader["PmEmployeeId"])] ,
                            PmHours = Convert.ToDecimal(reader["PmHours"])
                        });
                    }
                }

                foreach (Project project in projects)
                {
                    using (SQLiteCommand command = new SQLiteCommand(
                        "SELECT EmployeeId, AllocatedHours FROM ProjectAssignments WHERE ProjectId = @projectId", connection))
                    {
                        command.Parameters.AddWithValue("@projectId", project.Id);
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                project.Assignments.Add(new ProjectAssignment
                                {
                                    Programmer = (Programmer)employees[Convert.ToInt32(reader["EmployeeId"])],
                                    AllocatedHours = Convert.ToDecimal(reader["AllocatedHours"])
                                });
                            }
                        }
                    }
                }
            }

            return projects;
        }

        private static void AddRoleDeficit(List<Employee> employees, Dictionary<string, int> counts,
            string role, int minimum, Seniority? seniority)
        {
            int existingCount;
            counts.TryGetValue(role, out existingCount);
            int deficit = Math.Max(0, minimum - existingCount);
            for (int index = 0; index < deficit; index++)
            {
                int number = existingCount + index + 1;
                Employee employee;
                if (seniority.HasValue)
                {
                    employee = new Programmer
                    {
                        Seniority = seniority.Value,
                        MonthlySalary = seniority.Value == Seniority.Junior ? 8000m
                            : seniority.Value == Seniority.Mid ? 12000m : 18000m,
                        Name = role + " " + number.ToString("00")
                    };
                }
                else if (role == "Project manager")
                {
                    employee = new ProjectManager { Name = role + " " + number.ToString("00"), MonthlySalary = 16000m };
                }
                else if (role == "HR director")
                {
                    employee = new HrDirector { Name = role + " " + number.ToString("00"), MonthlySalary = 22000m };
                }
                else
                {
                    employee = new Director { Name = role + " " + number.ToString("00"), MonthlySalary = 20000m };
                }

                employees.Add(employee);
            }
        }

        private static Employee CreateEmployee(int id, string name, string role, string seniority, decimal salary)
        {
            if (role == "Project manager")
            {
                return new ProjectManager { Id = id, Name = name, MonthlySalary = salary };
            }

            if (role == "Director")
            {
                return new Director { Id = id, Name = name, MonthlySalary = salary };
            }

            if (role == "HR director")
            {
                return new HrDirector { Id = id, Name = name, MonthlySalary = salary };
            }

            if (role == "Junior programmer" || role == "Mid programmer" || role == "Senior programmer")
            {
                Seniority employeeSeniority = (Seniority)Enum.Parse(typeof(Seniority), seniority);
                return new Programmer { Id = id, Name = name, MonthlySalary = salary, Seniority = employeeSeniority };
            }

            return new Employee { Id = id, Name = name, MonthlySalary = salary };
        }
    }
}
