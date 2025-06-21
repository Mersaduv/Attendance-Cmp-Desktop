using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AttandenceDesktop.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AttandenceDesktop.ViewModels
{
    public class EmployeeDialogViewModel : ObservableValidator
    {
        private int _id;
        private string _firstName;
        private string _lastName;
        private string _email;
        private string _phoneNumber;
        private string _position;
        private string _employeeCode;
        private int _departmentId;
        private DateTime _hireDate = DateTime.Today;
        private List<Department> _availableDepartments;
        private Department _selectedDepartment;
        
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }
        
        [Required(ErrorMessage = "First Name is required")]
        [StringLength(50, ErrorMessage = "First Name cannot be longer than 50 characters")]
        public string FirstName
        {
            get => _firstName;
            set => SetProperty(ref _firstName, value, true);
        }
        
        [Required(ErrorMessage = "Last Name is required")]
        [StringLength(50, ErrorMessage = "Last Name cannot be longer than 50 characters")]
        public string LastName
        {
            get => _lastName;
            set => SetProperty(ref _lastName, value, true);
        }
        
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        [StringLength(100, ErrorMessage = "Email cannot be longer than 100 characters")]
        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value, true);
        }
        
        [Required(ErrorMessage = "Phone Number is required")]
        [StringLength(20, ErrorMessage = "Phone Number cannot be longer than 20 characters")]
        public string PhoneNumber
        {
            get => _phoneNumber;
            set => SetProperty(ref _phoneNumber, value, true);
        }
        
        [Required(ErrorMessage = "Position is required")]
        [StringLength(100, ErrorMessage = "Position cannot be longer than 100 characters")]
        public string Position
        {
            get => _position;
            set => SetProperty(ref _position, value, true);
        }
        
        [Required(ErrorMessage = "Employee Code is required")]
        [StringLength(20, ErrorMessage = "Employee Code cannot be longer than 20 characters")]
        public string EmployeeCode
        {
            get => _employeeCode;
            set => SetProperty(ref _employeeCode, value, true);
        }
        
        [Required(ErrorMessage = "Department is required")]
        public int DepartmentId
        {
            get => _departmentId;
            set => SetProperty(ref _departmentId, value, true);
        }
        
        [Required(ErrorMessage = "Hire Date is required")]
        public DateTime HireDate
        {
            get => _hireDate;
            set => SetProperty(ref _hireDate, value, true);
        }
        
        public List<Department> AvailableDepartments
        {
            get => _availableDepartments;
            set => SetProperty(ref _availableDepartments, value);
        }
        
        public Department SelectedDepartment
        {
            get => _selectedDepartment;
            set
            {
                SetProperty(ref _selectedDepartment, value, true);
                if (value != null)
                {
                    DepartmentId = value.Id;
                }
            }
        }
        
        public bool IsNew => Id == 0;
        
        public string WindowTitle => IsNew ? "Add Employee" : "Edit Employee";
        
        public Employee ToEmployee()
        {
            return new Employee
            {
                Id = Id,
                FirstName = FirstName,
                LastName = LastName,
                Email = Email,
                PhoneNumber = PhoneNumber,
                Position = Position,
                EmployeeCode = EmployeeCode,
                DepartmentId = DepartmentId,
                HireDate = HireDate
            };
        }
        
        public void LoadFromEmployee(Employee employee)
        {
            if (employee == null) return;
            
            Id = employee.Id;
            FirstName = employee.FirstName;
            LastName = employee.LastName;
            Email = employee.Email;
            PhoneNumber = employee.PhoneNumber;
            Position = employee.Position;
            EmployeeCode = employee.EmployeeCode;
            DepartmentId = employee.DepartmentId;
            HireDate = employee.HireDate;
        }
    }
} 