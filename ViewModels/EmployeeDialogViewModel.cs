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
        private bool _isFlexibleHours;
        private double _requiredWorkHoursPerDay = 8.0;
        private int _leaveDays = 2;
        private string? _zkUserId;
        private bool _isFingerprintRegistered;
        private byte[]? _fingerprintTemplate1;
        private string? _employeeNumber;
        
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
        
        public bool IsFlexibleHours
        {
            get => _isFlexibleHours;
            set => SetProperty(ref _isFlexibleHours, value);
        }
        
        [Range(1, 24, ErrorMessage = "Work hours must be between 1 and 24")]
        public double RequiredWorkHoursPerDay
        {
            get => _requiredWorkHoursPerDay;
            set => SetProperty(ref _requiredWorkHoursPerDay, value, true);
        }

        [Range(0, 365, ErrorMessage = "Leave days must be between 0 and 365")]
        public int LeaveDays
        {
            get => _leaveDays;
            set => SetProperty(ref _leaveDays, value, true);
        }
        
        [Required]
        public string? ZkUserId
        {
            get => _zkUserId;
            set => SetProperty(ref _zkUserId, value, true);
        }
        
        public bool IsFingerprintRegistered
        {
            get => _isFingerprintRegistered;
            set => SetProperty(ref _isFingerprintRegistered, value);
        }
        
        public byte[]? FingerprintTemplate1
        {
            get => _fingerprintTemplate1;
            set => SetProperty(ref _fingerprintTemplate1, value);
        }
        
        [Required]
        [StringLength(50)]
        public string? EmployeeNumber
        {
            get => _employeeNumber;
            set
            {
                if(SetProperty(ref _employeeNumber, value, true))
                {
                    if(string.IsNullOrWhiteSpace(ZkUserId) && !string.IsNullOrWhiteSpace(value))
                        ZkUserId = value;
                }
            }
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
                EmployeeCode = string.IsNullOrWhiteSpace(EmployeeCode) ? EmployeeNumber : EmployeeCode,
                EmployeeNumber = EmployeeNumber,
                ZkUserId = ZkUserId ?? EmployeeNumber,
                DepartmentId = DepartmentId,
                HireDate = HireDate,
                IsFlexibleHours = IsFlexibleHours,
                RequiredWorkHoursPerDay = RequiredWorkHoursPerDay,
                LeaveDays = LeaveDays,
                FingerprintTemplate1 = FingerprintTemplate1
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
            EmployeeNumber = string.IsNullOrWhiteSpace(employee.EmployeeNumber) ? employee.EmployeeCode : employee.EmployeeNumber;
            DepartmentId = employee.DepartmentId;
            HireDate = employee.HireDate;
            IsFlexibleHours = employee.IsFlexibleHours;
            RequiredWorkHoursPerDay = employee.RequiredWorkHoursPerDay;
            LeaveDays = employee.LeaveDays;
            ZkUserId = employee.ZkUserId;
            FingerprintTemplate1 = employee.FingerprintTemplate1;
            IsFingerprintRegistered = employee.FingerprintTemplate1 != null && employee.FingerprintTemplate1.Length > 0;
        }
    }
} 