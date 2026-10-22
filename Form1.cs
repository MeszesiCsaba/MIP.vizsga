using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MIPVizsga
{
    public partial class Form1 : Form
    {
        private SqliteRepository repository;
        private List<Employee> employees = new List<Employee>();
        private readonly List<ProjectAssignment> assignments = new List<ProjectAssignment>();
        private readonly List<Project> projects = new List<Project>();
        private DataGridView employeeGrid;
        private DataGridView assignmentGrid;
        private DataGridView projectGrid;
        private ComboBox projectManagerComboBox;
        private ComboBox programmerComboBox;
        private NumericUpDown budgetInput;
        private NumericUpDown hoursInput;
        private TextBox projectNameInput;
        private Label costLabel;
        private Label differenceLabel;
        private Label statusLabel;
        private Button acceptButton;

        public Form1()
        {
            InitializeComponent();
            BuildInterface();

            try
            {
                repository = new SqliteRepository();
                RefreshEmployees();
                RefreshProjects();
            }
            catch (Exception exception)
            {
                MessageBox.Show("Az adatbázis nem nyitható meg: " + exception.Message,
                    "Adatbázishiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BuildInterface()
        {
            Text = "Projektcsapat-tervező";
            ClientSize = new Size(1100, 720);
            MinimumSize = new Size(900, 600);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage employeesTab = new TabPage("Employees");
            TabPage projectsTab = new TabPage("Projects");
            tabs.TabPages.Add(employeesTab);
            tabs.TabPages.Add(projectsTab);
            Controls.Add(tabs);

            Button generateButton = new Button
            {
                Text = "Generate employees",
                Dock = DockStyle.Top,
                Height = 42,
                Margin = new Padding(10)
            };
            generateButton.Click += GenerateEmployeesClicked;

            employeeGrid = CreateGrid();
            employeeGrid.Columns.Add("EmployeeName", "Név");
            employeeGrid.Columns.Add("EmployeeRole", "Munkakör");
            employeeGrid.Columns.Add("EmployeeSeniority", "Senioritás");
            employeeGrid.Columns.Add("EmployeeSalary", "Havi bér (RON)");
            employeeGrid.Columns[0].FillWeight = 35;
            employeeGrid.Columns[1].FillWeight = 25;
            employeeGrid.Columns[2].FillWeight = 20;
            employeeGrid.Columns[3].FillWeight = 20;

            employeesTab.Controls.Add(employeeGrid);
            employeesTab.Controls.Add(generateButton);

            BuildProjectsTab(projectsTab);
        }

        private void BuildProjectsTab(TabPage tab)
        {
            Panel editor = new Panel { Dock = DockStyle.Top, Height = 210, Padding = new Padding(10) };

            Label nameLabel = new Label { Text = "Projekt neve", AutoSize = true, Location = new Point(10, 15) };
            projectNameInput = new TextBox { Location = new Point(115, 10), Width = 200 };
            projectNameInput.TextChanged += delegate { UpdateCalculation(); };

            Label budgetLabel = new Label { Text = "Becsült keret (RON)", AutoSize = true, Location = new Point(340, 15) };
            budgetInput = CreateNumberInput(0, 1000000000m, 2);
            budgetInput.Location = new Point(475, 10);
            budgetInput.Width = 150;
            budgetInput.ValueChanged += delegate { UpdateCalculation(); };

            Label managerLabel = new Label { Text = "Projektmenedzser", AutoSize = true, Location = new Point(650, 15) };
            projectManagerComboBox = new ComboBox
            {
                Location = new Point(770, 10),
                Width = 250,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            projectManagerComboBox.SelectedIndexChanged += delegate { UpdateCalculation(); };

            Label programmerLabel = new Label { Text = "Fejlesztő", AutoSize = true, Location = new Point(10, 55) };
            programmerComboBox = new ComboBox
            {
                Location = new Point(115, 50),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            Label hoursLabel = new Label { Text = "Óraszám", AutoSize = true, Location = new Point(340, 55) };
            hoursInput = CreateNumberInput(0.01m, 1000000m, 2);
            hoursInput.Location = new Point(405, 50);
            hoursInput.Width = 100;
            hoursInput.Value = 40;

            Button addButton = new Button { Text = "Tag hozzáadása", AutoSize = true, Location = new Point(525, 48) };
            addButton.Click += AddAssignmentClicked;
            Button removeButton = new Button { Text = "Kijelölt tag eltávolítása", AutoSize = true, Location = new Point(650, 48) };
            removeButton.Click += RemoveAssignmentClicked;

            assignmentGrid = CreateGrid();
            assignmentGrid.Dock = DockStyle.None;
            assignmentGrid.Location = new Point(10, 85);
            assignmentGrid.Size = new Size(1010, 70);
            assignmentGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            assignmentGrid.Columns.Add("Developer", "Fejlesztő");
            assignmentGrid.Columns.Add("DeveloperHours", "Óraszám");

            costLabel = new Label { AutoSize = true, Location = new Point(10, 165), Font = new Font(Font, FontStyle.Bold) };
            differenceLabel = new Label { AutoSize = true, Location = new Point(230, 165) };
            statusLabel = new Label { AutoSize = true, Location = new Point(470, 165), Width = 450 };
            acceptButton = new Button { Text = "Accept project", AutoSize = true, Location = new Point(920, 158) };
            acceptButton.Click += AcceptProjectClicked;

            editor.Controls.AddRange(new Control[]
            {
                nameLabel, projectNameInput, budgetLabel, budgetInput, managerLabel,
                projectManagerComboBox, programmerLabel, programmerComboBox, hoursLabel,
                hoursInput, addButton, removeButton, assignmentGrid, costLabel,
                differenceLabel, statusLabel, acceptButton
            });

            projectGrid = CreateGrid();
            projectGrid.Dock = DockStyle.Fill;
            projectGrid.Columns.Add("ProjectName", "Projekt");
            projectGrid.Columns.Add("ProjectManager", "Projektmenedzser");
            projectGrid.Columns.Add("ProjectCost", "Számított költség (RON)");
            projectGrid.Columns.Add("ProjectBudget", "Becsült keret (RON)");
            tab.Controls.Add(projectGrid);
            tab.Controls.Add(editor);
        }

        private static DataGridView CreateGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false
            };
        }

        private static NumericUpDown CreateNumberInput(decimal minimum, decimal maximum, int decimalPlaces)
        {
            return new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                DecimalPlaces = decimalPlaces,
                ThousandsSeparator = true
            };
        }

        private void GenerateEmployeesClicked(object sender, System.EventArgs e)
        {
            if (repository == null)
            {
                return;
            }

            try
            {
                int generatedCount = repository.GenerateEmployees();
                RefreshEmployees();
                bool sampleProjectCreated = false;
                if (repository.GetProjects().Count == 0 && employees.OfType<ProjectManager>().Any()
                    && employees.OfType<Programmer>().Any(programmer => programmer.Seniority == Seniority.Junior)
                    && employees.OfType<Programmer>().Any(programmer => programmer.Seniority == Seniority.Mid))
                {
                    CreateSampleProject();
                    RefreshProjects();
                    sampleProjectCreated = true;
                }

                string message = generatedCount == 0
                    ? "A munkatársak már létrejöttek; nem készült duplikátum."
                    : generatedCount + " munkatárs hozzáadva.";
                if (sampleProjectCreated)
                {
                    message += " Egy elfogadott mintaprojekt is létrejött.";
                }

                MessageBox.Show(message, "Munkatársak", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                MessageBox.Show("A munkatársak generálása sikertelen: " + exception.Message,
                    "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CreateSampleProject()
        {
            ProjectManager manager = employees.OfType<ProjectManager>().First();
            Programmer junior = employees.OfType<Programmer>()
                .First(programmer => programmer.Seniority == Seniority.Junior);
            Programmer mid = employees.OfType<Programmer>()
                .First(programmer => programmer.Seniority == Seniority.Mid);
            Project project = new Project
            {
                Name = "Sample project",
                EstimatedBudget = 10000m,
                ProjectManager = manager,
                PmHours = 24m,
                Assignments = new List<ProjectAssignment>
                {
                    new ProjectAssignment { Programmer = junior, AllocatedHours = 80m },
                    new ProjectAssignment { Programmer = mid, AllocatedHours = 40m }
                }
            };
            project.CalculatedCost = project.Assignments.Sum(assignment =>
                assignment.Programmer.GetHourlyRate() * assignment.AllocatedHours)
                + manager.GetHourlyRate() * project.PmHours;
            repository.SaveProject(project);
        }

        private void RefreshEmployees()
        {
            employees = repository.GetEmployees();
            employeeGrid.Rows.Clear();
            foreach (Employee employee in employees)
            {
                string seniority = employee is Programmer ? ((Programmer)employee).Seniority.ToString() : "-";
                employeeGrid.Rows.Add(employee.Name, employee.GetRoleName(), seniority,
                    employee.MonthlySalary.ToString("N2"));
            }

            projectManagerComboBox.Items.Clear();
            foreach (ProjectManager manager in employees.OfType<ProjectManager>())
            {
                projectManagerComboBox.Items.Add(manager);
            }

            programmerComboBox.Items.Clear();
            foreach (Programmer programmer in employees.OfType<Programmer>())
            {
                programmerComboBox.Items.Add(programmer);
            }

            if (projectManagerComboBox.Items.Count > 0)
            {
                projectManagerComboBox.SelectedIndex = 0;
            }

            if (programmerComboBox.Items.Count > 0)
            {
                programmerComboBox.SelectedIndex = 0;
            }

            UpdateCalculation();
        }

        private void AddAssignmentClicked(object sender, System.EventArgs e)
        {
            Programmer programmer = programmerComboBox.SelectedItem as Programmer;
            if (programmer == null)
            {
                return;
            }

            if (assignments.Any(assignment => assignment.Programmer.Id == programmer.Id))
            {
                MessageBox.Show("Ez a fejlesztő már szerepel a csapatban.", "Csapat",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            assignments.Add(new ProjectAssignment { Programmer = programmer, AllocatedHours = hoursInput.Value });
            RefreshAssignments();
            UpdateCalculation();
        }

        private void RemoveAssignmentClicked(object sender, System.EventArgs e)
        {
            if (assignmentGrid.CurrentRow == null || assignmentGrid.CurrentRow.Index >= assignments.Count)
            {
                return;
            }

            assignments.RemoveAt(assignmentGrid.CurrentRow.Index);
            RefreshAssignments();
            UpdateCalculation();
        }

        private void RefreshAssignments()
        {
            assignmentGrid.Rows.Clear();
            foreach (ProjectAssignment assignment in assignments)
            {
                assignmentGrid.Rows.Add(assignment.Programmer.Name,
                    assignment.AllocatedHours.ToString("N2"));
            }
        }

        private void UpdateCalculation()
        {
            decimal developerHours = assignments.Sum(assignment => assignment.AllocatedHours);
            decimal developerCost = assignments.Sum(assignment =>
                assignment.Programmer.GetHourlyRate() * assignment.AllocatedHours);
            ProjectManager manager = projectManagerComboBox == null
                ? null : projectManagerComboBox.SelectedItem as ProjectManager;
            decimal pmHours = developerHours * 0.20m;
            decimal cost = developerCost + (manager == null ? 0m : manager.GetHourlyRate() * pmHours);
            decimal difference = budgetInput == null ? 0m : budgetInput.Value - cost;

            costLabel.Text = "Számított költség: " + cost.ToString("N2") + " RON (PM: " + pmHours.ToString("N2") + " óra)";
            differenceLabel.Text = "Keretkülönbség: " + difference.ToString("N2") + " RON";
            if (assignments.Count > 0 && manager != null && difference < 0)
            {
                statusLabel.Text = "A projekt nem fér bele a költségkeretbe.";
                statusLabel.ForeColor = Color.Red;
            }
            else if (assignments.Count == 0 || manager == null)
            {
                statusLabel.Text = "Adj meg egy projektmenedzsert és legalább egy fejlesztőt.";
                statusLabel.ForeColor = Color.DarkOrange;
            }
            else
            {
                statusLabel.Text = "A projekt belefér a költségkeretbe.";
                statusLabel.ForeColor = Color.Green;
            }

            acceptButton.Enabled = manager != null && assignments.Count > 0
                && !string.IsNullOrWhiteSpace(projectNameInput.Text) && difference >= 0;
        }

        private void AcceptProjectClicked(object sender, System.EventArgs e)
        {
            ProjectManager manager = projectManagerComboBox.SelectedItem as ProjectManager;
            if (manager == null || assignments.Count == 0 || string.IsNullOrWhiteSpace(projectNameInput.Text))
            {
                MessageBox.Show("Adj meg projektnevet, egy projektmenedzsert és legalább egy fejlesztőt.",
                    "Hiányos projekt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal developerHours = assignments.Sum(assignment => assignment.AllocatedHours);
            decimal calculatedCost = assignments.Sum(assignment =>
                assignment.Programmer.GetHourlyRate() * assignment.AllocatedHours)
                + manager.GetHourlyRate() * developerHours * 0.20m;
            if (calculatedCost > budgetInput.Value)
            {
                UpdateCalculation();
                return;
            }

            Project project = new Project
            {
                Name = projectNameInput.Text.Trim(),
                EstimatedBudget = budgetInput.Value,
                CalculatedCost = calculatedCost,
                ProjectManager = manager,
                PmHours = developerHours * 0.20m,
                Assignments = assignments.Select(assignment => new ProjectAssignment
                {
                    Programmer = assignment.Programmer,
                    AllocatedHours = assignment.AllocatedHours
                }).ToList()
            };

            try
            {
                repository.SaveProject(project);
                RefreshProjects();
                assignments.Clear();
                RefreshAssignments();
                projectNameInput.Clear();
                UpdateCalculation();
                MessageBox.Show("A projekt elfogadva és elmentve.", "Projekt",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                MessageBox.Show("A projekt mentése sikertelen: " + exception.Message,
                    "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshProjects()
        {
            projects.Clear();
            projects.AddRange(repository.GetProjects());
            projectGrid.Rows.Clear();
            foreach (Project project in projects)
            {
                projectGrid.Rows.Add(project.Name, project.ProjectManager.Name,
                    project.CalculatedCost.ToString("N2"), project.EstimatedBudget.ToString("N2"));
            }
        }
    }
}
