export interface DepartmentDto {
  id: number;
  name: string;
  description: string | null;
  employeeCount: number;
  createdAt: string;
}

export type EmployeeStatus = 'Active' | 'OnLeave' | 'Terminated';

export interface EmployeeDto {
  id: number;
  fullName: string;
  email: string;
  jobTitle: string | null;
  departmentId: number | null;
  departmentName: string | null;
  hireDate: string | null;
  status: EmployeeStatus;
  createdAt: string;
}

export interface EmployeeInput {
  fullName: string;
  email: string;
  jobTitle: string | null;
  departmentId: number | null;
  hireDate: string | null;
  status: EmployeeStatus;
}
